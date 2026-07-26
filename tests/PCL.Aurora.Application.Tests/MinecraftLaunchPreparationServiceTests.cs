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
        Assert.False(preparation.ClasspathInspection.IsReady);
        Assert.Contains(preparation.ArgumentPreparation.BlockingReasons, reason => reason.Contains("${classpath}", StringComparison.Ordinal));
        Assert.DoesNotContain(preparation.ArgumentPreparation.BlockingReasons, reason => reason.Contains("${auth_player_name}", StringComparison.Ordinal));
    }

    [Fact]
    public async Task PrepareAsync_UsesPersistedCustomLaunchOptions()
    {
        var rootDirectory = Path.Combine(Path.GetTempPath(), $"pcl-aurora-launch-{Guid.NewGuid():N}");
        var instanceDirectory = Path.Combine(rootDirectory, "versions", "1.21.4");
        try
        {
            Directory.CreateDirectory(instanceDirectory);
            var libraryPath = Path.Combine(rootDirectory, "libraries", "example", "library.jar");
            Directory.CreateDirectory(Path.GetDirectoryName(libraryPath)!);
            await File.WriteAllBytesAsync(libraryPath, []);
            await File.WriteAllBytesAsync(Path.Combine(instanceDirectory, "1.21.4.jar"), []);

            var instance = new MinecraftInstance(
                "1.21.4",
                instanceDirectory,
                "1.21.4",
                "release",
                null,
                MinecraftInstanceStatus.Valid);
            var metadata = new MinecraftVersionMetadata(
                "1.21.4",
                null,
                "release",
                null,
                new MinecraftVersionDownload(new Uri("https://example.invalid/client.jar"), null, null),
                null,
                new MinecraftLaunchMetadata(
                    "net.minecraft.client.main.Main",
                    ["-cp", "${classpath}"],
                    ["--width", "${resolution_width}", "--height", "${resolution_height}"],
                    HasModernArguments: true,
                    HasConditionalArguments: false,
                    LegacyGameArguments: null),
                [
                    new MinecraftVersionLibrary(
                        "example:library:1.0",
                        "example/library.jar",
                        new MinecraftVersionDownload(new Uri("https://example.invalid/library.jar"), null, null),
                        HasConditionalRules: false),
                ]);
            var inspection = new MinecraftVersionMetadataInspection([metadata], metadata, []);
            var options = new MinecraftLaunchOptions(
                AdditionalJvmArguments: "-Xmx4G",
                AdditionalGameArguments: "--demo",
                WindowMode: MinecraftGameWindowMode.Custom,
                WindowWidth: 1280,
                WindowHeight: 720);
            var service = new MinecraftLaunchPreparationService(
                new FakeVersionPreparationService(new MinecraftVersionPreparation(inspection, new MinecraftDownloadPlan("1.21.4", [], []))),
                new FixedLauncherPreferencesService(new LauncherPreferences(LauncherThemeMode.System, LaunchOptions: options)));
            OfflineAccount.TryCreate("AuroraPlayer", out var account);

            var preparation = await service.PrepareAsync(instance, account);

            Assert.True(preparation.ArgumentPreparation.IsReady);
            Assert.Contains("-Xmx4G", preparation.ArgumentPreparation.Arguments!.JvmArguments);
            Assert.Equal(
                ["--width", "1280", "--height", "720", "--demo"],
                preparation.ArgumentPreparation.Arguments.GameArguments);
        }
        finally
        {
            if (Directory.Exists(rootDirectory))
            {
                Directory.Delete(rootDirectory, recursive: true);
            }
        }
    }

    private sealed class FakeVersionPreparationService(MinecraftVersionPreparation preparation) : IMinecraftVersionPreparationService
    {
        public Task<MinecraftVersionPreparation> PrepareAsync(
            MinecraftInstance instance,
            CancellationToken cancellationToken = default) => Task.FromResult(preparation);
    }

    private sealed class FixedLauncherPreferencesService(LauncherPreferences preferences) : ILauncherPreferencesService
    {
        public LauncherPreferences Current { get; } = preferences;

        public Task<LauncherPreferencesLoadResult> LoadAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new LauncherPreferencesLoadResult(Current, null));

        public Task SaveThemeModeAsync(LauncherThemeMode themeMode, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task SaveSelectedInstanceNameAsync(string? instanceName, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task SaveOfflinePlayerNameAsync(string? playerName, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task SaveDownloadConcurrencyAsync(int concurrency, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task SaveDownloadSpeedLimitStepAsync(int speedLimitStep, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task SaveMicrosoftAccountAsync(MicrosoftAccountProfile? profile, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task SaveLaunchOptionsAsync(MinecraftLaunchOptions options, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
