using PCL.Aurora.Domain;
using PCL.Aurora.Platform.Abstractions;

namespace PCL.Aurora.Application;

public sealed class MinecraftVersionProvisioningService(
    HttpClient httpClient,
    IMinecraftRootDirectoryProvider rootDirectoryProvider) : IMinecraftVersionProvisioningService
{
    public Task<MinecraftInstance> ProvisionAsync(
        MinecraftVersionCatalogEntry version,
        CancellationToken cancellationToken = default) =>
        ProvisionAsync(version, rootDirectoryProvider.GetRootDirectory(), cancellationToken);

    public async Task<MinecraftInstance> ProvisionAsync(
        MinecraftVersionCatalogEntry version,
        string minecraftRootDirectory,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(version);
        ArgumentException.ThrowIfNullOrWhiteSpace(minecraftRootDirectory);
        if (!IsSafeVersionId(version.Id) || version.MetadataUrl.Scheme != Uri.UriSchemeHttps)
        {
            throw new InvalidOperationException("所选版本元数据无效。 ");
        }

        var rootDirectory = Path.GetFullPath(minecraftRootDirectory);
        var versionsDirectory = Path.Combine(rootDirectory, "versions");
        var instanceDirectory = Path.Combine(versionsDirectory, version.Id);
        if (Directory.Exists(instanceDirectory))
        {
            throw new InvalidOperationException($"本地实例 {version.Id} 已存在，不会覆盖。 ");
        }

        Directory.CreateDirectory(versionsDirectory);
        var temporaryPath = Path.Combine(versionsDirectory, $".{version.Id}.{Guid.NewGuid():N}.json.partial");
        try
        {
            using var response = await httpClient.GetAsync(version.MetadataUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            var json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            var parsed = MinecraftVersionMetadataParser.Parse(json);
            if (!parsed.IsSuccess || parsed.Metadata is null || !string.Equals(parsed.Metadata.Id, version.Id, StringComparison.Ordinal))
            {
                throw new InvalidDataException("下载的版本元数据无效或版本 ID 不匹配。 ");
            }

            await File.WriteAllTextAsync(temporaryPath, json, cancellationToken).ConfigureAwait(false);
            Directory.CreateDirectory(instanceDirectory);
            File.Move(temporaryPath, Path.Combine(instanceDirectory, $"{version.Id}.json"));
            return new(version.Id, instanceDirectory, version.Id, version.Type, version.ReleaseTime, MinecraftInstanceStatus.Valid);
        }
        catch
        {
            TryDelete(temporaryPath);
            if (Directory.Exists(instanceDirectory) && !Directory.EnumerateFileSystemEntries(instanceDirectory).Any())
            {
                Directory.Delete(instanceDirectory);
            }

            throw;
        }
    }

    private static bool IsSafeVersionId(string id) =>
        !string.IsNullOrWhiteSpace(id) && id == Path.GetFileName(id) &&
        !id.Contains(Path.DirectorySeparatorChar) && !id.Contains(Path.AltDirectorySeparatorChar);

    private static void TryDelete(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); }
        catch (IOException) { }
    }
}
