using PCL.Aurora.Application;
using PCL.Aurora.Platform.Abstractions;

namespace PCL.Aurora.Application.Tests;

public sealed class BackgroundMusicServiceTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), $"pcl-aurora-music-{Guid.NewGuid():N}");

    [Fact]
    public async Task RefreshAndGameLifecycle_ApplyPlaybackPolicy()
    {
        var player = new RecordingAudioPlayer();
        var paths = new FixedPlatformPaths(root);
        var musicDirectory = Path.Combine(root, "Musics");
        Directory.CreateDirectory(musicDirectory);
        await File.WriteAllBytesAsync(Path.Combine(musicDirectory, "track.mp3"), [1, 2, 3]);
        await using var service = new BackgroundMusicService(player, paths);
        var settings = InterfaceSettings.Default with { AutoPlayMusic = true, StopMusicInGame = true };

        await service.RefreshAsync(settings, startAccordingToSettings: true);
        await service.HandleGameStartedAsync(settings);
        await service.HandleGameExitedAsync(settings);

        Assert.Equal(1, player.PlayCount);
        Assert.Equal(1, player.PauseCount);
        Assert.Equal(1, player.ResumeCount);
        Assert.True(player.IsPlaying);
    }

    [Fact]
    public async Task Refresh_IgnoresUnsupportedFiles()
    {
        var player = new RecordingAudioPlayer();
        Directory.CreateDirectory(Path.Combine(root, "Musics"));
        await File.WriteAllTextAsync(Path.Combine(root, "Musics", "readme.txt"), "not audio");
        await using var service = new BackgroundMusicService(player, new FixedPlatformPaths(root));

        await service.RefreshAsync(InterfaceSettings.Default, startAccordingToSettings: true);

        Assert.False(service.State.IsAvailable);
        Assert.Equal(0, player.PlayCount);
    }

    public void Dispose()
    {
        if (Directory.Exists(root)) Directory.Delete(root, true);
    }

    private sealed class FixedPlatformPaths(string path) : IPlatformPaths
    {
        public PlatformPaths Get() => new(path, Path.Combine(path, "Cache"));
    }

    private sealed class RecordingAudioPlayer : IBackgroundAudioPlayer
    {
        public bool IsSupported => true;
        public bool SupportsSystemMediaControls => false;
        public bool IsPlaying { get; private set; }
        public bool IsPaused { get; private set; }
        public string? CurrentPath { get; private set; }
        public int PlayCount { get; private set; }
        public int PauseCount { get; private set; }
        public int ResumeCount { get; private set; }
        public event EventHandler? PlaybackEnded;

        public Task PlayAsync(string path, double volume, CancellationToken cancellationToken = default)
        {
            CurrentPath = path;
            IsPlaying = true;
            IsPaused = false;
            PlayCount++;
            return Task.CompletedTask;
        }

        public Task PauseAsync(CancellationToken cancellationToken = default)
        {
            if (IsPlaying)
            {
                IsPlaying = false;
                IsPaused = true;
                PauseCount++;
            }
            return Task.CompletedTask;
        }

        public Task ResumeAsync(CancellationToken cancellationToken = default)
        {
            if (IsPaused)
            {
                IsPaused = false;
                IsPlaying = true;
                ResumeCount++;
            }
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken = default)
        {
            IsPlaying = false;
            IsPaused = false;
            CurrentPath = null;
            return Task.CompletedTask;
        }

        public Task SetVolumeAsync(double volume, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;

        public void CompleteTrack() => PlaybackEnded?.Invoke(this, EventArgs.Empty);
    }
}
