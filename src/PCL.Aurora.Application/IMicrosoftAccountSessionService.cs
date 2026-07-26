using PCL.Aurora.Domain;

namespace PCL.Aurora.Application;

public interface IMicrosoftAccountSessionService
{
    Task PersistAsync(MicrosoftAuthenticationResult result, CancellationToken cancellationToken = default);

    Task<MicrosoftAccountRestoreResult> RestoreAsync(
        MicrosoftAccountProfile profile,
        IProgress<MicrosoftAuthenticationProgress>? progress = null,
        CancellationToken cancellationToken = default);

    Task RemoveAsync(MicrosoftAccountProfile profile, CancellationToken cancellationToken = default);
}
