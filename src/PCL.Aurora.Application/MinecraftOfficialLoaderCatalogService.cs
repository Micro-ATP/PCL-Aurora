using System.Text.Json;
using PCL.Aurora.Domain;

namespace PCL.Aurora.Application;

public sealed class MinecraftOfficialLoaderCatalogService(HttpClient httpClient) : IMinecraftOfficialLoaderCatalogService
{
    private static readonly Uri ForgeMetadataUri = new("https://maven.minecraftforge.net/net/minecraftforge/forge/maven-metadata.xml");
    private static readonly Uri NeoForgeReleasesUri = new("https://maven.neoforged.net/api/maven/versions/releases/net/neoforged/neoforge");
    private static readonly Uri NeoForgeLegacyUri = new("https://maven.neoforged.net/api/maven/versions/releases/net/neoforged/forge");
    private static readonly Uri NeoForgeMirrorReleasesUri = new("https://bmclapi2.bangbang93.com/neoforge/meta/api/maven/details/releases/net/neoforged/neoforge");
    private static readonly Uri NeoForgeMirrorLegacyUri = new("https://bmclapi2.bangbang93.com/neoforge/meta/api/maven/details/releases/net/neoforged/forge");
    private static readonly Uri OptiFineCatalogUri = new("https://bmclapi2.bangbang93.com/optifine/versionList");
    private static readonly Uri ForgeMinecraftVersionsUri = new("https://bmclapi2.bangbang93.com/forge/minecraft");
    private static readonly Uri FabricInstallersUri = new("https://meta.fabricmc.net/v2/versions/installer");
    private static readonly Uri CleanroomReleasesUri = new("https://api.github.com/repos/CleanroomMC/Cleanroom/releases?per_page=100");
    private static readonly Uri LegacyFabricVersionsUri = new("https://meta.legacyfabric.net/v2/versions");
    private static readonly Uri LabyModProductionUri = new("https://releases.r2.labymod.net/api/v1/manifest/production/latest.json");
    private static readonly Uri LabyModSnapshotUri = new("https://releases.r2.labymod.net/api/v1/manifest/snapshot/latest.json");
    private static readonly Uri LiteLoaderMirrorUri = new("https://bmclapi2.bangbang93.com/maven/com/mumfrey/liteloader/versions.json");
    private static readonly Uri LiteLoaderOfficialUri = new("https://dl.liteloader.com/versions/versions.json");

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
                ? TryFetchWithFallbackAsync("NeoForge", NeoForgeMirrorReleasesUri, NeoForgeReleasesUri, cancellationToken)
                : Task.FromResult(CatalogResponse.Empty);
            var neoForgeLegacyTask = Includes(MinecraftLoaderKind.NeoForge)
                ? TryFetchWithFallbackAsync("NeoForge 遗留目录", NeoForgeMirrorLegacyUri, NeoForgeLegacyUri, cancellationToken)
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

    public async Task<MinecraftLoaderDirectoryResult> FetchDirectoryAsync(
        MinecraftLoaderKind loaderKind,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return loaderKind switch
            {
                MinecraftLoaderKind.Forge => ParseSingle(
                    await TryFetchAsync("Forge", ForgeMinecraftVersionsUri, cancellationToken).ConfigureAwait(false),
                    MinecraftLoaderDirectoryParser.ParseForgeMinecraftVersions),
                MinecraftLoaderKind.NeoForge => await FetchNeoForgeDirectoryAsync(cancellationToken).ConfigureAwait(false),
                MinecraftLoaderKind.OptiFine => ParseSingle(
                    await TryFetchAsync("OptiFine", OptiFineCatalogUri, cancellationToken).ConfigureAwait(false),
                    MinecraftLoaderDirectoryParser.ParseOptiFineVersions),
                MinecraftLoaderKind.Fabric => ParseSingle(
                    await TryFetchAsync("Fabric", FabricInstallersUri, cancellationToken).ConfigureAwait(false),
                    MinecraftLoaderDirectoryParser.ParseFabricInstallers),
                MinecraftLoaderKind.Cleanroom => ParseSingle(
                    await TryFetchAsync("Cleanroom", CleanroomReleasesUri, cancellationToken).ConfigureAwait(false),
                    MinecraftLoaderDirectoryParser.ParseCleanroomVersions),
                MinecraftLoaderKind.LegacyFabric => ParseSingle(
                    await TryFetchAsync("Legacy Fabric", LegacyFabricVersionsUri, cancellationToken).ConfigureAwait(false),
                    MinecraftLoaderDirectoryParser.ParseLegacyFabricInstallers),
                MinecraftLoaderKind.LabyMod => await FetchLabyModDirectoryAsync(cancellationToken).ConfigureAwait(false),
                MinecraftLoaderKind.LiteLoader => ParseSingle(
                    await TryFetchWithFallbackAsync(
                        "LiteLoader",
                        LiteLoaderOfficialUri,
                        LiteLoaderMirrorUri,
                        cancellationToken).ConfigureAwait(false),
                    MinecraftLoaderDirectoryParser.ParseLiteLoaderVersions),
                _ => new(null, [$"暂不支持 {loaderKind} 的独立安装包目录。"]),
            };
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception) when (exception is JsonException or FormatException or InvalidOperationException or ArgumentException)
        {
            return new(null, [$"{loaderKind} 目录格式无效：{exception.Message}"]);
        }
    }

    public async Task<MinecraftLoaderDirectoryResult> FetchDirectoryGroupAsync(
        MinecraftLoaderKind loaderKind,
        string groupKey,
        CancellationToken cancellationToken = default)
    {
        if (loaderKind != MinecraftLoaderKind.Forge)
        {
            return new(null, [$"{loaderKind} 目录不需要单独加载分组。"]);
        }

        if (string.IsNullOrWhiteSpace(groupKey) || groupKey.Length > 64)
        {
            return new(null, ["Forge Minecraft 版本号无效。"]);
        }

        try
        {
            var normalizedKey = groupKey.Replace('-', '_');
            var uri = new Uri($"https://bmclapi2.bangbang93.com/forge/minecraft/{Uri.EscapeDataString(normalizedKey)}");
            var response = await TryFetchAsync($"Forge {groupKey}", uri, cancellationToken).ConfigureAwait(false);
            return ParseSingle(response, json => MinecraftLoaderDirectoryParser.ParseForgeVersions(groupKey, json));
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception) when (exception is JsonException or FormatException or InvalidOperationException or ArgumentException)
        {
            return new(null, [$"Forge {groupKey} 目录格式无效：{exception.Message}"]);
        }
    }

    private async Task<MinecraftLoaderDirectoryResult> FetchNeoForgeDirectoryAsync(CancellationToken cancellationToken)
    {
        var releasesTask = TryFetchWithFallbackAsync("NeoForge", NeoForgeMirrorReleasesUri, NeoForgeReleasesUri, cancellationToken);
        var legacyTask = TryFetchWithFallbackAsync("NeoForge 遗留目录", NeoForgeMirrorLegacyUri, NeoForgeLegacyUri, cancellationToken);
        await Task.WhenAll(releasesTask, legacyTask).ConfigureAwait(false);
        var responses = new[] { await releasesTask.ConfigureAwait(false), await legacyTask.ConfigureAwait(false) };
        var errors = responses.Where(response => response.Error is not null).Select(response => response.Error!).ToArray();
        try
        {
            var directory = MinecraftLoaderDirectoryParser.ParseNeoForgeVersions(responses.Select(response => response.Content).ToArray());
            return new(directory, errors);
        }
        catch (FormatException exception)
        {
            return new(null, errors.Append(exception.Message).ToArray());
        }
    }

    private async Task<MinecraftLoaderDirectoryResult> FetchLabyModDirectoryAsync(CancellationToken cancellationToken)
    {
        var productionTask = TryFetchAsync("LabyMod 正式版", LabyModProductionUri, cancellationToken);
        var snapshotTask = TryFetchAsync("LabyMod 快照版", LabyModSnapshotUri, cancellationToken);
        await Task.WhenAll(productionTask, snapshotTask).ConfigureAwait(false);
        var production = await productionTask.ConfigureAwait(false);
        var snapshot = await snapshotTask.ConfigureAwait(false);
        var errors = new[] { production.Error, snapshot.Error }.OfType<string>().ToArray();
        if (production.Content is null || snapshot.Content is null)
        {
            return new(null, errors);
        }

        return new(
            MinecraftLoaderDirectoryParser.ParseLabyModVersions(production.Content, snapshot.Content),
            errors);
    }

    private static MinecraftLoaderDirectoryResult ParseSingle(
        CatalogResponse response,
        Func<string, MinecraftLoaderDirectory> parser)
    {
        if (response.Content is null)
        {
            return new(null, [response.Error ?? "目录响应为空。"]);
        }

        var directory = parser(response.Content);
        return response.Error is null ? new(directory, []) : new(directory, [response.Error]);
    }

    private async Task<CatalogResponse> TryFetchAsync(string sourceName, Uri uri, CancellationToken cancellationToken)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, uri);
            request.Headers.UserAgent.ParseAdd("PCL-Aurora/1.0");
            using var response = await httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
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

    private async Task<CatalogResponse> TryFetchWithFallbackAsync(
        string sourceName,
        Uri primaryUri,
        Uri fallbackUri,
        CancellationToken cancellationToken)
    {
        var primary = await TryFetchAsync(sourceName, primaryUri, cancellationToken).ConfigureAwait(false);
        if (primary.Content is not null)
        {
            return primary;
        }

        var fallback = await TryFetchAsync(sourceName, fallbackUri, cancellationToken).ConfigureAwait(false);
        return fallback.Content is not null
            ? fallback
            : new(null, string.Join("；", new[] { primary.Error, fallback.Error }.Where(error => error is not null)));
    }

    private sealed record CatalogResponse(string? Content, string? Error)
    {
        public static CatalogResponse Empty { get; } = new(null, null);
    }
}
