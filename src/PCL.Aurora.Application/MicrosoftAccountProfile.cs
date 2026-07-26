using PCL.Aurora.Domain;

namespace PCL.Aurora.Application;

/// <summary>
/// 可安全写入本机偏好的 Microsoft 账户元数据；不含访问令牌或刷新令牌。
/// </summary>
public sealed record MicrosoftAccountProfile(string DisplayName, string Uuid)
{
    public bool IsValid =>
        OfflineAccount.TryCreate(DisplayName, out _) &&
        Guid.TryParse(Uuid, out _);

    public static MicrosoftAccountProfile FromAuthenticatedAccount(MinecraftAccount account)
    {
        ArgumentNullException.ThrowIfNull(account);
        if (account.Kind != MinecraftAccountKind.Microsoft || !account.IsAuthenticated || !Guid.TryParse(account.Uuid, out _))
        {
            throw new ArgumentException("只有已认证的 Microsoft 账户可转换为安全档案。", nameof(account));
        }

        return new(account.DisplayName, account.Uuid);
    }
}
