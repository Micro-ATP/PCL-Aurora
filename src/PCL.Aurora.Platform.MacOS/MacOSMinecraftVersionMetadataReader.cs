using PCL.Aurora.Domain;
using PCL.Aurora.Platform.Abstractions;

namespace PCL.Aurora.Platform.MacOS;

public sealed class MacOSMinecraftVersionMetadataReader : IMinecraftVersionMetadataReader
{
    public async Task<MinecraftVersionMetadataInspection> InspectAsync(
        MinecraftInstance instance,
        CancellationToken cancellationToken = default)
    {
        if (instance.Status != MinecraftInstanceStatus.Valid)
        {
            return new([], null, ["所选实例没有可读取的版本元数据。"]);
        }

        var versionsDirectory = Directory.GetParent(instance.DirectoryPath)?.FullName;
        if (string.IsNullOrWhiteSpace(versionsDirectory))
        {
            return new([], null, ["无法确定实例所在的 versions 目录。"]);
        }

        var chain = new List<MinecraftVersionMetadata>();
        var errors = new List<string>();
        var visited = new HashSet<string>(StringComparer.Ordinal);
        var currentId = instance.Name;

        while (!string.IsNullOrWhiteSpace(currentId))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!visited.Add(currentId))
            {
                errors.Add($"检测到版本继承循环：{currentId}。");
                break;
            }

            if (!IsSafeVersionId(currentId))
            {
                errors.Add($"继承版本名称无效：{currentId}。");
                break;
            }

            var metadataPath = Path.Combine(versionsDirectory, currentId, $"{currentId}.json");
            if (!File.Exists(metadataPath))
            {
                errors.Add($"未找到继承版本元数据：{currentId}。");
                break;
            }

            try
            {
                var json = await File.ReadAllTextAsync(metadataPath, cancellationToken).ConfigureAwait(false);
                var parsed = MinecraftVersionMetadataParser.Parse(json);
                if (parsed.Metadata is null)
                {
                    errors.AddRange(parsed.Errors.Select(error => $"{currentId}：{error}"));
                    break;
                }

                chain.Add(parsed.Metadata);
                errors.AddRange(parsed.Errors.Select(error => $"{currentId}：{error}"));
                currentId = parsed.Metadata.InheritsFrom ?? string.Empty;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                errors.Add($"无法读取版本元数据 {currentId}：{exception.Message}");
                break;
            }
        }

        var resolved = MinecraftVersionMetadataResolver.Resolve(chain);
        errors.AddRange(resolved.Errors);
        return new(resolved.InheritanceChain, resolved.EffectiveMetadata, errors);
    }

    private static bool IsSafeVersionId(string versionId) =>
        versionId == Path.GetFileName(versionId) &&
        !versionId.Contains(Path.DirectorySeparatorChar) &&
        !versionId.Contains(Path.AltDirectorySeparatorChar);
}
