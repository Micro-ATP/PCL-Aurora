using PCL.Aurora.Domain;
using PCL.Aurora.Platform.MacOS;

namespace PCL.Aurora.Platform.Tests;

public sealed class MacOSMinecraftVersionMetadataReaderTests : IDisposable
{
    private readonly string rootDirectory = Path.Combine(Path.GetTempPath(), $"pcl-aurora-version-metadata-{Guid.NewGuid():N}");

    [Fact]
    public async Task InspectAsync_ResolvesParentMetadataWithoutWritingFiles()
    {
        var versionsDirectory = Path.Combine(rootDirectory, "versions");
        var baseDirectory = Path.Combine(versionsDirectory, "1.21.4");
        var childDirectory = Path.Combine(versionsDirectory, "fabric-1.21.4");
        Directory.CreateDirectory(baseDirectory);
        Directory.CreateDirectory(childDirectory);
        await File.WriteAllTextAsync(
            Path.Combine(baseDirectory, "1.21.4.json"),
            """
            {
              "id": "1.21.4",
              "type": "release",
              "downloads": { "client": { "url": "https://example.invalid/client.jar", "sha1": "client-sha", "size": 123 } }
            }
            """);
        await File.WriteAllTextAsync(
            Path.Combine(childDirectory, "fabric-1.21.4.json"),
            """
            {
              "id": "fabric-1.21.4",
              "inheritsFrom": "1.21.4",
              "assetIndex": { "id": "17", "url": "https://example.invalid/assets.json", "sha1": "assets-sha", "size": 456 }
            }
            """);
        var instance = new MinecraftInstance("fabric-1.21.4", childDirectory, "fabric-1.21.4", null, null, MinecraftInstanceStatus.Valid);

        var inspection = await new MacOSMinecraftVersionMetadataReader().InspectAsync(instance);

        Assert.True(inspection.IsSuccess);
        Assert.Equal(["fabric-1.21.4", "1.21.4"], inspection.InheritanceChain.Select(item => item.Id));
        Assert.Equal("fabric-1.21.4", inspection.EffectiveMetadata!.Id);
        Assert.NotNull(inspection.EffectiveMetadata.ClientDownload);
        Assert.NotNull(inspection.EffectiveMetadata.AssetIndex);
    }

    public void Dispose()
    {
        if (Directory.Exists(rootDirectory))
        {
            Directory.Delete(rootDirectory, recursive: true);
        }
    }
}
