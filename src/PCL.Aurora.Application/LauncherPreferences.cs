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
    GameManagementOptions? GameManagementOptions = null,
    InterfaceSettings? InterfaceSettings = null,
    LauncherLocalizationSettings? LocalizationSettings = null,
    LauncherMiscSettings? MiscSettings = null)
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
        (GameManagementOptions?.IsValid ?? true) &&
        (InterfaceSettings?.IsValid ?? true) &&
        (LocalizationSettings?.IsValid ?? true) &&
        (MiscSettings?.IsValid ?? true);

    public MinecraftLaunchOptions EffectiveLaunchOptions => LaunchOptions ?? MinecraftLaunchOptions.Default;

    public GameManagementOptions EffectiveGameManagementOptions =>
        GameManagementOptions ?? PCL.Aurora.Application.GameManagementOptions.Default;

    public InterfaceSettings EffectiveInterfaceSettings =>
        InterfaceSettings ?? PCL.Aurora.Application.InterfaceSettings.Default;

    public LauncherLocalizationSettings EffectiveLocalizationSettings =>
        LocalizationSettings ?? LauncherLocalizationSettings.Default;

    public LauncherMiscSettings EffectiveMiscSettings =>
        MiscSettings ?? LauncherMiscSettings.Default;

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
}
