using PCL.Aurora.Domain;

namespace PCL.Aurora.Domain.Tests;

public sealed class Pcl2MinecraftJavaRequirementEvaluatorTests
{
    [Fact]
    public void Evaluate_PrefersDeclaredJavaVersionOverReleaseDateFallback()
    {
        var metadata = new MinecraftVersionMetadata(
            "1.21.4",
            null,
            "release",
            new DateTimeOffset(2024, 12, 3, 0, 0, 0, TimeSpan.Zero),
            null,
            null,
            JavaVersionRequirement: new MinecraftJavaVersionRequirement(21, "java-runtime-gamma"));

        var requirement = Pcl2MinecraftJavaRequirementEvaluator.Evaluate(metadata);

        Assert.Equal(21, requirement.MinimumMajorVersion);
        Assert.Equal("java-runtime-gamma", requirement.RecommendedComponent);
        Assert.Contains("javaVersion", requirement.Source, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("2024-04-02T00:00:00Z", 21)]
    [InlineData("2021-11-16T00:00:00Z", 17)]
    [InlineData("2021-05-11T00:00:00Z", 16)]
    [InlineData("2017-06-07T00:00:00Z", 8)]
    public void Evaluate_UsesPcl2ReleaseDateFallback(string releaseTime, int expectedMinimum)
    {
        var metadata = new MinecraftVersionMetadata(
            "example",
            null,
            "release",
            DateTimeOffset.Parse(releaseTime, System.Globalization.CultureInfo.InvariantCulture),
            null,
            null);

        var requirement = Pcl2MinecraftJavaRequirementEvaluator.Evaluate(metadata);

        Assert.Equal(expectedMinimum, requirement.MinimumMajorVersion);
    }

    [Fact]
    public void GetBlockingReasons_RejectsJavaBelowRequirement()
    {
        var requirement = new MinecraftJavaRequirement(21, null, null, "test");
        var java = new JavaInstallation("/java", "17.0.10", 17, "Temurin", JavaArchitecture.Arm64, JavaSource.Path, true);

        var reasons = requirement.GetBlockingReasons(java);

        Assert.Contains(reasons, reason => reason.Contains("低于", StringComparison.Ordinal));
    }

    [Fact]
    public void Evaluate_OldReleaseUsesJava8MaximumAndRejectsNewerJava()
    {
        var metadata = new MinecraftVersionMetadata(
            "1.5.2",
            null,
            "release",
            new DateTimeOffset(2013, 5, 1, 0, 0, 0, TimeSpan.Zero),
            null,
            null);
        var java = new JavaInstallation("/java", "17", 17, "Temurin", JavaArchitecture.X64, JavaSource.Path, true);

        var requirement = Pcl2MinecraftJavaRequirementEvaluator.Evaluate(metadata);
        var reasons = requirement.GetBlockingReasons(java);

        Assert.Equal(8, requirement.MaximumMajorVersion);
        Assert.Contains(reasons, reason => reason.Contains("高于", StringComparison.Ordinal));
    }
}
