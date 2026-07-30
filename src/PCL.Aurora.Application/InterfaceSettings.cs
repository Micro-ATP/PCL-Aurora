namespace PCL.Aurora.Application;

public enum LauncherColorTheme
{
    SkyBlue = 0,
    CatBlue = 1,
    CrashBlue = 2,
}

public enum LauncherBackgroundSuitMode
{
    Smart = 0,
    Center = 1,
    Fit = 2,
    Stretch = 3,
    Tile = 4,
    TopLeft = 5,
    TopRight = 6,
    BottomLeft = 7,
    BottomRight = 8,
}

public enum LauncherBlurKernel
{
    Gaussian = 0,
    Box = 1,
}

public enum LauncherTitleContentType
{
    None = 0,
    Default = 1,
    Text = 2,
    Image = 3,
}

public enum LauncherHomepageType
{
    Blank = 0,
    LocalFile = 1,
    Online = 2,
    Preset = 3,
}

public sealed record InterfaceFeatureVisibility(
    bool PageDownload = false,
    bool PageSettings = false,
    bool PageTools = false,
    bool SetupLaunch = false,
    bool SetupJava = false,
    bool SetupManage = false,
    bool SetupLink = false,
    bool SetupInterface = false,
    bool SetupLanguage = false,
    bool SetupMisc = false,
    bool SetupUpdate = false,
    bool SetupAbout = false,
    bool SetupFeedback = false,
    bool SetupLog = false,
    bool ToolsLink = false,
    bool ToolsToolbox = false,
    bool InstanceEdit = false,
    bool InstanceExport = false,
    bool InstanceSave = false,
    bool InstanceScreenshot = false,
    bool InstanceMod = false,
    bool InstanceResourcePack = false,
    bool InstanceShader = false,
    bool InstanceSchematic = false,
    bool InstanceServer = false,
    bool FunctionInstanceSelect = false,
    bool FunctionModUpdate = false,
    bool FunctionHideSettings = false)
{
    public static InterfaceFeatureVisibility Default { get; } = new();
}

/// <summary>
/// 启动器个性化设置。字段、顺序和默认值适配自 PCL-CE PageSetupUI 与 Config.Preference。
/// </summary>
public sealed record InterfaceSettings(
    int WindowOpacity = 600,
    LauncherColorTheme LightColor = LauncherColorTheme.CatBlue,
    LauncherColorTheme DarkColor = LauncherColorTheme.CatBlue,
    bool ShowStartupLogo = true,
    bool LockWindowSize = false,
    bool ShowLaunchingHint = true,
    bool EnableAdvancedMaterial = false,
    int BlurRadius = 16,
    int BlurSamplingRate = 70,
    LauncherBlurKernel BlurKernel = LauncherBlurKernel.Gaussian,
    string GlobalFont = "",
    string MotdFont = "",
    LauncherBackgroundSuitMode BackgroundSuit = LauncherBackgroundSuitMode.Smart,
    int BackgroundOpacity = 1000,
    int BackgroundBlurRadius = 0,
    bool AutoPauseVideo = true,
    bool BackgroundColorful = true,
    int MusicVolume = 500,
    bool ShuffleMusic = true,
    bool AutoPlayMusic = true,
    bool StartMusicInGame = false,
    bool StopMusicInGame = false,
    bool EnableSystemMediaControls = true,
    LauncherTitleContentType TitleType = LauncherTitleContentType.Default,
    bool TitleLeftAligned = false,
    string CustomTitleText = "",
    LauncherHomepageType HomepageType = LauncherHomepageType.Blank,
    int HomepagePreset = 0,
    string HomepageUrl = "",
    InterfaceFeatureVisibility? Hidden = null)
{
    public const int MaximumTextLength = 100;
    public const int MaximumUrlLength = 2048;

    public static InterfaceSettings Default { get; } = new();

    public InterfaceFeatureVisibility EffectiveHidden => Hidden ?? InterfaceFeatureVisibility.Default;

    public bool IsValid =>
        WindowOpacity is >= 0 and <= 600 &&
        Enum.IsDefined(LightColor) &&
        Enum.IsDefined(DarkColor) &&
        BlurRadius is >= 0 and <= 40 &&
        BlurSamplingRate is >= 0 and <= 100 &&
        Enum.IsDefined(BlurKernel) &&
        GlobalFont.Length <= MaximumTextLength &&
        MotdFont.Length <= MaximumTextLength &&
        Enum.IsDefined(BackgroundSuit) &&
        BackgroundOpacity is >= 0 and <= 1000 &&
        BackgroundBlurRadius is >= 0 and <= 40 &&
        MusicVolume is >= 0 and <= 1000 &&
        Enum.IsDefined(TitleType) &&
        CustomTitleText.Length <= MaximumTextLength &&
        Enum.IsDefined(HomepageType) &&
        HomepagePreset is >= 0 and <= 64 &&
        HomepageUrl.Length <= MaximumUrlLength;
}
