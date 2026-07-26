namespace PCL.Aurora.Application;

/// <summary>
/// 一次下载批次的真实传输状态。不计算速度或百分比，避免在重试、未知长度时制造误导性指标。
/// </summary>
public sealed record MinecraftDownloadProgress(
    int CompletedArtifacts,
    int TotalArtifacts,
    int ActiveArtifacts,
    long DownloadedBytes,
    long? TotalBytes,
    string CurrentDescription);
