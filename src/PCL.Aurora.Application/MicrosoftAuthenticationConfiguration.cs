namespace PCL.Aurora.Application;

/// <summary>
/// Microsoft 设备代码流所需的 Aurora 自有公开客户端标识。
/// 此值不属于上游项目，且不会从上游配置、构建产物或源代码继承。
/// </summary>
public sealed class MicrosoftAuthenticationConfiguration(string? clientId)
{
    private string? clientId = Normalize(clientId);

    public string? ClientId => Volatile.Read(ref clientId);

    public bool IsConfigured => Guid.TryParse(ClientId, out _);

    public bool TrySetClientId(string? value)
    {
        var normalized = Normalize(value);
        if (normalized is not null && !Guid.TryParse(normalized, out _))
        {
            return false;
        }

        Volatile.Write(ref clientId, normalized);
        return true;
    }

    public void EnsureConfigured()
    {
        if (!IsConfigured)
        {
            throw new InvalidOperationException("尚未配置 Microsoft OAuth Client ID。请先在启动页填写 PCL Aurora 的公开客户端 ID。");
        }
    }

    private static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
