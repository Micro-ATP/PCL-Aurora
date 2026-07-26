using PCL.Aurora.Domain;

namespace PCL.Aurora.Domain.Tests;

public sealed class MinecraftClasspathBuilderTests : IDisposable
{
    private readonly string rootDirectory = Path.Combine(Path.GetTempPath(), $"pcl-aurora-classpath-{Guid.NewGuid():N}");

    [Fact]
    public async Task Build_UsesExistingArtifactAndClientJarInsideMinecraftRoot()
    {
        var libraryPath = Path.Combine(rootDirectory, "libraries", "com", "example", "demo", "1.0", "demo-1.0.jar");
        var clientPath = Path.Combine(rootDirectory, "versions", "1.21.4", "1.21.4.jar");
        Directory.CreateDirectory(Path.GetDirectoryName(libraryPath)!);
        Directory.CreateDirectory(Path.GetDirectoryName(clientPath)!);
        await File.WriteAllTextAsync(libraryPath, "library");
        await File.WriteAllTextAsync(clientPath, "client");
        var metadata = CreateMetadata("com/example/demo/1.0/demo-1.0.jar");
        var inspection = new MinecraftVersionMetadataInspection([metadata], metadata, []);

        var result = MinecraftClasspathBuilder.Build(inspection, rootDirectory);

        Assert.True(result.IsReady);
        Assert.Equal([libraryPath, clientPath], result.Entries);
        Assert.Equal(string.Join(Path.PathSeparator, libraryPath, clientPath), result.Value);
    }

    [Fact]
    public void Build_RejectsArtifactPathOutsideLibrariesDirectory()
    {
        var metadata = CreateMetadata("../outside.jar");
        var inspection = new MinecraftVersionMetadataInspection([metadata], metadata, []);

        var result = MinecraftClasspathBuilder.Build(inspection, rootDirectory);

        Assert.False(result.IsReady);
        Assert.Contains(result.BlockingReasons, reason => reason.Contains("目录外", StringComparison.Ordinal));
    }

    public void Dispose()
    {
        if (Directory.Exists(rootDirectory))
        {
            Directory.Delete(rootDirectory, recursive: true);
        }
    }

    private static MinecraftVersionMetadata CreateMetadata(string artifactPath) =>
        new(
            "1.21.4",
            null,
            "release",
            null,
            new MinecraftVersionDownload(new Uri("https://example.invalid/client.jar"), null, null),
            null,
            null,
            [new MinecraftVersionLibrary(
                "com.example:demo:1.0",
                artifactPath,
                new MinecraftVersionDownload(new Uri("https://example.invalid/demo.jar"), null, null),
                HasConditionalRules: false)]);
}
