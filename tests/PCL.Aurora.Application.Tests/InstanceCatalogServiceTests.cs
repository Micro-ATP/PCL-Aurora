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

    private sealed class FakeInstanceLocator(IReadOnlyList<MinecraftInstance> instances) : IMinecraftInstanceLocator
    {
        public Task<IReadOnlyList<MinecraftInstance>> FindAllAsync(CancellationToken cancellationToken = default) => Task.FromResult(instances);
    }
}
