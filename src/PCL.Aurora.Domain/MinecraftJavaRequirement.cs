namespace PCL.Aurora.Domain;

public sealed record MinecraftJavaRequirement(
    int? MinimumMajorVersion,
    int? MaximumMajorVersion,
    string? RecommendedComponent,
    string Source)
{
    public IReadOnlyList<string> GetBlockingReasons(JavaInstallation? java)
    {
        if (java is null || !java.IsCompatible)
        {
            return ["未找到兼容的 Java。"];
        }

        if (java.MajorVersion is not { } majorVersion)
        {
            return ["无法识别所选 Java 的主版本，无法确认其是否满足 Minecraft 版本要求。"];
        }

        if (MinimumMajorVersion is { } minimum && majorVersion < minimum)
        {
            return [$"所选 Java {majorVersion} 低于该 Minecraft 版本要求的 Java {minimum}。"];
        }

        if (MaximumMajorVersion is { } maximum && majorVersion > maximum)
        {
            return [$"所选 Java {majorVersion} 高于该 Minecraft 版本允许的最高 Java {maximum}。"];
        }

        return [];
    }
}
