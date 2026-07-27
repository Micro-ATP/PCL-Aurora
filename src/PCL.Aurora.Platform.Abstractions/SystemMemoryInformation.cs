namespace PCL.Aurora.Platform.Abstractions;

/// <summary>
/// 当前平台可提供的物理内存信息；未知值使用 null 表示。
/// </summary>
public sealed record SystemMemoryInformation(long? TotalBytes, long? AvailableBytes)
{
    public bool IsUsable => TotalBytes is > 0 && AvailableBytes is > 0;
}
