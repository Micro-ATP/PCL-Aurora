using PCL.Aurora.Domain;

namespace PCL.Aurora.Application;

public sealed class MinecraftLoaderCatalogService : IMinecraftLoaderCatalogService
{
    public async Task<MinecraftLoaderCatalogParseResult> ReadAsync(
        string catalogPath,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(catalogPath))
        {
            return new(null, ["请提供本地加载器目录 JSON 文件路径。"]);
        }

        var fullPath = Path.GetFullPath(catalogPath);
        if (!File.Exists(fullPath))
        {
            return new(null, [$"未找到加载器目录文件：{fullPath}"]);
        }

        try
        {
            var json = await File.ReadAllTextAsync(fullPath, cancellationToken).ConfigureAwait(false);
            return MinecraftLoaderCatalogParser.Parse(json);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return new(null, [$"无法读取加载器目录文件：{exception.Message}"]);
        }
    }
}
