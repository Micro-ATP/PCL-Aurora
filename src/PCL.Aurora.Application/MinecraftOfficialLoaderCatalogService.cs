using PCL.Aurora.Domain;

namespace PCL.Aurora.Application;

public sealed class MinecraftOfficialLoaderCatalogService(HttpClient httpClient) : IMinecraftOfficialLoaderCatalogService
{
    private static readonly Uri ForgeMetadataUri = new("https://maven.minecraftforge.net/net/minecraftforge/forge/maven-metadata.xml");
    private static readonly Uri NeoForgeReleasesUri = new("https://maven.neoforged.net/api/maven/versions/releases/net/neoforged/neoforge");
    private static readonly Uri NeoForgeLegacyUri = new("https://maven.neoforged.net/api/maven/versions/releases/net/neoforged/forge");

    public async Task<MinecraftLoaderCatalogParseResult> FetchAsync(
        string minecraftVersion,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(minecraftVersion))
        {
            return new(null, ["请先选择 Minecraft 版本。"]);
        }

        try
        {
            var fabricUri = new Uri($"https://meta.fabricmc.net/v2/versions/loader/{Uri.EscapeDataString(minecraftVersion)}");
            var forgeTask = TryFetchAsync("Forge", ForgeMetadataUri, cancellationToken);
            var neoForgeTask = TryFetchAsync("NeoForge", NeoForgeReleasesUri, cancellationToken);
            var neoForgeLegacyTask = TryFetchAsync("NeoForge 遗留目录", NeoForgeLegacyUri, cancellationToken);
            var fabricTask = TryFetchAsync("Fabric", fabricUri, cancellationToken);
            await Task.WhenAll(forgeTask, neoForgeTask, neoForgeLegacyTask, fabricTask).ConfigureAwait(false);
            var responses = new[]
            {
                await forgeTask.ConfigureAwait(false),
                await neoForgeTask.ConfigureAwait(false),
                await neoForgeLegacyTask.ConfigureAwait(false),
                await fabricTask.ConfigureAwait(false),
            };
            var parsed = MinecraftOfficialLoaderCatalogParser.Parse(
                minecraftVersion,
                responses[0].Content,
                responses[1].Content,
                responses[2].Content,
                responses[3].Content);
            var errors = responses
                .Where(response => response.Error is not null)
                .Select(response => response.Error!)
                .Concat(parsed.Errors)
                .ToArray();
            return new(parsed.Catalog, errors);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception) when (exception is HttpRequestException or IOException)
        {
            return new(null, [$"无法获取官方加载器目录：{exception.Message}"]);
        }
    }

    private async Task<CatalogResponse> TryFetchAsync(string sourceName, Uri uri, CancellationToken cancellationToken)
    {
        try
        {
            using var response = await httpClient.GetAsync(uri, cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            return new(await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false), null);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception) when (exception is HttpRequestException or IOException)
        {
            return new(null, $"无法获取 {sourceName} 官方目录：{exception.Message}");
        }
    }

    private sealed record CatalogResponse(string? Content, string? Error);
}
