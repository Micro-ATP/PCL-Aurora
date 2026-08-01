using PCL.Aurora.Application;

namespace PCL.Aurora.Application.Tests;

public sealed class MinecraftJunkCleanupServiceTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), $"pcl-aurora-junk-{Guid.NewGuid():N}");

    [Fact]
    public async Task ScanAndClean_RemovesOnlyRegenerableMinecraftDiagnosticsAndNatives()
    {
        Write("logs/latest.log", "root log");
        Write("crash-reports/crash.txt", "crash");
        Write("hs_err_pid123.log", "jvm crash");
        Write("versions/1.20.1/logs/debug.log", "version log");
        Write("versions/1.20.1/1.20.1-natives/libnative.dylib", "native");
        Write("versions/1.20.1/saves/world/level.dat", "save");
        Write("versions/1.20.1/mods/example.jar", "mod");
        Write("options.txt", "settings");
        var service = new MinecraftJunkCleanupService();

        var plan = await service.ScanAsync(root);

        Assert.Equal(5, plan.FileCount);
        Assert.True(plan.TotalSize > 0);
        Assert.DoesNotContain(plan.Entries, entry => entry.Path.Contains("saves", StringComparison.Ordinal));
        Assert.DoesNotContain(plan.Entries, entry => entry.Path.Contains("mods", StringComparison.Ordinal));

        var result = await service.CleanAsync(plan);

        Assert.Equal(5, result.DeletedFiles);
        Assert.Equal(0, result.FailedEntries);
        Assert.False(Directory.Exists(Path.Combine(root, "logs")));
        Assert.False(Directory.Exists(Path.Combine(root, "crash-reports")));
        Assert.False(File.Exists(Path.Combine(root, "hs_err_pid123.log")));
        Assert.False(Directory.Exists(Path.Combine(root, "versions", "1.20.1", "logs")));
        Assert.False(Directory.Exists(Path.Combine(root, "versions", "1.20.1", "1.20.1-natives")));
        Assert.True(File.Exists(Path.Combine(root, "versions", "1.20.1", "saves", "world", "level.dat")));
        Assert.True(File.Exists(Path.Combine(root, "versions", "1.20.1", "mods", "example.jar")));
        Assert.True(File.Exists(Path.Combine(root, "options.txt")));
    }

    [Fact]
    public async Task ScanAsync_SkipsDiagnosticDirectoryContainingSymbolicLink()
    {
        var outside = Path.Combine(Path.GetTempPath(), $"pcl-aurora-junk-outside-{Guid.NewGuid():N}");
        try
        {
            Directory.CreateDirectory(outside);
            await File.WriteAllTextAsync(Path.Combine(outside, "keep.log"), "outside");
            var logs = Path.Combine(root, "logs");
            Directory.CreateDirectory(logs);
            Directory.CreateSymbolicLink(Path.Combine(logs, "outside"), outside);
            var service = new MinecraftJunkCleanupService();

            var plan = await service.ScanAsync(root);

            Assert.DoesNotContain(plan.Entries, entry => entry.Path == logs);
            Assert.True(File.Exists(Path.Combine(outside, "keep.log")));
        }
        finally
        {
            if (Directory.Exists(outside))
            {
                Directory.Delete(outside, recursive: true);
            }
        }
    }

    private void Write(string relativePath, string content)
    {
        var path = Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
    }

    public void Dispose()
    {
        if (Directory.Exists(root))
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
