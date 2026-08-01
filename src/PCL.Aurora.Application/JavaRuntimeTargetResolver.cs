using PCL.Aurora.Domain;

namespace PCL.Aurora.Application;

public sealed record JavaRuntimeTarget(int MajorVersion, Version? MinimumVersion, Version? MaximumVersion)
{
    public bool Accepts(Version version) =>
        version.Major == MajorVersion &&
        (MinimumVersion is null || version >= MinimumVersion) &&
        (MaximumVersion is null || version <= MaximumVersion);
}

public static class JavaRuntimeTargetResolver
{
    private static readonly IReadOnlyDictionary<string, int> ComponentVersions =
        new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            ["jre-legacy"] = 8,
            ["java-runtime-alpha"] = 16,
            ["java-runtime-beta"] = 17,
            ["java-runtime-gamma"] = 17,
            ["java-runtime-gamma-snapshot"] = 17,
            ["java-runtime-delta"] = 21,
            ["java-runtime-epsilon"] = 25,
        };

    public static JavaRuntimeTarget Resolve(MinecraftJavaRequirement? requirement)
    {
        if (requirement?.RecommendedComponent is { } component &&
            ComponentVersions.TryGetValue(component, out var componentVersion) &&
            Satisfies(requirement, componentVersion))
        {
            return new(componentVersion, requirement.MinimumVersion, requirement.MaximumVersion);
        }

        if (requirement?.MinimumMajorVersion is { } minimum)
        {
            return new(minimum, requirement.MinimumVersion, requirement.MaximumVersion);
        }

        if (requirement?.MaximumMajorVersion is { } maximum)
        {
            return new(maximum, requirement.MinimumVersion, requirement.MaximumVersion);
        }

        return new(21, null, null);
    }

    private static bool Satisfies(MinecraftJavaRequirement requirement, int majorVersion) =>
        (requirement.MinimumMajorVersion is null || majorVersion >= requirement.MinimumMajorVersion) &&
        (requirement.MaximumMajorVersion is null || majorVersion <= requirement.MaximumMajorVersion);
}
