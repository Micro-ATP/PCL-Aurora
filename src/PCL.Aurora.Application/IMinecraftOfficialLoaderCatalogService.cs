using PCL.Aurora.Domain;

namespace PCL.Aurora.Application;

/// <summary>
/// 仅在用户明确请求时读取 Forge、NeoForge、Fabric 官方目录与 PCL 上游使用的 OptiFine 公开目录。
/// 不下载游戏文件，也不执行任何安装器。
/// </summary>
public interface IMinecraftOfficialLoaderCatalogService
{
    Task<MinecraftLoaderCatalogParseResult> FetchAsync(
        string minecraftVersion,
        MinecraftLoaderKind? loaderKind = null,
        CancellationToken cancellationToken = default);
}
