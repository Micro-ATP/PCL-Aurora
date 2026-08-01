namespace PCL.Aurora.Application;

public enum LauncherAnnouncementMode
{
    All = 0,
    ImportantOnly = 1,
    Disabled = 2,
}

public enum LauncherProxyMode
{
    None = 0,
    System = 1,
    Custom = 2,
}

public sealed record LauncherLocalizationSettings(
    string Language = "zh-CN",
    string FormatCulture = "auto")
{
    public const string Auto = "auto";
    public const string FollowInterfaceLanguage = "ui-language";
    public const string DefaultLanguageCode = "zh-CN";

    public static IReadOnlyList<string> SupportedLanguageCodes { get; } =
    [
        DefaultLanguageCode,
    ];

    public static LauncherLocalizationSettings Default { get; } = new();

    public bool IsValid =>
        SupportedLanguageCodes.Contains(Language, StringComparer.OrdinalIgnoreCase) &&
        IsValidCulture(FormatCulture);

    public static bool IsValidCulture(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        if (string.Equals(value, Auto, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(value, FollowInterfaceLanguage, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return SupportedLanguageCodes.Contains(value, StringComparer.OrdinalIgnoreCase);
    }
}

public sealed record LauncherMiscSettings(
    LauncherAnnouncementMode AnnouncementMode = LauncherAnnouncementMode.All,
    int AnimationFpsLimitStep = 59,
    int MaximumGameLogLinesStep = 13,
    bool DisableHardwareAcceleration = false,
    bool Telemetry = false,
    bool EnableDoh = true,
    LauncherProxyMode ProxyMode = LauncherProxyMode.System,
    string CustomProxyAddress = "",
    string CustomProxyUsername = "",
    int DebugAnimationSpeedStep = 9,
    bool DebugSkipCopy = false,
    bool DebugMode = false,
    bool DebugDelay = false)
{
    public const int MaximumProxyAddressLength = 2048;
    public const int MaximumProxyUsernameLength = 256;

    public static LauncherMiscSettings Default { get; } = new();

    public int AnimationFramesPerSecond => AnimationFpsLimitStep + 1;

    public int MaximumGameLogLines => MaximumGameLogLinesStep switch
    {
        <= 5 => MaximumGameLogLinesStep * 10 + 50,
        <= 13 => MaximumGameLogLinesStep * 50 - 150,
        <= 28 => MaximumGameLogLinesStep * 100 - 800,
        _ => int.MaxValue,
    };

    public double AnimationSpeedMultiplier =>
        DebugAnimationSpeedStep > 29 ? 0 : DebugAnimationSpeedStep / 10d + 0.1d;

    public bool IsValid =>
        Enum.IsDefined(AnnouncementMode) &&
        AnimationFpsLimitStep is >= 0 and <= 59 &&
        MaximumGameLogLinesStep is >= 0 and <= 29 &&
        Enum.IsDefined(ProxyMode) &&
        CustomProxyAddress is not null &&
        CustomProxyUsername is not null &&
        CustomProxyAddress.Length <= MaximumProxyAddressLength &&
        CustomProxyUsername.Length <= MaximumProxyUsernameLength &&
        DebugAnimationSpeedStep is >= 0 and <= 30;
}
