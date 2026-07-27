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

    [Fact]
    public void Evaluate_Forge34To36RequiresJava8UpdateAtMost320()
    {
        var requirement = Pcl2MinecraftJavaRequirementEvaluator.Evaluate(
            CreateMetadata(),
            CreateInstance(
                "1.16.5",
                new MinecraftInstalledLoader(MinecraftLoaderKind.Forge, "36.2.25", "1.16.5")));

        Assert.Equal(8, requirement.MaximumMajorVersion);
        Assert.Equal(new Version(8, 0, 320), requirement.MaximumVersion);
        Assert.Empty(requirement.GetBlockingReasons(CreateJava("8u320", 8)));
        Assert.Contains(
            requirement.GetBlockingReasons(CreateJava("8u321", 8)),
            reason => reason.Contains("8u320", StringComparison.Ordinal));
    }

    [Fact]
    public void Evaluate_ForgeAndOptiFineOn118CapsJavaAt18()
    {
        var requirement = Pcl2MinecraftJavaRequirementEvaluator.Evaluate(
            CreateMetadata(),
            CreateInstance(
                "1.18.2",
                new MinecraftInstalledLoader(MinecraftLoaderKind.Forge, "40.2.0", "1.18.2"),
                hasOptiFine: true));

        Assert.Equal(18, requirement.MaximumMajorVersion);
        Assert.Contains(
            requirement.GetBlockingReasons(CreateJava("19.0.2", 19)),
            reason => reason.Contains("高于", StringComparison.Ordinal));
    }

    [Fact]
    public void Evaluate_NeoForgeEarlyVersionAndFabricApplyPcl2Ranges()
    {
        var neoForgeRequirement = Pcl2MinecraftJavaRequirementEvaluator.Evaluate(
            CreateMetadata(),
            CreateInstance(
                "1.20.2",
                new MinecraftInstalledLoader(MinecraftLoaderKind.NeoForge, "20.2.62-beta", null)));
        var fabricRequirement = Pcl2MinecraftJavaRequirementEvaluator.Evaluate(
            CreateMetadata(),
            CreateInstance(
                "1.18.2",
                new MinecraftInstalledLoader(MinecraftLoaderKind.Fabric, "0.14.25", null)));

        Assert.Equal(21, neoForgeRequirement.MaximumMajorVersion);
        Assert.Equal(17, fabricRequirement.MinimumMajorVersion);
    }

    private static MinecraftVersionMetadata CreateMetadata() =>
        new("derived", null, "release", null, null, null);

    private static MinecraftInstance CreateInstance(
        string baseVersion,
        MinecraftInstalledLoader loader,
        bool hasOptiFine = false) =>
        new(
            $"{baseVersion}-{loader.Kind}",
            "/minecraft/versions/example",
            "derived",
            "release",
            null,
            MinecraftInstanceStatus.Valid,
            BaseVersionId: baseVersion,
            InstalledLoader: loader,
            HasOptiFine: hasOptiFine);

    private static JavaInstallation CreateJava(string version, int majorVersion) =>
        new("/java", version, majorVersion, "Temurin", JavaArchitecture.Arm64, JavaSource.Path, true);
}
