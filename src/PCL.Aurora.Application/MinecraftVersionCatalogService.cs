using PCL.Aurora.Domain;

namespace PCL.Aurora.Application;

public sealed class MinecraftVersionCatalogService(
    HttpClient httpClient,
    ILauncherPreferencesService? preferencesService = null) : IMinecraftVersionCatalogService
{
    private static readonly Uri ManifestUri = new("https://piston-meta.mojang.com/mc/game/version_manifest_v2.json");

    public async Task<MinecraftVersionCatalogParseResult> FetchAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var preference = preferencesService?.Current.EffectiveGameManagementOptions.VersionListSource
                ?? DownloadSourcePreference.PreferOfficialWithFallback;
            var sources = PclCeDownloadSourceResolver.Order(
                preference,
                ManifestUri,
                PclCeDownloadSourceResolver.ToBmclapi(ManifestUri));
            var errors = new List<string>();
            foreach (var source in sources)
            {
                try
                {
                    using var response = await httpClient.GetAsync(source, cancellationToken).ConfigureAwait(false);
                    response.EnsureSuccessStatusCode();
                    return MinecraftVersionCatalogParser.Parse(await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false));
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

            return new(null, [$"无法获取官方版本清单：{string.Join("；", errors)}"]);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception) when (exception is HttpRequestException or IOException)
        {
            return new(null, [$"无法获取官方版本清单：{exception.Message}"]);
        }
    }
}
