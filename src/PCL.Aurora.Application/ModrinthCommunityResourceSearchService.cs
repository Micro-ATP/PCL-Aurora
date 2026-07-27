// Directly adapts the public Modrinth request mapping from PCL-CE
// Plain Craft Launcher 2/Modules/Minecraft/ModComp.cs. Aurora limits the
// implementation to credential-free searches and uses its own domain model.
using System.Globalization;
using System.Text.Json;
using PCL.Aurora.Domain;

namespace PCL.Aurora.Application;

public sealed class ModrinthCommunityResourceSearchService(HttpClient httpClient) : ICommunityResourceSearchService
{
    private static readonly Uri SearchEndpoint = new("https://api.modrinth.com/v2/search");

    public async Task<CommunityResourceSearchResult> SearchAsync(
        CommunityResourceSearchRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.Type == CommunityResourceType.World)
        {
            return CommunityResourceSearchResult.Failure(
                "世界目录当前只有需要私有 API 凭据的来源，Aurora 未访问网络。");
        }

        if (request.Page < 0 ||
            request.PageSize is < 1 or > 40 ||
            request.Page > int.MaxValue / request.PageSize)
        {
            return CommunityResourceSearchResult.Failure("社区资源页码或每页数量无效。");
        }

        var searchText = request.SearchText.Trim();
        var gameVersion = request.GameVersion?.Trim();
        if (searchText.Length > 200 || gameVersion?.Length > 80)
        {
            return CommunityResourceSearchResult.Failure("搜索文本或 Minecraft 版本筛选过长。");
        }

        try
        {
            using var message = new HttpRequestMessage(HttpMethod.Get, BuildSearchUri(request with
            {
                SearchText = searchText,
                GameVersion = string.IsNullOrWhiteSpace(gameVersion) ? null : gameVersion,
            }));
            message.Headers.UserAgent.ParseAdd("PCL-Aurora/0.1");
            message.Headers.Accept.ParseAdd("application/json");
            using var response = await httpClient.SendAsync(message, cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            var json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            return ModrinthCommunityResourceParser.Parse(json, request.Type);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception) when (exception is HttpRequestException or IOException)
        {
            return CommunityResourceSearchResult.Failure($"无法获取 Modrinth 社区资源：{exception.Message}");
        }
    }

    private static Uri BuildSearchUri(CommunityResourceSearchRequest request)
    {
        var query = new List<KeyValuePair<string, string>>
        {
            new("limit", request.PageSize.ToString(CultureInfo.InvariantCulture)),
            new("offset", checked(request.Page * request.PageSize).ToString(CultureInfo.InvariantCulture)),
            new("index", GetSortValue(request.Sort)),
        };
        if (!string.IsNullOrWhiteSpace(request.SearchText))
        {
            query.Add(new("query", request.SearchText));
        }

        var facets = new List<string[]>
        {
            new[] { $"project_type:{GetProjectTypeValue(request.Type)}" },
        };
        if (request.Type == CommunityResourceType.DataPack)
        {
            facets.Add(new[] { "categories:datapack" });
        }

        if (!string.IsNullOrWhiteSpace(request.GameVersion))
        {
            facets.Add(new[] { $"versions:{request.GameVersion}" });
        }

        if (request.Loader != CommunityResourceLoader.Any &&
            request.Type is CommunityResourceType.Mod or CommunityResourceType.ModPack)
        {
            facets.Add(new[] { $"categories:{GetLoaderValue(request.Loader)}" });
        }

        query.Add(new("facets", JsonSerializer.Serialize(facets)));
        var queryText = string.Join(
            "&",
            query.Select(item => $"{Uri.EscapeDataString(item.Key)}={Uri.EscapeDataString(item.Value)}"));
        return new UriBuilder(SearchEndpoint) { Query = queryText }.Uri;
    }

    private static string GetProjectTypeValue(CommunityResourceType type) => type switch
    {
        CommunityResourceType.Mod => "mod",
        CommunityResourceType.ModPack => "modpack",
        CommunityResourceType.DataPack => "mod",
        CommunityResourceType.ResourcePack => "resourcepack",
        CommunityResourceType.Shader => "shader",
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, null),
    };

    private static string GetLoaderValue(CommunityResourceLoader loader) => loader switch
    {
        CommunityResourceLoader.Forge => "forge",
        CommunityResourceLoader.NeoForge => "neoforge",
        CommunityResourceLoader.Fabric => "fabric",
        CommunityResourceLoader.Quilt => "quilt",
        _ => throw new ArgumentOutOfRangeException(nameof(loader), loader, null),
    };

    private static string GetSortValue(CommunityResourceSort sort) => sort switch
    {
        CommunityResourceSort.Relevance => "relevance",
        CommunityResourceSort.Downloads => "downloads",
        CommunityResourceSort.Follows => "follows",
        CommunityResourceSort.Newest => "newest",
        CommunityResourceSort.Updated => "updated",
        _ => throw new ArgumentOutOfRangeException(nameof(sort), sort, null),
    };
}
