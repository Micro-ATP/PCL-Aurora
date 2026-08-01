// Request parameters directly adapt PCL-CE's CurseForge world search in
// Plain Craft Launcher 2/Modules/Minecraft/ModComp.cs. The configured endpoint
// is the same public mirror PCL-CE uses and requires no bundled API credential.
using System.Globalization;
using PCL.Aurora.Domain;

namespace PCL.Aurora.Application;

public sealed class CurseForgeCommunityResourceSearchService(
    HttpClient httpClient,
    ILauncherPreferencesService? preferencesService = null)
    : ICommunityResourceSearchService
{
    private static readonly Uri SearchEndpoint =
        new("https://mod.mcimirror.top/curseforge/v1/mods/search");

    public async Task<CommunityResourceSearchResult> SearchAsync(
        CommunityResourceSearchRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!TryGetClassId(request.Type, out var classId))
        {
            return CommunityResourceSearchResult.Failure("CurseForge 暂不支持该社区资源类型。");
        }

        if (request.Page < 0 || request.PageSize is < 1 or > 40 ||
            request.Page > int.MaxValue / request.PageSize)
        {
            return CommunityResourceSearchResult.Failure("世界资源页码或每页数量无效。");
        }

        var searchText = request.SearchText.Trim();
        var gameVersion = request.GameVersion?.Trim();
        var category = request.Category?.Trim();
        if (searchText.Length > 200 || gameVersion?.Length > 80 ||
            !IsSafeVersion(gameVersion) || !IsSafeCategory(category))
        {
            return CommunityResourceSearchResult.Failure("世界资源搜索条件无效。");
        }

        try
        {
            var endpoint = BuildSearchUri(
                request with { SearchText = searchText, GameVersion = gameVersion, Category = category }, classId);
            var preference = preferencesService?.Current.EffectiveGameManagementOptions.CommunitySource
                ?? DownloadSourcePreference.PreferOfficialWithFallback;
            var officialEndpoint = endpoint.AbsoluteUri.Replace(
                "https://mod.mcimirror.top/curseforge",
                "https://api.curseforge.com",
                StringComparison.OrdinalIgnoreCase);
            var official = new Uri(officialEndpoint);
            var mirror = endpoint;
            var errors = new List<string>();
            foreach (var source in PclCeDownloadSourceResolver.OrderCommunity(preference, official, mirror))
            {
                try
                {
                    using var message = new HttpRequestMessage(HttpMethod.Get, source);
                    message.Headers.UserAgent.ParseAdd("PCL-Aurora/0.1");
                    message.Headers.Accept.ParseAdd("application/json");
                    using var response = await httpClient.SendAsync(message, cancellationToken).ConfigureAwait(false);
                    response.EnsureSuccessStatusCode();
                    return CurseForgeCommunityResourceParser.Parse(
                        await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false), request.Type);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception exception) when (exception is HttpRequestException or IOException)
                {
                    errors.Add($"{source.Host}：{exception.Message}");
                }
            }

            return CommunityResourceSearchResult.Failure($"无法获取 CurseForge 社区资源：{string.Join("；", errors)}");
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception) when (exception is HttpRequestException or IOException)
        {
            return CommunityResourceSearchResult.Failure($"无法获取 CurseForge 世界资源：{exception.Message}");
        }
    }

    private static Uri BuildSearchUri(CommunityResourceSearchRequest request, string classId)
    {
        var query = new List<KeyValuePair<string, string>>
        {
            new("gameId", "432"),
            new("classId", classId),
            new("sortOrder", "desc"),
            new("pageSize", request.PageSize.ToString(CultureInfo.InvariantCulture)),
            new("index", checked(request.Page * request.PageSize).ToString(CultureInfo.InvariantCulture)),
            new("sortField", GetSortField(request.Sort)),
        };
        if (!string.IsNullOrWhiteSpace(request.SearchText))
        {
            query.Add(new("searchFilter", request.SearchText));
        }

        if (!string.IsNullOrWhiteSpace(request.GameVersion))
        {
            query.Add(new("gameVersion", request.GameVersion));
        }

        if (!string.IsNullOrWhiteSpace(request.Category))
        {
            query.Add(new("categoryId", request.Category));
        }

        return new UriBuilder(SearchEndpoint)
        {
            Query = string.Join("&", query.Select(item =>
                $"{Uri.EscapeDataString(item.Key)}={Uri.EscapeDataString(item.Value)}")),
        }.Uri;
    }

    private static string GetSortField(CommunityResourceSort sort) => sort switch
    {
        CommunityResourceSort.Relevance => "4",
        CommunityResourceSort.Downloads => "6",
        CommunityResourceSort.Follows => "2",
        CommunityResourceSort.Newest => "11",
        CommunityResourceSort.Updated => "3",
        _ => "2",
    };

    private static bool IsSafeVersion(string? value) =>
        string.IsNullOrWhiteSpace(value) ||
        value.All(character => char.IsAsciiLetterOrDigit(character) || character is '.' or '-' or '_');

    private static bool IsSafeCategory(string? value) =>
        string.IsNullOrWhiteSpace(value) ||
        value is "248" or "249" or "250" or "251" or "252" or "253" or "4464";

    private static bool TryGetClassId(CommunityResourceType type, out string classId)
    {
        classId = type switch
        {
            CommunityResourceType.Mod => "6",
            CommunityResourceType.ModPack => "4471",
            CommunityResourceType.DataPack => "6945",
            CommunityResourceType.Shader => "6552",
            CommunityResourceType.ResourcePack => "12",
            CommunityResourceType.World => "17",
            _ => string.Empty,
        };
        return classId.Length > 0;
    }
}
