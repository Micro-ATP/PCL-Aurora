using System.IO.Compression;
using PCL.Aurora.Domain;
using PCL.Aurora.Infrastructure;

namespace PCL.Aurora.Application.Tests;

public sealed class MinecraftNativeLibraryPreparerTests : IDisposable
{
    private readonly string rootDirectory = Path.Combine(Path.GetTempPath(), $"pcl-aurora-native-extract-{Guid.NewGuid():N}");

    [Fact]
    public async Task PrepareAsync_ExtractsNativeFilesAndSkipsMetaInf()
    {
        var archivePath = CreateArchive(("libexample.dylib", "native"), ("META-INF/MANIFEST.MF", "metadata"));
        var plan = CreatePlan(archivePath);

        var result = await new MinecraftNativeLibraryPreparer().PrepareAsync(plan);

        Assert.True(result.IsReady);
        Assert.Equal(1, result.ExtractedFileCount);
        Assert.Equal("native", await File.ReadAllTextAsync(Path.Combine(plan.NativesDirectory, "libexample.dylib")));
        Assert.False(File.Exists(Path.Combine(plan.NativesDirectory, "META-INF", "MANIFEST.MF")));
    }

    [Fact]
    public async Task PrepareAsync_RejectsZipSlipEntry()
    {
        var archivePath = CreateArchive(("../escape.dylib", "malicious"));
        var plan = CreatePlan(archivePath);

        var result = await new MinecraftNativeLibraryPreparer().PrepareAsync(plan);

        Assert.False(result.IsReady);
        Assert.Contains(result.BlockingReasons, reason => reason.Contains("不安全", StringComparison.Ordinal));
        Assert.False(File.Exists(Path.Combine(rootDirectory, "escape.dylib")));
    }

    private string CreateArchive(params (string EntryName, string Content)[] entries)
    {
        Directory.CreateDirectory(rootDirectory);
        var archivePath = Path.Combine(rootDirectory, $"{Guid.NewGuid():N}.jar");
        using var archive = ZipFile.Open(archivePath, ZipArchiveMode.Create);
        foreach (var (entryName, content) in entries)
        {
            using var writer = new StreamWriter(archive.CreateEntry(entryName).Open());
            writer.Write(content);
        }

        return archivePath;
    }

    private MinecraftNativeLibraryPlan CreatePlan(string archivePath) =>
        new(
            Path.Combine(rootDirectory, "natives"),
            [new MinecraftNativeLibraryArchive(
                "org.example:native:1.0",
                "natives-macos-arm64",
                archivePath,
                new MinecraftVersionDownload(new Uri("https://example.invalid/native.jar"), null, null))],
            [],
            []);

    public void Dispose()
    {
        if (Directory.Exists(rootDirectory))
        {
            Directory.Delete(rootDirectory, recursive: true);
        }
    }
}
