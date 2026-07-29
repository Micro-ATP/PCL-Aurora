// Directly adapts the public Modrinth request mapping from PCL-CE
// Plain Craft Launcher 2/Modules/Minecraft/ModComp.cs. Aurora limits the
// implementation to credential-free searches and uses its own domain model.
using System.Globalization;
using System.Text.Json;
using PCL.Aurora.Domain;

namespace PCL.Aurora.Application;

public sealed class ModrinthCommunityResourceSearchService : ICommunityResourceSearchService
{
    private static readonly Uri SearchEndpoint = new("https://api.modrinth.com/v2/search");
    private readonly HttpClient httpClient;
    private readonly ICommunityResourceLocalizationService? localizationService;

    public ModrinthCommunityResourceSearchService(HttpClient httpClient)
        : this(httpClient, null)
    {
    }

    public ModrinthCommunityResourceSearchService(
        HttpClient httpClient,
        ICommunityResourceLocalizationService? localizationService)
    {
        this.httpClient = httpClient;
        this.localizationService = localizationService;
    }

    public async Task<CommunityResourceSearchResult> SearchAsync(
        CommunityResourceSearchRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.Type == CommunityResourceType.World)
        {
            return CommunityResourceSearchResult.Failure(
                "Modrinth 不提供世界资源项目类型，未发送搜索请求。");
        }

        if (request.Page < 0 ||
            request.PageSize is < 1 or > 40 ||
            request.Page > int.MaxValue / request.PageSize)
        {
            return CommunityResourceSearchResult.Failure("社区资源页码或每页数量无效。");
        }

        var searchText = request.SearchText.Trim();
        var gameVersion = request.GameVersion?.Trim();
        var category = request.Category?.Trim();
        if (searchText.Length > 200 || gameVersion?.Length > 80 ||
            category?.Length > 80 || !IsSafeFacetValue(category))
        {
            return CommunityResourceSearchResult.Failure("搜索条件无效。");
        }

        try
        {
            using var message = new HttpRequestMessage(HttpMethod.Get, BuildSearchUri(request with
            {
                SearchText = searchText,
                GameVersion = string.IsNullOrWhiteSpace(gameVersion) ? null : gameVersion,
                Category = string.IsNullOrWhiteSpace(category) ? null : category,
            }));
            message.Headers.UserAgent.ParseAdd("PCL-Aurora/0.1");
            message.Headers.Accept.ParseAdd("application/json");
            using var response = await httpClient.SendAsync(message, cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            var json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            var result = ModrinthCommunityResourceParser.Parse(json, request.Type);
            return localizationService is null || result.Projects.Count == 0
                ? result
                : result with
                {
                    Projects = result.Projects.Select(localizationService.Localize).ToArray(),
                };
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

        if (!string.IsNullOrWhiteSpace(request.Category))
        {
            facets.Add(new[] { $"categories:{request.Category}" });
        }

        if (!string.IsNullOrWhiteSpace(request.GameVersion))
        {
            facets.Add(new[] { $"versions:{request.GameVersion}" });
        }

        if (ShouldApplyLoaderFacet(request))
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
        CommunityResourceLoader.Vanilla => "vanilla",
        CommunityResourceLoader.Iris => "iris",
        CommunityResourceLoader.OptiFine => "optifine",
        _ => throw new ArgumentOutOfRangeException(nameof(loader), loader, null),
    };

    private static string GetSortValue(CommunityResourceSort sort) => sort switch
    {
        CommunityResourceSort.Default => "relevance",
        CommunityResourceSort.Relevance => "relevance",
        CommunityResourceSort.Downloads => "downloads",
        CommunityResourceSort.Follows => "follows",
        CommunityResourceSort.Newest => "newest",
        CommunityResourceSort.Updated => "updated",
        _ => throw new ArgumentOutOfRangeException(nameof(sort), sort, null),
    };

    private static bool ShouldApplyLoaderFacet(CommunityResourceSearchRequest request)
    {
        if (request.Loader == CommunityResourceLoader.Any ||
            request.Type is not (CommunityResourceType.Mod or CommunityResourceType.ModPack or CommunityResourceType.Shader))
        {
            return false;
        }

        if (request.Type is CommunityResourceType.Mod or CommunityResourceType.ModPack &&
            request.Loader == CommunityResourceLoader.Forge &&
            TryGetMinecraftMinorVersion(request.GameVersion, out var minorVersion) &&
            minorVersion < 14)
        {
            return false;
        }

        return true;
    }

    private static bool TryGetMinecraftMinorVersion(string? version, out int minorVersion)
    {
        minorVersion = 0;
        if (string.IsNullOrWhiteSpace(version))
        {
            return false;
        }

        var parts = version.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return parts.Length >= 2 && parts[0] == "1" &&
               int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out minorVersion);
    }

    private static bool IsSafeFacetValue(string? value) =>
        string.IsNullOrWhiteSpace(value) ||
        value.All(character => char.IsAsciiLetterOrDigit(character) || character is '-' or '+' or '_');
}
