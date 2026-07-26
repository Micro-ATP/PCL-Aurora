using PCL.Aurora.Domain;
using PCL.Aurora.Platform.Abstractions;

namespace PCL.Aurora.Platform.MacOS;

public sealed class MacOSMinecraftAssetIndexReader : IMinecraftAssetIndexReader
{
    public async Task<MinecraftAssetIndexParseResult> ReadAsync(
        MinecraftInstance instance,
        string assetIndexId,
        CancellationToken cancellationToken = default)
    {
        if (instance.Status != MinecraftInstanceStatus.Valid)
        {
            return new(null, ["所选实例没有可读取的版本元数据。"]);
        }

        if (!IsSafeAssetIndexId(assetIndexId))
        {
            return new(null, ["资源索引名称无效。"]);
        }

        var versionsDirectory = Directory.GetParent(instance.DirectoryPath)?.FullName;
        var minecraftRootDirectory = versionsDirectory is null
            ? null
            : Directory.GetParent(versionsDirectory)?.FullName;
        if (string.IsNullOrWhiteSpace(minecraftRootDirectory))
        {
            return new(null, ["无法确定 Minecraft 根目录。"]);
        }

        var indexPath = Path.Combine(minecraftRootDirectory, "assets", "indexes", $"{assetIndexId}.json");
        if (!File.Exists(indexPath))
        {
            return new(null, [$"未找到本地资源索引：{assetIndexId}。"]);
        }

        try
        {
            return MinecraftAssetIndexParser.Parse(
                assetIndexId,
                await File.ReadAllTextAsync(indexPath, cancellationToken).ConfigureAwait(false));
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return new(null, [$"无法读取资源索引 {assetIndexId}：{exception.Message}"]);
        }
    }

    private static bool IsSafeAssetIndexId(string value) =>
        !string.IsNullOrWhiteSpace(value) &&
        value == Path.GetFileName(value) &&
        !value.Contains(Path.DirectorySeparatorChar) &&
        !value.Contains(Path.AltDirectorySeparatorChar);
}
