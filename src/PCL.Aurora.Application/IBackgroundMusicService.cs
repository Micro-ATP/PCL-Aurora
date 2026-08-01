namespace PCL.Aurora.Application;

public sealed record BackgroundMusicState(
    bool IsAvailable,
    bool IsPlaying,
    bool IsPaused,
    string? TrackName,
    string Status);

public interface IBackgroundMusicService
{
    bool SupportsSystemMediaControls { get; }

    BackgroundMusicState State { get; }

    event EventHandler<BackgroundMusicState>? StateChanged;

    Task RefreshAsync(InterfaceSettings settings, bool startAccordingToSettings, CancellationToken cancellationToken = default);

    Task ApplySettingsAsync(InterfaceSettings settings, CancellationToken cancellationToken = default);

    Task HandleGameStartedAsync(InterfaceSettings settings, CancellationToken cancellationToken = default);

    Task HandleGameExitedAsync(InterfaceSettings settings, CancellationToken cancellationToken = default);

    Task StopAsync(CancellationToken cancellationToken = default);
}
