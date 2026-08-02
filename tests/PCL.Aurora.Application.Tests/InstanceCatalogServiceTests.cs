using PCL.Aurora.Application;
using PCL.Aurora.Domain;
using PCL.Aurora.Platform.Abstractions;

namespace PCL.Aurora.Application.Tests;

public sealed class InstanceCatalogServiceTests
{
    [Fact]
    public async Task GetAllAsync_ReturnsInstancesFromPlatformLocator()
    {
        var expected = new[]
        {
            new MinecraftInstance("1.21.4", "/minecraft/versions/1.21.4", "1.21.4", "release", null, MinecraftInstanceStatus.Valid),
        };
        var service = new InstanceCatalogService(new FakeInstanceLocator(expected));

        var instances = await service.GetAllAsync();

        Assert.Equal(expected, instances);
    }

    [Fact]
    public async Task GetAllAsync_PassesPersistedMinecraftRootsToPlatformLocator()
    {
        var rootDirectory = Path.GetFullPath(Path.Combine("minecraft", "1.21.4"));
        var locator = new FakeInstanceLocator([]);
        var preferences = new LauncherPreferencesService(new StaticPreferencesStore(
            new LauncherPreferences(
                LauncherThemeMode.System,
                MinecraftRootDirectories: [rootDirectory])));
        await preferences.LoadAsync();
        var service = new InstanceCatalogService(locator, preferences);

        await service.GetAllAsync();

        Assert.Equal([rootDirectory], locator.AdditionalRootDirectories);
    }

    private sealed class FakeInstanceLocator(IReadOnlyList<MinecraftInstance> instances) : IMinecraftInstanceLocator
    {
        public IReadOnlyList<string> AdditionalRootDirectories { get; private set; } = [];

        public Task<IReadOnlyList<MinecraftInstance>> FindAllAsync(CancellationToken cancellationToken = default) => Task.FromResult(instances);

        public Task<IReadOnlyList<MinecraftInstance>> FindAllAsync(
            IReadOnlyList<string> additionalRootDirectories,
            CancellationToken cancellationToken = default)
        {
            AdditionalRootDirectories = additionalRootDirectories;
            return Task.FromResult(instances);
        }
    }

    private sealed class StaticPreferencesStore(LauncherPreferences preferences) : ILauncherPreferencesStore
    {
        public Task<LauncherPreferencesLoadResult> LoadAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new LauncherPreferencesLoadResult(preferences, null));

        public Task SaveAsync(
            LauncherPreferences savedPreferences,
            CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
