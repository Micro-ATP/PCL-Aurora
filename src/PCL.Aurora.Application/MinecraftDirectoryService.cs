using PCL.Aurora.Platform.Abstractions;

namespace PCL.Aurora.Application;

/// <summary>
/// 仅打开已存在的 Minecraft 根目录，不创建或修改游戏文件。
/// </summary>
public sealed class MinecraftDirectoryService(
    IMinecraftRootDirectoryProvider rootDirectoryProvider,
    IOpenPathService openPathService) : IMinecraftDirectoryService
{
    public string GetRootDirectory() => Path.GetFullPath(rootDirectoryProvider.GetRootDirectory());

    public Task OpenRootDirectoryAsync(CancellationToken cancellationToken = default)
    {
        var rootDirectory = GetRootDirectory();
        if (!Directory.Exists(rootDirectory))
        {
            throw new DirectoryNotFoundException($"Minecraft 游戏目录尚不存在：{rootDirectory}");
        }

        return openPathService.OpenFolderAsync(rootDirectory, cancellationToken);
    }
}
