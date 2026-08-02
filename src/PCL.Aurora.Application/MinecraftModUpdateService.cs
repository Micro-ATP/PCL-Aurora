using System.Net.Http.Headers;
using System.Security.Cryptography;
using PCL.Aurora.Domain;

namespace PCL.Aurora.Application;

public sealed class MinecraftModUpdateService(
    HttpClient httpClient,
    IMinecraftInstanceManagementService instanceManagementService,
    ICommunityResourceVersionService versionService,
    IMinecraftDownloadExecutor downloadExecutor) : IMinecraftModUpdateService
{
    private static readonly Uri ModrinthApiRoot = new("https://api.modrinth.com/v2/");
    private const int MaximumMods = 512;

    public async Task<MinecraftModUpdateCheckResult> CheckAsync(
        MinecraftInstance instance,
        MinecraftInstanceIsolationMode isolationMode,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(instance);
        var mods = (await instanceManagementService.GetContentAsync(
                instance,
                isolationMode,
                MinecraftInstanceContentKind.Mod,
                cancellationToken)
            .ConfigureAwait(false))
            .Where(item => !item.IsDirectory &&
                           (item.RelativePath.EndsWith(".jar", StringComparison.OrdinalIgnoreCase) ||
                            item.RelativePath.EndsWith(".jar.disabled", StringComparison.OrdinalIgnoreCase)))
            .Take(MaximumMods + 1)
            .ToArray();
        if (mods.Length > MaximumMods)
        {
            throw new InvalidOperationException($"当前实例超过 {MaximumMods} 个 Mod，请缩小范围后再检查更新。");
        }

        var gameVersion = instance.BaseVersionId ?? instance.VersionId
                          ?? throw new InvalidOperationException("无法确定实例的 Minecraft 版本。");
        var loader = GetLoader(instance);
        var updates = new List<MinecraftModUpdateCandidate>();
        var errors = new List<string>();
        var recognized = 0;
        foreach (var mod in mods)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                var current = await IdentifyAsync(mod.FullPath, cancellationToken).ConfigureAwait(false);
                if (current is null)
                {
                    continue;
                }
                recognized++;
                var catalog = await versionService.GetProjectVersionsAsync(
                    current.ProjectId,
                    gameVersion,
                    loader,
                    cancellationToken).ConfigureAwait(false);
                var latest = catalog.Versions
                    .Where(version => version.PrimaryFile is not null)
                    .OrderByDescending(version => version.PublishedAt)
                    .FirstOrDefault();
                if (latest is not null && !string.Equals(latest.Id, current.Id, StringComparison.OrdinalIgnoreCase) &&
                    (current.PublishedAt is null || latest.PublishedAt is null || latest.PublishedAt > current.PublishedAt))
                {
                    updates.Add(new(mod, current, latest));
                }
                errors.AddRange(catalog.Errors.Select(error => $"{mod.Name}：{error}"));
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception exception) when (exception is IOException or HttpRequestException or InvalidDataException)
            {
                errors.Add($"{mod.Name}：{exception.Message}");
            }
        }

        return new(updates, recognized, mods.Length - recognized, errors);
    }

    public async Task<MinecraftModUpdateApplyResult> ApplyAsync(
        MinecraftInstance instance,
        MinecraftInstanceIsolationMode isolationMode,
        IReadOnlyList<MinecraftModUpdateCandidate> updates,
        IProgress<MinecraftDownloadProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(updates);
        var modDirectory = instanceManagementService.GetContentDirectory(
            instance,
            isolationMode,
            MinecraftInstanceContentKind.Mod);
        Directory.CreateDirectory(modDirectory);
        var updatedFiles = new List<string>();
        foreach (var update in updates)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var source = Path.GetFullPath(update.LocalMod.FullPath);
            EnsureDirectChild(modDirectory, source);
            if (!File.Exists(source))
            {
                throw new FileNotFoundException($"本地 Mod 已不存在：{update.LocalMod.Name}", source);
            }
            var file = update.LatestVersion.PrimaryFile
                       ?? throw new InvalidOperationException($"{update.LatestVersion.Name} 没有可下载文件。");
            var disabledSuffix = update.LocalMod.IsEnabled ? string.Empty : ".disabled";
            var destinationName = file.FileName + disabledSuffix;
            if (destinationName != Path.GetFileName(destinationName))
            {
                throw new InvalidDataException("更新文件名不安全。");
            }
            var destination = Path.Combine(modDirectory, destinationName);
            if (!string.Equals(source, destination, StringComparison.Ordinal) && File.Exists(destination))
            {
                throw new IOException($"目标文件 {destinationName} 已存在，不会覆盖。");
            }

            var stagingName = $".pcl-aurora-mod-update-{Guid.NewGuid():N}.partial";
            var staging = Path.Combine(modDirectory, stagingName);
            var backup = source + $".{Guid.NewGuid():N}.backup";
            try
            {
                await downloadExecutor.ExecuteAsync(
                    new MinecraftDownloadPlan(
                        update.LatestVersion.Id,
                        [new(update.LatestVersion.Name, stagingName, file.Url, file.Sha1, file.Size)],
                        []),
                    modDirectory,
                    progress,
                    cancellationToken).ConfigureAwait(false);
                File.Move(source, backup);
                try
                {
                    File.Move(staging, destination);
                    File.Delete(backup);
                }
                catch
                {
                    if (File.Exists(destination))
                    {
                        File.Delete(destination);
                    }
                    File.Move(backup, source);
                    throw;
                }
                updatedFiles.Add(destinationName);
            }
            catch
            {
                TryDelete(staging);
                if (File.Exists(backup) && !File.Exists(source))
                {
                    File.Move(backup, source);
                }
                throw;
            }
        }
        return new(updatedFiles.Count, updatedFiles);
    }

    private async Task<CommunityResourceVersion?> IdentifyAsync(
        string path,
        CancellationToken cancellationToken)
    {
        await using var stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            81920,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        var hash = Convert.ToHexString(await SHA1.HashDataAsync(stream, cancellationToken).ConfigureAwait(false))
            .ToLowerInvariant();
        var endpoint = new Uri(ModrinthApiRoot, $"version_file/{hash}?algorithm=sha1");
        using var request = new HttpRequestMessage(HttpMethod.Get, endpoint);
        request.Headers.UserAgent.Add(new ProductInfoHeaderValue("PCL-Aurora", "0.1"));
        request.Headers.Accept.ParseAdd("application/json");
        using var response = await httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }
        response.EnsureSuccessStatusCode();
        var catalog = ModrinthCommunityResourceVersionParser.ParseSingle(
            await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false));
        return catalog.Versions.SingleOrDefault();
    }

    private static CommunityResourceLoader GetLoader(MinecraftInstance instance) => instance.InstalledLoader?.Kind switch
    {
        MinecraftLoaderKind.Forge => CommunityResourceLoader.Forge,
        MinecraftLoaderKind.NeoForge => CommunityResourceLoader.NeoForge,
        MinecraftLoaderKind.Fabric => CommunityResourceLoader.Fabric,
        _ => CommunityResourceLoader.Any,
    };

    private static void EnsureDirectChild(string root, string path)
    {
        var parent = Directory.GetParent(path)?.FullName;
        if (parent is null || !string.Equals(
                Path.TrimEndingDirectorySeparator(Path.GetFullPath(parent)),
                Path.TrimEndingDirectorySeparator(Path.GetFullPath(root)),
                StringComparison.Ordinal))
        {
            throw new InvalidDataException("Mod 路径超出实例目录。");
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
