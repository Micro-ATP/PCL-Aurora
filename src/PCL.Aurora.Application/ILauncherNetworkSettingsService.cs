namespace PCL.Aurora.Application;

/// <summary>
/// 将非敏感网络偏好与平台安全存储中的代理密码应用到共享网络栈。
/// </summary>
public interface ILauncherNetworkSettingsService
{
    void Apply(LauncherMiscSettings settings, string? customProxyPassword);
}
