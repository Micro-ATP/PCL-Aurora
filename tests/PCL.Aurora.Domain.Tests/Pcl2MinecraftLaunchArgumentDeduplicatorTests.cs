using PCL.Aurora.Domain;

namespace PCL.Aurora.Domain.Tests;

public sealed class Pcl2MinecraftLaunchArgumentDeduplicatorTests
{
    [Fact]
    public void Deduplicate_GameArgumentsLetsLaterKeyValuePairOverrideEarlierValue()
    {
        var result = Pcl2MinecraftLaunchArgumentDeduplicator.Deduplicate(
            ["--width", "854", "--username", "Aurora", "--width", "1280"],
            isJvmArgument: false);

        Assert.Equal(["--width", "1280", "--username", "Aurora"], result);
    }

    [Fact]
    public void Deduplicate_GameArgumentsKeepsMultipleTweakClasses()
    {
        var result = Pcl2MinecraftLaunchArgumentDeduplicator.Deduplicate(
            ["--tweakClass", "first.Tweaker", "--tweakClass", "second.Tweaker"],
            isJvmArgument: false);

        Assert.Equal(
            ["--tweakClass", "first.Tweaker", "--tweakClass", "second.Tweaker"],
            result);
    }

    [Fact]
    public void Deduplicate_JvmArgumentsKeepsDistinctSingleArgumentsAndDropsExactDuplicates()
    {
        var result = Pcl2MinecraftLaunchArgumentDeduplicator.Deduplicate(
            ["-Xmx2G", "-Xmx4G", "-Xmx2G"],
            isJvmArgument: true);

        Assert.Equal(["-Xmx2G", "-Xmx4G"], result);
    }
}
