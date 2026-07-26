using PCL.Aurora.Application;
using PCL.Aurora.Domain;
using PCL.Aurora.Platform.Abstractions;

namespace PCL.Aurora.Application.Tests;

public sealed class MinecraftVersionPreparationServiceTests
{
    [Fact]
    public async Task PrepareAsync_GeneratesAReadOnlyDownloadPlan()
    {
        var instance = new MinecraftInstance("1.21.4", "/minecraft/versions/1.21.4", "1.21.4", "release", null, MinecraftInstanceStatus.Valid);
        var metadata = new MinecraftVersionMetadata(
            "1.21.4",
            null,
            "release",
            null,
            new MinecraftVersionDownload(new Uri("https://example.invalid/client.jar"), "client-sha", 123),
            new MinecraftVersionAssetIndex("17", new Uri("https://example.invalid/assets.json"), "assets-sha", 456),
            null,
            [new MinecraftVersionLibrary(
                "org.example:mac-only:1.0",
                "org/example/mac-only/1.0/mac-only.jar",
                new MinecraftVersionDownload(new Uri("https://example.invalid/mac-only.jar"), "mac-only-sha", 20),
                HasConditionalRules: true,
                Rules: [new(MinecraftLaunchRuleAction.Allow, new("osx", null, "arm64"), null)])]);
        var inspection = new MinecraftVersionMetadataInspection([metadata], metadata, []);
        var service = new MinecraftVersionPreparationService(new FakeMetadataReader(inspection), new FakePlatformInfo());

        var preparation = await service.PrepareAsync(instance);

        Assert.True(preparation.Inspection.IsSuccess);
        Assert.True(preparation.DownloadPlan.IsReady);
        Assert.Equal(
            ["versions/1.21.4/1.21.4.jar", "assets/indexes/17.json", "libraries/org/example/mac-only/1.0/mac-only.jar"],
            preparation.DownloadPlan.Artifacts.Select(item => item.RelativePath));
        Assert.Equal("osx", preparation.RuleEnvironment!.OperatingSystemName);
        Assert.Equal("arm64", preparation.RuleEnvironment.Architecture);
    }

    private sealed class FakeMetadataReader(MinecraftVersionMetadataInspection inspection) : IMinecraftVersionMetadataReader
    {
        public Task<MinecraftVersionMetadataInspection> InspectAsync(
            MinecraftInstance instance,
            CancellationToken cancellationToken = default) => Task.FromResult(inspection);
    }

    private sealed class FakePlatformInfo : IPlatformInfo
    {
        public PlatformInformation Get() => new("macOS", "test", JavaArchitecture.Arm64, ".NET test");
    }
}
