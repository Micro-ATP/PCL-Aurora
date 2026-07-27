// Directly adapts the public Modrinth version request flow from PCL2
// Plain Craft Launcher 2/Modules/Resource/ResourceVersion.vb and PCL-CE
// Plain Craft Launcher 2/Modules/Minecraft/ModComp.cs. Aurora keeps requests
// credential-free and parses them into its own cross-platform domain model.
using System.Text.Json;
using PCL.Aurora.Domain;

namespace PCL.Aurora.Application;

public sealed class ModrinthCommunityResourceVersionService(HttpClient httpClient) : ICommunityResourceVersionService
{
    private static readonly Uri ApiRoot = new("https://api.modrinth.com/v2/");

    public Task<CommunityResourceVersionCatalog> GetProjectVersionsAsync(
        string projectId,
        string? gameVersion,
        CommunityResourceLoader loader,
        CancellationToken cancellationToken = default)
    {
        if (!IsValidId(projectId))
        {
            return Task.FromResult(CommunityResourceVersionCatalog.Failure("Modrinth 项目 ID 无效。"));
        }

        var query = new List<string> { "include_changelog=false" };
        if (!string.IsNullOrWhiteSpace(gameVersion))
        {
            query.Add("game_versions=" + Uri.EscapeDataString(JsonSerializer.Serialize(new[] { gameVersion.Trim() })));
        }

        if (loader != CommunityResourceLoader.Any)
        {
            query.Add("loaders=" + Uri.EscapeDataString(JsonSerializer.Serialize(new[] { GetLoaderValue(loader) })));
        }

        var endpoint = new Uri(ApiRoot, $"project/{Uri.EscapeDataString(projectId)}/version?{string.Join("&", query)}");
        return SendAsync(endpoint, ModrinthCommunityResourceVersionParser.ParseCatalog, cancellationToken);
    }

    public Task<CommunityResourceVersionCatalog> GetVersionAsync(
        string versionId,
        CancellationToken cancellationToken = default)
    {
        if (!IsValidId(versionId))
        {
            return Task.FromResult(CommunityResourceVersionCatalog.Failure("Modrinth 版本 ID 无效。"));
        }

        return SendAsync(
            new Uri(ApiRoot, $"version/{Uri.EscapeDataString(versionId)}"),
            ModrinthCommunityResourceVersionParser.ParseSingle,
            cancellationToken);
    }

    private async Task<CommunityResourceVersionCatalog> SendAsync(
        Uri endpoint,
        Func<string, CommunityResourceVersionCatalog> parse,
        CancellationToken cancellationToken)
    {
        try
        {
            using var message = new HttpRequestMessage(HttpMethod.Get, endpoint);
            message.Headers.UserAgent.ParseAdd("PCL-Aurora/0.1");
            message.Headers.Accept.ParseAdd("application/json");
            using var response = await httpClient.SendAsync(message, cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            return parse(await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false));
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception) when (exception is HttpRequestException or IOException)
        {
            return CommunityResourceVersionCatalog.Failure($"无法获取 Modrinth 版本信息：{exception.Message}");
        }
    }

    private static bool IsValidId(string? value) =>
        !string.IsNullOrWhiteSpace(value) &&
        value.Length <= 80 &&
        value.All(character => char.IsLetterOrDigit(character) || character is '-' or '_');

    private static string GetLoaderValue(CommunityResourceLoader loader) => loader switch
    {
        CommunityResourceLoader.Forge => "forge",
        CommunityResourceLoader.NeoForge => "neoforge",
        CommunityResourceLoader.Fabric => "fabric",
        CommunityResourceLoader.Quilt => "quilt",
        _ => throw new ArgumentOutOfRangeException(nameof(loader), loader, null),
    };
}
