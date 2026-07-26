using PCL.Aurora.Application;
using PCL.Aurora.Platform.Abstractions;

namespace PCL.Aurora.Application.Tests;

public sealed class MinecraftDirectoryServiceTests : IDisposable
{
    private readonly string rootDirectory = Path.Combine(Path.GetTempPath(), $"pcl-aurora-directory-{Guid.NewGuid():N}");

    [Fact]
    public async Task OpenRootDirectoryAsync_DelegatesOnlyForExistingDirectory()
    {
        Directory.CreateDirectory(rootDirectory);
        var openPathService = new RecordingOpenPathService();
        var service = new MinecraftDirectoryService(new FixedRootDirectoryProvider(rootDirectory), openPathService);

        await service.OpenRootDirectoryAsync();

        Assert.Equal(Path.GetFullPath(rootDirectory), openPathService.OpenedPath);
    }

    [Fact]
    public async Task OpenRootDirectoryAsync_RejectsMissingDirectoryWithoutOpeningPath()
    {
        var openPathService = new RecordingOpenPathService();
        var service = new MinecraftDirectoryService(new FixedRootDirectoryProvider(rootDirectory), openPathService);

        await Assert.ThrowsAsync<DirectoryNotFoundException>(() => service.OpenRootDirectoryAsync());

        Assert.Null(openPathService.OpenedPath);
    }

    public void Dispose()
    {
        if (Directory.Exists(rootDirectory))
        {
            Directory.Delete(rootDirectory, recursive: true);
        }
    }

    private sealed class FixedRootDirectoryProvider(string rootDirectory) : IMinecraftRootDirectoryProvider
    {
        public string GetRootDirectory() => rootDirectory;
    }

    private sealed class RecordingOpenPathService : IOpenPathService
    {
        public string? OpenedPath { get; private set; }

        public Task OpenFolderAsync(string path, CancellationToken cancellationToken = default)
        {
            OpenedPath = path;
            return Task.CompletedTask;
        }
    }
}
