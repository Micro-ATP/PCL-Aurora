using PCL.Aurora.Domain;

namespace PCL.Aurora.Application;

public sealed class CurseForgeCommunityResourceVersionService(
    HttpClient httpClient,
    ILauncherPreferencesService? preferencesService = null)
    : ICommunityResourceVersionService
{
    private static readonly Uri ApiRoot = new("https://mod.mcimirror.top/curseforge/v1/");

    public Task<CommunityResourceVersionCatalog> GetProjectVersionsAsync(
        string projectId,
        string? gameVersion,
        CommunityResourceLoader loader,
        CancellationToken cancellationToken = default)
    {
        if (!IsNumericId(projectId))
        {
            return Task.FromResult(CommunityResourceVersionCatalog.Failure("CurseForge 项目 ID 无效。"));
        }

        var query = new List<string> { "pageSize=10000", "index=0" };
        if (!string.IsNullOrWhiteSpace(gameVersion))
        {
            query.Add("gameVersion=" + Uri.EscapeDataString(gameVersion.Trim()));
        }

        return SendAsync(
            new Uri(ApiRoot, $"mods/{projectId}/files?{string.Join("&", query)}"),
            CurseForgeCommunityResourceVersionParser.ParseCatalog,
            cancellationToken);
    }

    public Task<CommunityResourceVersionCatalog> GetVersionAsync(
        string versionId,
        CancellationToken cancellationToken = default)
    {
        if (!IsNumericId(versionId))
        {
            return Task.FromResult(CommunityResourceVersionCatalog.Failure("CurseForge 文件 ID 无效。"));
        }

        return SendAsync(
            new Uri(ApiRoot, $"mods/files/{versionId}"),
            CurseForgeCommunityResourceVersionParser.ParseSingle,
            cancellationToken);
    }

    private async Task<CommunityResourceVersionCatalog> SendAsync(
        Uri endpoint,
        Func<string, CommunityResourceVersionCatalog> parser,
        CancellationToken cancellationToken)
    {
        try
        {
            var preference = preferencesService?.Current.EffectiveGameManagementOptions.CommunitySource
                ?? DownloadSourcePreference.PreferOfficialWithFallback;
            var official = new Uri(endpoint.AbsoluteUri.Replace(
                "https://mod.mcimirror.top/curseforge",
                "https://api.curseforge.com",
                StringComparison.OrdinalIgnoreCase));
            var errors = new List<string>();
            foreach (var source in PclCeDownloadSourceResolver.OrderCommunity(preference, official, endpoint))
            {
                try
                {
                    using var message = new HttpRequestMessage(HttpMethod.Get, source);
                    message.Headers.UserAgent.ParseAdd("PCL-Aurora/0.1");
                    message.Headers.Accept.ParseAdd("application/json");
                    using var response = await httpClient.SendAsync(message, cancellationToken).ConfigureAwait(false);
                    response.EnsureSuccessStatusCode();
                    return parser(await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false));
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

            return CommunityResourceVersionCatalog.Failure($"无法获取 CurseForge 版本：{string.Join("；", errors)}");
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception) when (exception is HttpRequestException or IOException)
        {
            return CommunityResourceVersionCatalog.Failure($"无法获取 CurseForge 世界版本：{exception.Message}");
        }
    }

    private static bool IsNumericId(string? value) =>
        !string.IsNullOrWhiteSpace(value) && value.Length <= 20 && value.All(char.IsAsciiDigit);
}
