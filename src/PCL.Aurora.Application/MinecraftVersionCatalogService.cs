using PCL.Aurora.Domain;

namespace PCL.Aurora.Application;

public sealed class MinecraftVersionCatalogService(HttpClient httpClient) : IMinecraftVersionCatalogService
{
    private static readonly Uri ManifestUri = new("https://piston-meta.mojang.com/mc/game/version_manifest_v2.json");

    public async Task<MinecraftVersionCatalogParseResult> FetchAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using var response = await httpClient.GetAsync(ManifestUri, cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            return MinecraftVersionCatalogParser.Parse(await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false));
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
