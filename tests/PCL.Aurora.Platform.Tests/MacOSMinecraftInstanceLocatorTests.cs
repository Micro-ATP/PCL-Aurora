using PCL.Aurora.Domain;
using PCL.Aurora.Platform.MacOS;

namespace PCL.Aurora.Platform.Tests;

public sealed class MacOSMinecraftInstanceLocatorTests : IDisposable
{
    private readonly string rootDirectory = Path.Combine(Path.GetTempPath(), $"pcl-aurora-instances-{Guid.NewGuid():N}");

    [Fact]
    public async Task FindAllAsync_ReportsValidAndIncompleteDirectories()
    {
        var versionsDirectory = Path.Combine(rootDirectory, "versions");
        var validDirectory = Path.Combine(versionsDirectory, "1.21.4");
        var incompleteDirectory = Path.Combine(versionsDirectory, "broken-instance");
        Directory.CreateDirectory(validDirectory);
        Directory.CreateDirectory(incompleteDirectory);
        await File.WriteAllTextAsync(
            Path.Combine(validDirectory, "1.21.4.json"),
            """
            { "id": "1.21.4", "type": "release", "releaseTime": "2024-12-03T00:00:00Z" }
            """);

        var instances = await new MacOSMinecraftInstanceLocator(rootDirectory).FindAllAsync();

        Assert.Collection(
            instances,
            instance =>
            {
                Assert.Equal("1.21.4", instance.Name);
                Assert.Equal("1.21.4", instance.VersionId);
                Assert.Equal(MinecraftInstanceStatus.Valid, instance.Status);
            },
            instance =>
            {
                Assert.Equal("broken-instance", instance.Name);
                Assert.Equal(MinecraftInstanceStatus.Incomplete, instance.Status);
            });
    }

    [Fact]
    public async Task FindAllAsync_ReportsBaseVersionAndInstalledLoaderFromMetadata()
    {
        var versionsDirectory = Path.Combine(rootDirectory, "versions");
        var forgeDirectory = Path.Combine(versionsDirectory, "1.20.1-forge-47.2.0");
        Directory.CreateDirectory(forgeDirectory);
        await File.WriteAllTextAsync(
            Path.Combine(forgeDirectory, "1.20.1-forge-47.2.0.json"),
            """
            {
              "id": "1.20.1-forge-47.2.0",
              "inheritsFrom": "1.20.1",
              "libraries": [
                { "name": "net.minecraftforge:forge:1.20.1-47.2.0" },
                { "name": "optifine:OptiFine:1.20.1_HD_U_I6" }
              ]
            }
            """);

        var instance = Assert.Single(await new MacOSMinecraftInstanceLocator(rootDirectory).FindAllAsync());

        Assert.Equal("1.20.1", instance.BaseVersionId);
        Assert.Equal("1.20.1", instance.InheritsFrom);
        Assert.Equal(MinecraftLoaderKind.Forge, instance.InstalledLoader!.Kind);
        Assert.Equal("47.2.0", instance.InstalledLoader.Version);
        Assert.True(instance.HasOptiFine);
        Assert.Contains("1.20.1", instance.VersionDisplay, StringComparison.Ordinal);
        Assert.Equal("Forge 47.2.0 + OptiFine", instance.LoaderDisplay);
    }

    public void Dispose()
    {
        if (Directory.Exists(rootDirectory))
        {
            Directory.Delete(rootDirectory, recursive: true);
        }
    }
}
