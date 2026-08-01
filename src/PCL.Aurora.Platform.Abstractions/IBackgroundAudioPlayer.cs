namespace PCL.Aurora.Platform.Abstractions;

public interface IBackgroundAudioPlayer : IAsyncDisposable
{
    bool IsSupported { get; }

    bool SupportsSystemMediaControls { get; }

    bool IsPlaying { get; }

    bool IsPaused { get; }

    string? CurrentPath { get; }

    event EventHandler? PlaybackEnded;

    Task PlayAsync(string path, double volume, CancellationToken cancellationToken = default);

    Task PauseAsync(CancellationToken cancellationToken = default);

    Task ResumeAsync(CancellationToken cancellationToken = default);

    Task StopAsync(CancellationToken cancellationToken = default);

    Task SetVolumeAsync(double volume, CancellationToken cancellationToken = default);
}
