namespace PCL.Aurora.Application;

/// <summary>
/// Microsoft 设备代码流所需的 Aurora 自有公开客户端标识。
/// 此值不属于上游项目，且不会从上游配置、构建产物或源代码继承。
/// </summary>
public sealed record MicrosoftAuthenticationConfiguration(string? ClientId)
{
    public bool IsConfigured => Guid.TryParse(ClientId, out _);

    public void EnsureConfigured()
    {
        if (!IsConfigured)
        {
            throw new InvalidOperationException("尚未配置 Microsoft OAuth Client ID。请为 PCL Aurora 注册公开客户端，并设置 PCL_AURORA_MS_CLIENT_ID。");
        }
    }
}
