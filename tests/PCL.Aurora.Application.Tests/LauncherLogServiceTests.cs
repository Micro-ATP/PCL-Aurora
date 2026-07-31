using System.IO.Compression;
using PCL.Aurora.Infrastructure;
using PCL.Aurora.Platform.Abstractions;

namespace PCL.Aurora.Application.Tests;

public sealed class LauncherLogServiceTests : IDisposable
{
    private readonly string applicationDataDirectory = Path.Combine(
        Path.GetTempPath(),
        $"pcl-aurora-logs-{Guid.NewGuid():N}");

    [Fact]
    public async Task InitializeAndAppend_CreateCurrentLogWithRealContent()
    {
        using var service = CreateService();

        await service.InitializeAsync();
        await service.AppendAsync("Test", "第一行\n第二行");

        var current = Assert.Single(await service.GetFilesAsync());
        Assert.True(current.IsCurrent);
        var content = await File.ReadAllTextAsync(current.FullPath);
        Assert.Contains("PCL Aurora", content);
        Assert.Contains("[Test] 第一行", content);
        Assert.Contains("[Test] 第二行", content);
    }

    [Fact]
    public async Task ClearHistoryAndExport_KeepCurrentLogAndArchiveExpectedFiles()
    {
        using var service = CreateService();
        await service.InitializeAsync();
        var oldLogPath = Path.Combine(service.LogDirectory, "Launch-2026-1-1-000000000.log");
        await File.WriteAllTextAsync(oldLogPath, "old");
        File.SetLastWriteTimeUtc(oldLogPath, DateTime.UtcNow.AddDays(-1));

        var files = await service.GetFilesAsync();
        Assert.Equal(2, files.Count);
        Assert.True(files[0].IsCurrent);

        var zipPath = Path.Combine(applicationDataDirectory, "export.zip");
        await service.ExportAsync(files, zipPath);
        using (var archive = ZipFile.OpenRead(zipPath))
        {
            Assert.Equal(2, archive.Entries.Count);
            Assert.Contains(archive.Entries, entry => entry.Name == Path.GetFileName(oldLogPath));
        }

        Assert.Equal(1, await service.ClearHistoryAsync());
        var remaining = Assert.Single(await service.GetFilesAsync());
        Assert.True(remaining.IsCurrent);
        Assert.False(File.Exists(oldLogPath));
    }

    public void Dispose()
    {
        if (Directory.Exists(applicationDataDirectory))
        {
            Directory.Delete(applicationDataDirectory, recursive: true);
        }
    }

    private LauncherLogService CreateService() =>
        new(new FixedPlatformPaths(applicationDataDirectory));

    private sealed class FixedPlatformPaths(string applicationDataDirectory) : IPlatformPaths
    {
        public PlatformPaths Get() => new(applicationDataDirectory, Path.Combine(applicationDataDirectory, "cache"));
    }
}
