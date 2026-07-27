namespace PCL.Aurora.Domain;

/// <summary>
/// Minecraft 版本元数据给出的推荐 Java 运行时版本。
/// </summary>
public sealed record MinecraftJavaVersionRequirement(int MajorVersion, string? Component)
{
    public bool IsValid => MajorVersion is >= 1 and <= 100;
}
