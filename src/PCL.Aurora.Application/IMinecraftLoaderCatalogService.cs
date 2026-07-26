using PCL.Aurora.Domain;

namespace PCL.Aurora.Application;

/// <summary>
/// 仅读取用户明确指定的本地加载器目录；不访问网络、不下载也不执行安装器。
/// </summary>
public interface IMinecraftLoaderCatalogService
{
    Task<MinecraftLoaderCatalogParseResult> ReadAsync(
        string catalogPath,
        CancellationToken cancellationToken = default);
}
