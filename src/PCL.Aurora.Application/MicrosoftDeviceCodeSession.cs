namespace PCL.Aurora.Application;

/// <summary>
/// 一次内存内设备代码会话。device_code 仅供认证服务轮询，不能用于 UI 或持久化。
/// </summary>
public sealed class MicrosoftDeviceCodeSession
{
    internal MicrosoftDeviceCodeSession(string deviceCode, TimeSpan pollInterval, MicrosoftDeviceCodePrompt prompt)
    {
        DeviceCode = deviceCode;
        PollInterval = pollInterval;
        Prompt = prompt;
    }

    internal string DeviceCode { get; }

    internal TimeSpan PollInterval { get; }

    public MicrosoftDeviceCodePrompt Prompt { get; }
}
