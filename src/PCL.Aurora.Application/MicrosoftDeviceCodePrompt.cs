namespace PCL.Aurora.Application;

/// <summary>
/// 可展示给用户的设备代码说明；不包含供轮询使用的 device_code。
/// </summary>
public sealed record MicrosoftDeviceCodePrompt(
    string UserCode,
    Uri VerificationUri,
    Uri OpenUri,
    DateTimeOffset ExpiresAt);
