namespace PCL.Aurora.Application;

public sealed record LauncherLogFile(
    string Name,
    string FullPath,
    DateTimeOffset ModifiedAt,
    long Length,
    bool IsCurrent);
