using PCL.Aurora.Domain;

namespace PCL.Aurora.Domain.Tests;

public sealed class MinecraftNativeLibraryPlanBuilderTests : IDisposable
{
    private readonly string rootDirectory = Path.Combine(Path.GetTempPath(), $"pcl-aurora-native-plan-{Guid.NewGuid():N}");

    [Fact]
    public async Task Build_SelectsMacOSArm64ClassifierAndLocalArchive()
    {
        var archivePath = Path.Combine(rootDirectory, "libraries", "org", "example", "native", "1.0", "native-1.0-natives-macos-arm64.jar");
        Directory.CreateDirectory(Path.GetDirectoryName(archivePath)!);
        await File.WriteAllTextAsync(archivePath, "archive");
        var metadata = CreateMetadata("natives-macos-${arch}", "natives-macos-arm64");
        var inspection = new MinecraftVersionMetadataInspection([metadata], metadata, []);

        var plan = MinecraftNativeLibraryPlanBuilder.Build(
            inspection,
            rootDirectory,
            Path.Combine(rootDirectory, "versions", "1.21.4", "natives"),
            JavaArchitecture.Arm64);

        Assert.True(plan.IsReady);
        var archive = Assert.Single(plan.Archives);
        Assert.Equal("natives-macos-arm64", archive.Classifier);
        Assert.Equal(archivePath, archive.LocalPath);
    }

    [Fact]
    public void Build_BlocksMissingSelectedClassifier()
    {
        var metadata = CreateMetadata("natives-macos-${arch}", "natives-macos");
        var inspection = new MinecraftVersionMetadataInspection([metadata], metadata, []);

        var plan = MinecraftNativeLibraryPlanBuilder.Build(
            inspection,
            rootDirectory,
            Path.Combine(rootDirectory, "versions", "1.21.4", "natives"),
            JavaArchitecture.Arm64);

        Assert.False(plan.IsReady);
        Assert.Contains(plan.BlockingReasons, reason => reason.Contains("natives-macos-arm64", StringComparison.Ordinal));
    }

    [Fact]
    public void Build_SkipsConditionalNativeLibraryThatDoesNotMatchMacOS()
    {
        var metadata = CreateMetadata("natives-macos", "natives-macos") with
        {
            Libraries = [new MinecraftVersionLibrary(
                "org.example:native:1.0",
                null,
                null,
                HasConditionalRules: true,
                NativeClassifiers: new Dictionary<string, string> { ["macos"] = "natives-macos" },
                Classifiers: new Dictionary<string, MinecraftVersionLibraryClassifier>
                {
                    ["natives-macos"] = new(
                        "org/example/native/1.0/native-macos.jar",
                        new MinecraftVersionDownload(new Uri("https://example.invalid/native.jar"), null, null)),
                },
                Rules: [new(MinecraftLaunchRuleAction.Allow, new("windows", null, null), null)])],
        };
        var inspection = new MinecraftVersionMetadataInspection([metadata], metadata, []);

        var plan = MinecraftNativeLibraryPlanBuilder.Build(
            inspection,
            rootDirectory,
            Path.Combine(rootDirectory, "versions", "1.21.4", "natives"),
            JavaArchitecture.Arm64,
            new MinecraftLaunchRuleEnvironment("osx", "15.7.7", "arm64"));

        Assert.True(plan.IsReady);
        Assert.Empty(plan.Archives);
        Assert.Empty(plan.MissingFiles);
    }

    private static MinecraftVersionMetadata CreateMetadata(string pattern, string classifier) =>
        new(
            "1.21.4",
            null,
            "release",
            null,
            null,
            null,
            null,
            [new MinecraftVersionLibrary(
                "org.example:native:1.0",
                null,
                null,
                HasConditionalRules: false,
                NativeClassifiers: new Dictionary<string, string> { ["osx"] = pattern },
                Classifiers: new Dictionary<string, MinecraftVersionLibraryClassifier>
                {
                    [classifier] = new(
                        $"org/example/native/1.0/native-1.0-{classifier}.jar",
                        new MinecraftVersionDownload(new Uri("https://example.invalid/native.jar"), null, null)),
                })]);

    public void Dispose()
    {
        if (Directory.Exists(rootDirectory))
        {
            Directory.Delete(rootDirectory, recursive: true);
        }
    }
}
