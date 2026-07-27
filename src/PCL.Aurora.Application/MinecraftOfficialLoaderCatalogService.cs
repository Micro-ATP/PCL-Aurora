using PCL.Aurora.Domain;

namespace PCL.Aurora.Application;

public sealed class MinecraftOfficialLoaderCatalogService(HttpClient httpClient) : IMinecraftOfficialLoaderCatalogService
{
    private static readonly Uri ForgeMetadataUri = new("https://maven.minecraftforge.net/net/minecraftforge/forge/maven-metadata.xml");
    private static readonly Uri NeoForgeReleasesUri = new("https://maven.neoforged.net/api/maven/versions/releases/net/neoforged/neoforge");
    private static readonly Uri NeoForgeLegacyUri = new("https://maven.neoforged.net/api/maven/versions/releases/net/neoforged/forge");
    private static readonly Uri OptiFineCatalogUri = new("https://bmclapi2.bangbang93.com/optifine/versionList");

    public async Task<MinecraftLoaderCatalogParseResult> FetchAsync(
        string minecraftVersion,
        MinecraftLoaderKind? loaderKind = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(minecraftVersion))
        {
            return new(null, ["请先选择 Minecraft 版本。"]);
        }

        try
        {
            var fabricUri = new Uri($"https://meta.fabricmc.net/v2/versions/loader/{Uri.EscapeDataString(minecraftVersion)}");
            var forgeTask = Includes(MinecraftLoaderKind.Forge)
                ? TryFetchAsync("Forge", ForgeMetadataUri, cancellationToken)
                : Task.FromResult(CatalogResponse.Empty);
            var neoForgeTask = Includes(MinecraftLoaderKind.NeoForge)
                ? TryFetchAsync("NeoForge", NeoForgeReleasesUri, cancellationToken)
                : Task.FromResult(CatalogResponse.Empty);
            var neoForgeLegacyTask = Includes(MinecraftLoaderKind.NeoForge)
                ? TryFetchAsync("NeoForge 遗留目录", NeoForgeLegacyUri, cancellationToken)
                : Task.FromResult(CatalogResponse.Empty);
            var fabricTask = Includes(MinecraftLoaderKind.Fabric)
                ? TryFetchAsync("Fabric", fabricUri, cancellationToken)
                : Task.FromResult(CatalogResponse.Empty);
            var optiFineTask = Includes(MinecraftLoaderKind.OptiFine)
                ? TryFetchAsync("OptiFine 公开目录", OptiFineCatalogUri, cancellationToken)
                : Task.FromResult(CatalogResponse.Empty);
            await Task.WhenAll(forgeTask, neoForgeTask, neoForgeLegacyTask, fabricTask, optiFineTask).ConfigureAwait(false);
            var responses = new[]
            {
                await forgeTask.ConfigureAwait(false),
                await neoForgeTask.ConfigureAwait(false),
                await neoForgeLegacyTask.ConfigureAwait(false),
                await fabricTask.ConfigureAwait(false),
                await optiFineTask.ConfigureAwait(false),
            };
            var parsed = MinecraftOfficialLoaderCatalogParser.Parse(
                minecraftVersion,
                responses[0].Content,
                responses[1].Content,
                responses[2].Content,
                responses[3].Content,
                responses[4].Content);
            var errors = responses
                .Where(response => response.Error is not null)
                .Select(response => response.Error!)
                .Concat(parsed.Errors)
                .ToArray();
            return new(parsed.Catalog, errors);

            bool Includes(MinecraftLoaderKind candidate) => loaderKind is null || loaderKind == candidate;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception) when (exception is HttpRequestException or IOException)
        {
            return new(null, [$"无法获取加载器目录：{exception.Message}"]);
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
            return new(null, $"无法获取 {sourceName} 目录：{exception.Message}");
        }
    }

    private sealed record CatalogResponse(string? Content, string? Error)
    {
        public static CatalogResponse Empty { get; } = new(null, null);
    }
}
