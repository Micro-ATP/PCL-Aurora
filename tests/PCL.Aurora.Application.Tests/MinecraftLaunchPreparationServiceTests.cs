using PCL.Aurora.Application;
using PCL.Aurora.Domain;

namespace PCL.Aurora.Application.Tests;

public sealed class MinecraftLaunchPreparationServiceTests
{
    [Fact]
    public async Task PrepareAsync_ReportsClasspathAsTheRemainingLaunchBlocker()
    {
        var instance = new MinecraftInstance(
            "1.21.4",
            "/minecraft/versions/1.21.4",
            "1.21.4",
            "release",
            null,
            MinecraftInstanceStatus.Valid);
        var metadata = new MinecraftVersionMetadata(
            "1.21.4",
            null,
            "release",
            null,
            null,
            new MinecraftVersionAssetIndex("17", new Uri("https://example.invalid/assets.json"), null, null),
            new MinecraftLaunchMetadata(
                "net.minecraft.client.main.Main",
                ["-cp", "${classpath}"],
                ["--username", "${auth_player_name}"],
                HasModernArguments: true,
                HasConditionalArguments: false,
                LegacyGameArguments: null));
        var inspection = new MinecraftVersionMetadataInspection([metadata], metadata, []);
        var plan = new MinecraftDownloadPlan("1.21.4", [], []);
        var service = new MinecraftLaunchPreparationService(
            new FakeVersionPreparationService(new MinecraftVersionPreparation(inspection, plan)));
        OfflineAccount.TryCreate("AuroraPlayer", out var account);

        var preparation = await service.PrepareAsync(instance, account);

        Assert.False(preparation.ArgumentPreparation.IsReady);
        Assert.Contains(preparation.ArgumentPreparation.BlockingReasons, reason => reason.Contains("${classpath}", StringComparison.Ordinal));
        Assert.DoesNotContain(preparation.ArgumentPreparation.BlockingReasons, reason => reason.Contains("${auth_player_name}", StringComparison.Ordinal));
    }

    private sealed class FakeVersionPreparationService(MinecraftVersionPreparation preparation) : IMinecraftVersionPreparationService
    {
        public Task<MinecraftVersionPreparation> PrepareAsync(
            MinecraftInstance instance,
            CancellationToken cancellationToken = default) => Task.FromResult(preparation);
    }
}
