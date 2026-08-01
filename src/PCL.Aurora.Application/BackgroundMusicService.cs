using PCL.Aurora.Platform.Abstractions;

namespace PCL.Aurora.Application;

public sealed class BackgroundMusicService : IBackgroundMusicService, IAsyncDisposable
{
    private static readonly HashSet<string> SupportedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".mp3", ".m4a", ".aac", ".wav", ".aif", ".aiff", ".caf",
    };

    private readonly IBackgroundAudioPlayer player;
    private readonly string musicDirectory;
    private readonly SemaphoreSlim gate = new(1, 1);
    private IReadOnlyList<string> playlist = [];
    private InterfaceSettings settings = InterfaceSettings.Default;
    private int currentIndex = -1;
    private bool gameIsRunning;
    private bool disposed;

    public BackgroundMusicService(IBackgroundAudioPlayer player, IPlatformPaths platformPaths)
    {
        this.player = player;
        musicDirectory = Path.Combine(platformPaths.Get().ApplicationDataDirectory, "Musics");
        player.PlaybackEnded += PlayerPlaybackEnded;
        State = CreateState("尚未读取背景音乐。 ");
    }

    public bool SupportsSystemMediaControls => player.SupportsSystemMediaControls;

    public BackgroundMusicState State { get; private set; }

    public event EventHandler<BackgroundMusicState>? StateChanged;

    public async Task RefreshAsync(
        InterfaceSettings newSettings,
        bool startAccordingToSettings,
        CancellationToken cancellationToken = default)
    {
        await gate.WaitAsync(cancellationToken);
        try
        {
            ThrowIfDisposed();
            settings = newSettings;
            Directory.CreateDirectory(musicDirectory);
            playlist = Directory.EnumerateFiles(musicDirectory)
                .Where(path => SupportedExtensions.Contains(Path.GetExtension(path)))
                .OrderBy(path => Path.GetFileName(path), StringComparer.CurrentCultureIgnoreCase)
                .ToArray();
            if (player.CurrentPath is { } currentPath)
            {
                currentIndex = FindTrackIndex(currentPath);
                if (currentIndex < 0) await player.StopAsync(cancellationToken);
            }
            if (playlist.Count == 0)
            {
                currentIndex = -1;
                await player.StopAsync(cancellationToken);
                PublishState("背景音乐文件夹中没有可播放的音频。 ");
                return;
            }

            if (startAccordingToSettings && settings.AutoPlayMusic && !gameIsRunning && !player.IsPlaying)
            {
                await PlayNextCoreAsync(cancellationToken, chooseInitialTrack: true);
            }
            else
            {
                PublishState($"已读取 {playlist.Count} 首背景音乐。 ");
            }
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task ApplySettingsAsync(InterfaceSettings newSettings, CancellationToken cancellationToken = default)
    {
        await gate.WaitAsync(cancellationToken);
        try
        {
            ThrowIfDisposed();
            settings = newSettings;
            await player.SetVolumeAsync(settings.MusicVolume / 1000d, cancellationToken);
            if (settings.AutoPlayMusic && !gameIsRunning && playlist.Count > 0 && !player.IsPlaying && !player.IsPaused)
            {
                await PlayNextCoreAsync(cancellationToken, chooseInitialTrack: true);
            }
            PublishState(player.IsPlaying ? "正在播放背景音乐。" : player.IsPaused ? "背景音乐已暂停。" : "背景音乐已就绪。 ");
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task HandleGameStartedAsync(InterfaceSettings newSettings, CancellationToken cancellationToken = default)
    {
        await gate.WaitAsync(cancellationToken);
        try
        {
            ThrowIfDisposed();
            settings = newSettings;
            gameIsRunning = true;
            if (settings.StopMusicInGame)
            {
                await player.PauseAsync(cancellationToken);
                PublishState("游戏运行期间已暂停背景音乐。 ");
            }
            else if (settings.StartMusicInGame)
            {
                if (player.IsPaused) await player.ResumeAsync(cancellationToken);
                else if (!player.IsPlaying && playlist.Count > 0) await PlayNextCoreAsync(cancellationToken, true);
                PublishState("游戏运行期间正在播放背景音乐。 ");
            }
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task HandleGameExitedAsync(InterfaceSettings newSettings, CancellationToken cancellationToken = default)
    {
        await gate.WaitAsync(cancellationToken);
        try
        {
            ThrowIfDisposed();
            settings = newSettings;
            gameIsRunning = false;
            if (settings.StartMusicInGame)
            {
                await player.PauseAsync(cancellationToken);
                PublishState("游戏退出后已暂停背景音乐。 ");
            }
            else if (settings.StopMusicInGame)
            {
                if (player.IsPaused) await player.ResumeAsync(cancellationToken);
                else if (!player.IsPlaying && playlist.Count > 0) await PlayNextCoreAsync(cancellationToken, true);
                PublishState("游戏退出后已继续播放背景音乐。 ");
            }
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        await gate.WaitAsync(cancellationToken);
        try
        {
            await player.StopAsync(cancellationToken);
            PublishState("背景音乐已停止。 ");
        }
        finally
        {
            gate.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (disposed) return;
        disposed = true;
        player.PlaybackEnded -= PlayerPlaybackEnded;
        await player.DisposeAsync();
        gate.Dispose();
    }

    private async void PlayerPlaybackEnded(object? sender, EventArgs e)
    {
        try
        {
            await gate.WaitAsync();
            try
            {
                if (!disposed && playlist.Count > 0) await PlayNextCoreAsync(CancellationToken.None, false);
            }
            finally
            {
                gate.Release();
            }
        }
        catch
        {
            PublishState("无法继续播放下一首背景音乐。 ");
        }
    }

    private async Task PlayNextCoreAsync(CancellationToken cancellationToken, bool chooseInitialTrack)
    {
        if (playlist.Count == 0) return;
        if (settings.ShuffleMusic && playlist.Count > 1)
        {
            var next = currentIndex;
            while (next == currentIndex) next = Random.Shared.Next(playlist.Count);
            currentIndex = next;
        }
        else if (chooseInitialTrack && currentIndex < 0)
        {
            currentIndex = 0;
        }
        else
        {
            currentIndex = (currentIndex + 1 + playlist.Count) % playlist.Count;
        }
        await player.PlayAsync(playlist[currentIndex], settings.MusicVolume / 1000d, cancellationToken);
        PublishState($"正在播放：{Path.GetFileNameWithoutExtension(playlist[currentIndex])}");
    }

    private int FindTrackIndex(string path)
    {
        for (var index = 0; index < playlist.Count; index++)
        {
            if (string.Equals(playlist[index], path, StringComparison.Ordinal)) return index;
        }
        return -1;
    }

    private BackgroundMusicState CreateState(string status) => new(
        player.IsSupported && playlist.Count > 0,
        player.IsPlaying,
        player.IsPaused,
        player.CurrentPath is null ? null : Path.GetFileNameWithoutExtension(player.CurrentPath),
        status.Trim());

    private void PublishState(string status)
    {
        State = CreateState(status);
        StateChanged?.Invoke(this, State);
    }

    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(disposed, this);
}
