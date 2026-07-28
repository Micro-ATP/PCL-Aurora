using System.Reflection;

namespace PCL.Aurora.Application;

/// <summary>
/// Microsoft 设备代码流所需的 Aurora 自有公开客户端标识。
/// 此值仅来自 Aurora 构建元数据或运行环境，不读取用户偏好或上游配置。
/// </summary>
public sealed class MicrosoftAuthenticationConfiguration(string? clientId)
{
    public const string EnvironmentVariableName = "PCL_AURORA_MS_CLIENT_ID";
    public const string AssemblyMetadataKey = "PclAuroraMicrosoftClientId";

    public string? ClientId { get; } = Normalize(clientId);

    public bool IsConfigured => Guid.TryParse(ClientId, out _);

    public static MicrosoftAuthenticationConfiguration FromEnvironmentOrAssembly(Assembly assembly)
    {
        ArgumentNullException.ThrowIfNull(assembly);
        var embeddedClientId = assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
            .FirstOrDefault(attribute => attribute.Key == AssemblyMetadataKey)?.Value;
        var environmentClientId = Environment.GetEnvironmentVariable(EnvironmentVariableName);
        return new MicrosoftAuthenticationConfiguration(
            string.IsNullOrWhiteSpace(environmentClientId) ? embeddedClientId : environmentClientId);
    }

    public void EnsureConfigured()
    {
        if (!IsConfigured)
        {
            throw new InvalidOperationException("当前 PCL Aurora 构建尚未配置 Microsoft OAuth Client ID。");
        }
    }

    private static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
