namespace PCL.Aurora.Application;

public interface IMicrosoftAccountAuthenticationService
{
    bool IsConfigured { get; }

    Task<MicrosoftDeviceCodeSession> BeginDeviceCodeLoginAsync(CancellationToken cancellationToken = default);

    Task<MicrosoftAuthenticationResult> CompleteDeviceCodeLoginAsync(
        MicrosoftDeviceCodeSession session,
        IProgress<MicrosoftAuthenticationProgress>? progress = null,
        CancellationToken cancellationToken = default);

    Task<MicrosoftAuthenticationResult> RefreshAsync(
        string refreshToken,
        IProgress<MicrosoftAuthenticationProgress>? progress = null,
        CancellationToken cancellationToken = default);
}
