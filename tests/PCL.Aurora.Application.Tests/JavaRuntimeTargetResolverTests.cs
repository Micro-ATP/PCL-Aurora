using PCL.Aurora.Domain;

namespace PCL.Aurora.Application.Tests;

public sealed class JavaRuntimeTargetResolverTests
{
    [Fact]
    public void Resolve_PrefersDeclaredMojangComponent()
    {
        var requirement = new MinecraftJavaRequirement(17, null, "java-runtime-gamma", "test");

        Assert.Equal(17, JavaRuntimeTargetResolver.Resolve(requirement).MajorVersion);
    }

    [Fact]
    public void Resolve_UsesMinimumWhenComponentConflictsWithRange()
    {
        var requirement = new MinecraftJavaRequirement(21, null, "java-runtime-gamma", "test");

        Assert.Equal(21, JavaRuntimeTargetResolver.Resolve(requirement).MajorVersion);
    }

    [Fact]
    public void Resolve_UsesMaximumForLegacyVersions()
    {
        var requirement = new MinecraftJavaRequirement(null, 8, null, "test");

        Assert.Equal(8, JavaRuntimeTargetResolver.Resolve(requirement).MajorVersion);
    }

    [Fact]
    public void Resolve_DefaultsToCurrentMinecraftRuntime()
    {
        Assert.Equal(21, JavaRuntimeTargetResolver.Resolve(null).MajorVersion);
    }

    [Fact]
    public void Resolve_PreservesLegacyUpdateUpperBound()
    {
        var requirement = new MinecraftJavaRequirement(
            8,
            8,
            "jre-legacy",
            "test",
            MaximumVersion: new Version(8, 0, 320));

        var target = JavaRuntimeTargetResolver.Resolve(requirement);

        Assert.True(target.Accepts(new Version(8, 0, 312)));
        Assert.False(target.Accepts(new Version(8, 0, 321)));
    }
}
