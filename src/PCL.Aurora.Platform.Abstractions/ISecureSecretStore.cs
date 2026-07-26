namespace PCL.Aurora.Platform.Abstractions;

/// <summary>
/// 平台受保护凭据库。仅可存取秘密，不应保存可显示的账户元数据。
/// </summary>
public interface ISecureSecretStore
{
    Task<string?> GetAsync(string service, string account, CancellationToken cancellationToken = default);

    Task SetAsync(string service, string account, string secret, CancellationToken cancellationToken = default);

    Task DeleteAsync(string service, string account, CancellationToken cancellationToken = default);
}
