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
    int LaunchCount = 0,
    MicrosoftAccountProfile? MicrosoftAccount = null,
    MinecraftLaunchOptions? LaunchOptions = null,
    GameManagementOptions? GameManagementOptions = null,
    InterfaceSettings? InterfaceSettings = null,
    LauncherLocalizationSettings? LocalizationSettings = null,
    LauncherMiscSettings? MiscSettings = null,
    LauncherUpdateSettings? UpdateSettings = null,
    IReadOnlyList<string>? ManualJavaExecutablePaths = null,
    string? LastNotifiedReleaseVersion = null,
    string? LastNotifiedSnapshotVersion = null,
    IReadOnlyList<string>? OfflinePlayerNames = null,
    IReadOnlyList<string>? MinecraftRootDirectories = null)
{
    public static LauncherPreferences Default { get; } = new(LauncherThemeMode.System);

    public bool IsValid =>
        Enum.IsDefined(ThemeMode) &&
        IsValidInstanceName(SelectedInstanceName) &&
        IsValidOfflinePlayerName(OfflinePlayerName) &&
        IsValidOfflinePlayerNames(OfflinePlayerNames) &&
        LauncherDownloadSettings.IsValidConcurrency(DownloadConcurrency) &&
        LauncherDownloadSettings.IsValidSpeedLimitStep(DownloadSpeedLimitStep) &&
        LaunchCount >= 0 &&
        (MicrosoftAccount?.IsValid ?? true) &&
        (LaunchOptions?.IsValid ?? true) &&
        (GameManagementOptions?.IsValid ?? true) &&
        (InterfaceSettings?.IsValid ?? true) &&
        (LocalizationSettings?.IsValid ?? true) &&
        (MiscSettings?.IsValid ?? true) &&
        (UpdateSettings?.IsValid ?? true) &&
        IsValidManualJavaExecutablePaths(ManualJavaExecutablePaths) &&
        IsValidMinecraftRootDirectories(MinecraftRootDirectories) &&
        IsValidVersionMarker(LastNotifiedReleaseVersion) &&
        IsValidVersionMarker(LastNotifiedSnapshotVersion);

    public MinecraftLaunchOptions EffectiveLaunchOptions => LaunchOptions ?? MinecraftLaunchOptions.Default;

    public GameManagementOptions EffectiveGameManagementOptions =>
        GameManagementOptions ?? PCL.Aurora.Application.GameManagementOptions.Default;

    public InterfaceSettings EffectiveInterfaceSettings =>
        InterfaceSettings ?? PCL.Aurora.Application.InterfaceSettings.Default;

    public LauncherLocalizationSettings EffectiveLocalizationSettings =>
        LocalizationSettings ?? LauncherLocalizationSettings.Default;

    public LauncherMiscSettings EffectiveMiscSettings =>
        MiscSettings ?? LauncherMiscSettings.Default;

    public LauncherUpdateSettings EffectiveUpdateSettings =>
        UpdateSettings ?? LauncherUpdateSettings.Default;

    public IReadOnlyList<string> EffectiveManualJavaExecutablePaths =>
        ManualJavaExecutablePaths ?? [];

    public IReadOnlyList<string> EffectiveOfflinePlayerNames => NormalizeOfflinePlayerNames(
        OfflinePlayerName,
        OfflinePlayerNames);

    public IReadOnlyList<string> EffectiveMinecraftRootDirectories =>
        MinecraftRootDirectories ?? [];

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

    public static IReadOnlyList<string> NormalizeOfflinePlayerNames(
        string? currentPlayerName,
        IEnumerable<string>? playerNames)
    {
        var candidates = string.IsNullOrWhiteSpace(currentPlayerName)
            ? playerNames ?? []
            : new[] { currentPlayerName }.Concat(playerNames ?? []);
        return candidates
            .Where(name => OfflineAccount.TryCreate(name, out _))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(32)
            .ToArray();
    }

    public static IReadOnlyList<string> NormalizeMinecraftRootDirectories(IEnumerable<string>? rootDirectories)
    {
        if (rootDirectories is null)
        {
            return [];
        }

        return rootDirectories
            .Where(path => !string.IsNullOrWhiteSpace(path) && Path.IsPathFullyQualified(path))
            .Select(Path.GetFullPath)
            .Distinct(PathComparer)
            .Take(64)
            .ToArray();
    }

    private static bool IsValidOfflinePlayerNames(IReadOnlyList<string>? playerNames) =>
        playerNames is null ||
        (playerNames.Count <= 32 &&
         playerNames.All(name => OfflineAccount.TryCreate(name, out _)) &&
         playerNames.Distinct(StringComparer.OrdinalIgnoreCase).Count() == playerNames.Count);

    private static bool IsValidManualJavaExecutablePaths(IReadOnlyList<string>? paths)
    {
        if (paths is null)
        {
            return true;
        }

        if (paths.Count > 64 || paths.Any(path =>
                string.IsNullOrWhiteSpace(path) ||
                path.Length > 4096 ||
                !Path.IsPathFullyQualified(path)))
        {
            return false;
        }

        return paths.Distinct(StringComparer.OrdinalIgnoreCase).Count() == paths.Count;
    }

    private static bool IsValidMinecraftRootDirectories(IReadOnlyList<string>? paths)
    {
        if (paths is null)
        {
            return true;
        }

        return paths.Count <= 64 &&
               paths.All(path => !string.IsNullOrWhiteSpace(path) &&
                                 path.Length <= 4096 &&
                                 Path.IsPathFullyQualified(path)) &&
               paths.Distinct(PathComparer).Count() == paths.Count;
    }

    private static StringComparer PathComparer =>
        OperatingSystem.IsWindows() || OperatingSystem.IsMacOS()
            ? StringComparer.OrdinalIgnoreCase
            : StringComparer.Ordinal;

    private static bool IsValidVersionMarker(string? version) =>
        version is null ||
        (version.Length <= 64 && version.All(character =>
            char.IsAsciiLetterOrDigit(character) || character is '.' or '-' or '_' or '+'));
}
