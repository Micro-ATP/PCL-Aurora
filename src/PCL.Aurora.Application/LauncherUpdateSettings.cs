namespace PCL.Aurora.Application;

public enum LauncherUpdateChannel
{
    Release = 0,
    Beta = 1,
}

public enum LauncherAutoUpdateBehavior
{
    DownloadAndInstall = 0,
    DownloadAndNotify = 1,
    NotifyOnly = 2,
    Disabled = 3,
}

public sealed record LauncherUpdateSettings(
    LauncherUpdateChannel Channel = LauncherUpdateChannel.Release,
    LauncherAutoUpdateBehavior AutoUpdateBehavior = LauncherAutoUpdateBehavior.DownloadAndNotify)
{
    public static LauncherUpdateSettings Default { get; } = new();

    public bool IsValid =>
        Enum.IsDefined(Channel) &&
        Enum.IsDefined(AutoUpdateBehavior);
}
