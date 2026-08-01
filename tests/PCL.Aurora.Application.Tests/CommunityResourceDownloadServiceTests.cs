using PCL.Aurora.Domain;

namespace PCL.Aurora.Application.Tests;

public sealed class CommunityResourceDownloadServiceTests
{
    [Fact]
    public async Task DownloadAsync_BuildsVerifiedSingleFilePlanForChosenDirectory()
    {
        var executor = new CapturingDownloadExecutor();
        var service = new CommunityResourceDownloadService(executor);
        var project = CreateProject();
        var version = CreateVersion("sodium.jar");
        var destination = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "aurora-chosen-folder"));

        var result = await service.DownloadAsync(project, version, destination);

        Assert.Equal(Path.Combine(destination, "sodium.jar"), result);
        Assert.Equal(destination, executor.RootDirectory);
        var artifact = Assert.Single(executor.Plan!.Artifacts);
        Assert.Equal("sodium.jar", artifact.RelativePath);
        Assert.Equal(version.PrimaryFile!.Sha1, artifact.Sha1);
        Assert.Equal(version.PrimaryFile.Size, artifact.Size);
    }

    [Fact]
    public async Task DownloadAsync_RejectsFileNameContainingDirectories()
    {
        var service = new CommunityResourceDownloadService(new CapturingDownloadExecutor());

        var exception = await Assert.ThrowsAsync<InvalidDataException>(() =>
            service.DownloadAsync(CreateProject(), CreateVersion("../outside.jar"), Path.GetTempPath()));

        Assert.Contains("不安全", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task DownloadWithDependenciesAsync_BuildsOneVerifiedPlan()
    {
        var executor = new CapturingDownloadExecutor();
        var service = new CommunityResourceDownloadService(executor);
        var root = CreateVersion("sodium.jar");
        var dependency = CreateVersion("fabric-api.jar") with
        {
            Id = "dependency-version",
            Name = "Fabric API",
        };
        var destination = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "aurora-dependency-folder"));

        var result = await service.DownloadWithDependenciesAsync(
            CreateProject(),
            root,
            [dependency],
            destination);

        Assert.Equal(1, result.DependencyCount);
        Assert.Equal(2, result.Paths.Count);
        Assert.Equal(["sodium.jar", "fabric-api.jar"], executor.Plan!.Artifacts.Select(item => item.RelativePath));
    }

    [Fact]
    public async Task DownloadAsync_UsesTranslatedFileNameAndCommunityMirrorFallback()
    {
        var executor = new CapturingDownloadExecutor();
        var preferences = new FakePreferencesService(new LauncherPreferences(LauncherThemeMode.System)
        {
            GameManagementOptions = GameManagementOptions.Default with
            {
                CommunitySource = DownloadSourcePreference.Mirror,
                CommunityFileNameFormat = CommunityFileNameFormat.SquareBrackets,
            },
        });
        var service = new CommunityResourceDownloadService(executor, preferences);
        var project = CreateProject() with { TranslatedTitle = "钠 (Sodium)" };

        var result = await service.DownloadAsync(project, CreateVersion("sodium~0.6.jar"), Path.GetTempPath());

        Assert.EndsWith("[钠] sodium-0.6.jar", result, StringComparison.Ordinal);
        var artifact = Assert.Single(executor.Plan!.Artifacts);
        Assert.Equal("mod.mcimirror.top", artifact.Url.Host);
        Assert.Equal("cdn.modrinth.com", Assert.Single(artifact.AlternativeUrls!).Host);
    }

    private static CommunityResourceProject CreateProject() =>
        new(
            "AANobbMI", "sodium", "Sodium", "Rendering engine", "jellysquid3",
            CommunityResourceType.Mod, new Uri("https://modrinth.com/mod/sodium"), null,
            10, 2, null, null, [], ["1.21.1"]);

    private static CommunityResourceVersion CreateVersion(string fileName) =>
        new(
            "version", "AANobbMI", "Sodium 0.6", "0.6.0", CommunityResourceVersionChannel.Release,
            DateTimeOffset.UtcNow, 5, ["1.21.1"], ["fabric"],
            [new(fileName, new Uri("https://cdn.modrinth.com/data/AANobbMI/version/sodium.jar"), new string('a', 40), 123, true)],
            []);

    private sealed class CapturingDownloadExecutor : IMinecraftDownloadExecutor
    {
        public MinecraftDownloadPlan? Plan { get; private set; }

        public string? RootDirectory { get; private set; }

        public Task ExecuteAsync(
            MinecraftDownloadPlan downloadPlan,
            string minecraftRootDirectory,
            CancellationToken cancellationToken = default)
        {
            Plan = downloadPlan;
            RootDirectory = minecraftRootDirectory;
            return Task.CompletedTask;
        }

        public Task ExecuteAsync(
            MinecraftAssetDownloadPlan downloadPlan,
            string minecraftRootDirectory,
            CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class FakePreferencesService(LauncherPreferences preferences) : ILauncherPreferencesService
    {
        public LauncherPreferences Current { get; } = preferences;

        public Task<LauncherPreferencesLoadResult> LoadAsync(CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task SaveThemeModeAsync(LauncherThemeMode themeMode, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task SaveSelectedInstanceNameAsync(string? instanceName, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task SaveOfflinePlayerNameAsync(string? playerName, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task SaveDownloadConcurrencyAsync(int concurrency, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task SaveDownloadSpeedLimitStepAsync(int speedLimitStep, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task SaveGameManagementOptionsAsync(GameManagementOptions options, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task SaveInterfaceSettingsAsync(InterfaceSettings settings, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task SaveLocalizationSettingsAsync(LauncherLocalizationSettings settings, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task SaveMiscSettingsAsync(LauncherMiscSettings settings, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task SaveUpdateSettingsAsync(LauncherUpdateSettings settings, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task SaveManualJavaExecutablePathsAsync(IReadOnlyList<string> executablePaths, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task ReplaceAsync(LauncherPreferences newPreferences, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task SaveMicrosoftAccountAsync(MicrosoftAccountProfile? profile, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task SaveLaunchOptionsAsync(MinecraftLaunchOptions options, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }
}
