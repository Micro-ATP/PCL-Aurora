namespace PCL.Aurora.Domain;

public sealed record MinecraftJavaRequirement(
    int? MinimumMajorVersion,
    int? MaximumMajorVersion,
    string? RecommendedComponent,
    string Source,
    Version? MinimumVersion = null,
    Version? MaximumVersion = null)
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

        var parsedVersion = java.ParsedVersion;
        if ((MinimumVersion is not null || MaximumVersion is not null) && parsedVersion is null)
        {
            return ["无法识别所选 Java 的完整版本，无法确认其是否满足加载器的更新号要求。"];
        }

        if (MinimumVersion is { } minimumVersion && parsedVersion < minimumVersion)
        {
            return [$"所选 Java {java.Version} 低于该 Minecraft 版本要求的 Java {FormatVersion(minimumVersion)}。"];
        }

        if (MaximumVersion is { } maximumVersion && parsedVersion > maximumVersion)
        {
            return [$"所选 Java {java.Version} 高于该 Minecraft 版本允许的最高 Java {FormatVersion(maximumVersion)}。"];
        }

        return [];
    }

    private static string FormatVersion(Version version) =>
        version.Minor == 0 && version.Build > 0
            ? $"{version.Major}u{version.Build}"
            : version.ToString();
}
