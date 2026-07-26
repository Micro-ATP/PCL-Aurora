using PCL.Aurora.Domain;

namespace PCL.Aurora.Application;

/// <summary>
/// 认证成功后的会话账户与刷新令牌。刷新令牌必须立即交由平台安全存储，不得进入 JSON 偏好或日志。
/// </summary>
public sealed record MicrosoftAuthenticationResult(MinecraftAccount Account, string RefreshToken);
