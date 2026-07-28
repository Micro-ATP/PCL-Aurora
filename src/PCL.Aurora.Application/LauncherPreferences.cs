using PCL.Aurora.Domain;

namespace PCL.Aurora.Application;

/// <summary>
/// 可安全存储在本机的启动器偏好。
/// </summary>
public sealed record LauncherPreferences(
    LauncherThemeMode ThemeMode,
    string? SelectedInstanceName = null,
    string? OfflinePlayerName = null,
    int DownloadConcurrency = LauncherDownloadSettings.DefaultConcurrency,
    int DownloadSpeedLimitStep = LauncherDownloadSettings.UnlimitedSpeedLimitStep,
    MicrosoftAccountProfile? MicrosoftAccount = null,
    MinecraftLaunchOptions? LaunchOptions = null,
    string? MicrosoftOAuthClientId = null)
{
    public static LauncherPreferences Default { get; } = new(LauncherThemeMode.System);

    public bool IsValid =>
        Enum.IsDefined(ThemeMode) &&
        IsValidInstanceName(SelectedInstanceName) &&
        IsValidOfflinePlayerName(OfflinePlayerName) &&
        LauncherDownloadSettings.IsValidConcurrency(DownloadConcurrency) &&
        LauncherDownloadSettings.IsValidSpeedLimitStep(DownloadSpeedLimitStep) &&
        (MicrosoftAccount?.IsValid ?? true) &&
        (LaunchOptions?.IsValid ?? true) &&
        IsValidMicrosoftOAuthClientId(MicrosoftOAuthClientId);

    public MinecraftLaunchOptions EffectiveLaunchOptions => LaunchOptions ?? MinecraftLaunchOptions.Default;

    public static bool IsValidInstanceName(string? instanceName) =>
        instanceName is null ||
        (!string.IsNullOrWhiteSpace(instanceName) &&
         instanceName.Length <= 128 &&
         instanceName == Path.GetFileName(instanceName) &&
         instanceName is not "." and not ".." &&
         !instanceName.Contains('/') &&
         !instanceName.Contains('\\'));

    public static bool IsValidOfflinePlayerName(string? playerName) =>
        playerName is null || OfflineAccount.TryCreate(playerName, out _);

    public static bool IsValidMicrosoftOAuthClientId(string? clientId) =>
        clientId is null || Guid.TryParse(clientId, out _);
}
