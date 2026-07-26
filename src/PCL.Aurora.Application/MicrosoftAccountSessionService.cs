using PCL.Aurora.Domain;
using PCL.Aurora.Platform.Abstractions;

namespace PCL.Aurora.Application;

/// <summary>
/// 将 Microsoft 刷新令牌交给平台受保护凭据库，并在新会话中刷新令牌。
/// 普通启动器偏好仅保存 <see cref="MicrosoftAccountProfile"/>。
/// </summary>
public sealed class MicrosoftAccountSessionService(
    IMicrosoftAccountAuthenticationService authenticationService,
    ISecureSecretStore secretStore) : IMicrosoftAccountSessionService
{
    private const string SecretService = "PCL Aurora Microsoft OAuth";

    public async Task PersistAsync(MicrosoftAuthenticationResult result, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(result);
        var profile = MicrosoftAccountProfile.FromAuthenticatedAccount(result.Account);
        if (string.IsNullOrWhiteSpace(result.RefreshToken))
        {
            throw new ArgumentException("认证结果不含刷新令牌。", nameof(result));
        }

        await secretStore.SetAsync(SecretService, profile.Uuid, result.RefreshToken, cancellationToken).ConfigureAwait(false);
    }

    public async Task<MicrosoftAccountRestoreResult> RestoreAsync(
        MicrosoftAccountProfile profile,
        IProgress<MicrosoftAuthenticationProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(profile);
        if (!profile.IsValid)
        {
            return new(null, "本地 Microsoft 账户档案无效，未读取凭据库。");
        }

        var refreshToken = await secretStore.GetAsync(SecretService, profile.Uuid, cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            return new(null, "未在系统钥匙串中找到 Microsoft 刷新令牌，请重新登录。");
        }

        try
        {
            var result = await authenticationService.RefreshAsync(refreshToken, progress, cancellationToken).ConfigureAwait(false);
            await PersistAsync(result, cancellationToken).ConfigureAwait(false);
            return new(result.Account, null);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            return new(null, "无法恢复 Microsoft 登录；请检查网络或重新登录。");
        }
    }

    public Task RemoveAsync(MicrosoftAccountProfile profile, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(profile);
        return secretStore.DeleteAsync(SecretService, profile.Uuid, cancellationToken);
    }
}
