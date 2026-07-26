namespace PCL.Aurora.Application;

/// <summary>
/// 提供当前 Minecraft 根目录的显式系统文件管理器操作。
/// </summary>
public interface IMinecraftDirectoryService
{
    string GetRootDirectory();

    Task OpenRootDirectoryAsync(CancellationToken cancellationToken = default);
}
