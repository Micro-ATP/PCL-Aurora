using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Avalonia.Controls;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using PCL.Aurora.Application;
using PCL.Aurora.Desktop.Services;
using PCL.Aurora.Domain;
using PCL.Aurora.Platform.Abstractions;

namespace PCL.Aurora.Desktop.ViewModels;

public partial class MainViewModel(
    ISystemDiagnosticsService diagnosticsService,
    IInstanceCatalogService instanceCatalogService,
    ILaunchReadinessService launchReadinessService,
    IMinecraftVersionPreparationService versionPreparationService,
    IMinecraftLaunchPreparationService launchPreparationService,
    IMinecraftInstanceInstallationService installationService,
    IMinecraftVersionCatalogService versionCatalogService,
    IMinecraftVersionArchiveService versionArchiveService,
    ICommunityResourceSearchService communityResourceSearchService,
    CurseForgeCommunityResourceSearchService curseForgeCommunityResourceSearchService,
    ICommunityResourceIconService communityResourceIconService,
    ICommunityResourceVersionService communityResourceVersionService,
    ICommunityResourceDependencyResolver communityResourceDependencyResolver,
    ICommunityResourceDownloadService communityResourceDownloadService,
    IModrinthModpackImportService modrinthModpackImportService,
    ICommunityWorldImportService communityWorldImportService,
    ICommunityFavoritesStore communityFavoritesStore,
    ICommunityResourceDescriptionTranslationService communityDescriptionTranslationService,
    IMinecraftLoaderCatalogService loaderCatalogService,
    IMinecraftOfficialLoaderCatalogService officialLoaderCatalogService,
    IMinecraftLoaderPackageDownloadService loaderPackageDownloadService,
    IMinecraftLoaderInstallerService loaderInstallerService,
    IMinecraftVersionProvisioningService versionProvisioningService,
    IMinecraftDirectoryService minecraftDirectoryService,
    IMinecraftGameLaunchService gameLaunchService,
    ILauncherPreferencesService preferencesService,
    IMicrosoftAccountAuthenticationService microsoftAuthenticationService,
    IMicrosoftAccountSessionService microsoftAccountSessionService,
    IGitHubContributorService gitHubContributorService,
    ILauncherUpdateService launcherUpdateService,
    IGitHubIssueService gitHubIssueService,
    ILauncherLogService launcherLogService,
    IOpenPathService openPathService,
    IJavaInstallationInspector javaInstallationInspector,
    IThemeService themeService,
    ISystemMemoryInfo systemMemoryInfo,
    ISecureSecretStore secretStore,
    ILauncherNetworkSettingsService networkSettingsService) : ViewModelBase
{
    public event EventHandler<string>? MicrosoftDeviceCodeAvailable;
    public event EventHandler<MinecraftLauncherVisibility>? GameProcessStarted;
    public event EventHandler<MinecraftLauncherVisibility>? GameProcessExited;
    public event EventHandler<MinecraftVersionCatalogEntry>? MinecraftVersionUpdateAvailable;

    private const string ProxySecretService = "PCL.Aurora.Network.Proxy";
    private const string ProxySecretAccount = "default";
    private static readonly Uri AuroraRepositoryUri = new("https://github.com/Micro-ATP/PCL-Aurora");
    private static readonly Uri AuroraReleasesUri = new("https://github.com/Micro-ATP/PCL-Aurora/releases");
    private static readonly Uri AuroraIssuesUri = new("https://github.com/Micro-ATP/PCL-Aurora/issues");
    private static readonly Uri AuroraNewIssueUri = new("https://github.com/Micro-ATP/PCL-Aurora/issues/new/choose");
    private static readonly Uri PclOfficialSnapshotUri = new("https://afdian.com/a/LTCat");
    private static readonly Uri AuroraLicenseUri = new("https://github.com/Micro-ATP/PCL-Aurora/blob/main/LICENSE");
    private static readonly Uri PclRepositoryUri = new("https://github.com/Meloong-Git/PCL");
    private static readonly Uri PclCeRepositoryUri = new("https://github.com/PCL-Community/PCL-CE");
    private static readonly JsonSerializerOptions FavoriteTransferSerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() },
    };
    private static readonly JsonSerializerOptions PreferencesTransferSerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };
    private static readonly IReadOnlyList<CommunityResourceLoaderOption> ModLoaderOptions =
    [
        new(CommunityResourceLoader.Any, "任意"),
        new(CommunityResourceLoader.Forge, "Forge"),
        new(CommunityResourceLoader.NeoForge, "NeoForge"),
        new(CommunityResourceLoader.Fabric, "Fabric"),
        new(CommunityResourceLoader.Quilt, "Quilt"),
    ];
    private static readonly IReadOnlyList<CommunityResourceLoaderOption> ShaderLoaderOptions =
    [
        new(CommunityResourceLoader.Any, "任意加载器"),
        new(CommunityResourceLoader.Vanilla, "原版可用"),
        new(CommunityResourceLoader.Iris, "Iris"),
        new(CommunityResourceLoader.OptiFine, "OptiFine"),
    ];
    private static readonly CommunityResourceCategoryOption AllCommunityResourceCategories = new(null, "全部");
    private static readonly CultureInfo SystemUiCulture = CultureInfo.CurrentUICulture;
    private static readonly CultureInfo SystemFormatCulture = CultureInfo.CurrentCulture;

    private readonly List<MinecraftVersionCatalogEntry> allCatalogVersions = [];
    private MinecraftAccount? selectedAccount;
    private MinecraftLoaderCatalog? loaderCatalog;
    private MinecraftLoaderKind? loaderKindFilter;
    private CommunityResourceType? communityResourceType;
    private bool isCommunityFavoritesSection;
    private MinecraftGameLaunchPreparation? gameLaunchPreparation;
    private LauncherPreferences currentPreferences = LauncherPreferences.Default;
    private bool isRefreshing;
    private bool isLoadingPreferences;
    private bool isSelectingJavaForRequirement;
    private bool contributorsLoadAttempted;
    private int launcherClientWidth = MinecraftLaunchOptions.DefaultWindowWidth;
    private int launcherClientHeight = MinecraftLaunchOptions.DefaultWindowHeight;
    private MinecraftJavaRequirement? currentJavaRequirement;
    private CancellationTokenSource? installationCancellation;
    private CancellationTokenSource? microsoftLoginCancellation;
    private Uri? microsoftVerificationUri;
    private CancellationTokenSource? communitySearchCancellation;
    private CancellationTokenSource? communityVersionCancellation;
    private CancellationTokenSource? communityDownloadCancellation;
    private CancellationTokenSource? communityDescriptionTranslationCancellation;
    private CancellationTokenSource? feedbackLoadCancellation;
    private CancellationTokenSource? loaderDirectoryCancellation;
    private CancellationTokenSource? launchOptionsSaveCancellation;
    private CancellationTokenSource? gameManagementOptionsSaveCancellation;
    private CancellationTokenSource? interfaceSettingsSaveCancellation;
    private CancellationTokenSource? localizationSettingsSaveCancellation;
    private CancellationTokenSource? miscSettingsSaveCancellation;
    private CancellationTokenSource? updateSettingsSaveCancellation;
    private CommunityResourceVersionFilterSet communityVersionFilters = new([], [], false, false);
    private string? selectedCommunityGameVersionFilter;
    private string? selectedCommunityLoaderFilter;

    public ObservableCollection<MinecraftVersionCatalogEntry> AvailableVersions { get; } = [];

    public ObservableCollection<MinecraftVersionCatalogEntry> ReleaseVersions { get; } = [];

    public ObservableCollection<MinecraftVersionCatalogEntry> SnapshotVersions { get; } = [];

    public ObservableCollection<MinecraftVersionCatalogEntry> LegacyVersions { get; } = [];

    public ObservableCollection<MinecraftVersionCatalogEntry> AprilFoolsVersions { get; } = [];

    public ObservableCollection<MinecraftLoaderCatalogEntry> AvailableLoaders { get; } = [];

    public ObservableCollection<MinecraftInstallComponentViewModel> CombinedInstallComponents { get; } =
    [
        new(MinecraftLoaderKind.Forge, "Forge", "/Assets/Loaders/PclCeForge.png"),
        new(MinecraftLoaderKind.NeoForge, "NeoForge", "/Assets/Loaders/PclCeNeoForge.png"),
        new(MinecraftLoaderKind.Fabric, "Fabric", "/Assets/Loaders/PclCeFabric.png"),
        new(MinecraftLoaderKind.OptiFine, "OptiFine", "/Assets/Loaders/PclCeOptiFine.png"),
    ];

    public ObservableCollection<MinecraftLoaderDirectoryGroupViewModel> LoaderDirectoryGroups { get; } = [];

    public ObservableCollection<CommunityResourceItemViewModel> CommunityResources { get; } = [];

    public ObservableCollection<CommunityResourceVersion> CommunityResourceVersions { get; } = [];

    public ObservableCollection<CommunityResourceVersionGroupViewModel> CommunityResourceVersionGroups { get; } = [];

    public ObservableCollection<CommunityResourceVersionFilterOption> CommunityGameVersionFilters { get; } = [];

    public ObservableCollection<CommunityResourceVersionFilterOption> CommunityLoaderVersionFilters { get; } = [];

    public ObservableCollection<CommunityFavoriteFolder> CommunityFavoriteFolders { get; } = [];

    public ObservableCollection<CommunityFavoriteGroupViewModel> CommunityFavoriteGroups { get; } = [];

    public ObservableCollection<MinecraftInstance> AvailableInstances { get; } = [];

    public ObservableCollection<JavaInstallation> AvailableJavaInstallations { get; } = [];

    public ObservableCollection<GameLogLine> GameLogLines { get; } = [];

    public ObservableCollection<FeedbackGroupViewModel> FeedbackGroups { get; } =
    [
        new(GitHubIssueStatus.Processing, "正在处理", true),
        new(GitHubIssueStatus.Triage, "等待处理", true),
        new(GitHubIssueStatus.Waiting, "等待", true),
        new(GitHubIssueStatus.Paused, "暂停", true),
        new(GitHubIssueStatus.UpNext, "在即", true),
        new(GitHubIssueStatus.Completed, "已完成", false),
        new(GitHubIssueStatus.Declined, "已拒绝", false),
        new(GitHubIssueStatus.Ignored, "已忽略", false),
        new(GitHubIssueStatus.Duplicate, "重复", false),
    ];

    public ObservableCollection<LauncherLogFileItemViewModel> LauncherLogFiles { get; } = [];

    public ObservableCollection<GitHubContributorItemViewModel> Contributors { get; } = [];

    public IReadOnlyList<LicenseEntryViewModel> LicenseEntries { get; } =
    [
        new(
            "PCL Aurora 新增内容",
            "Copyright © 2026 Micro-ATP。由 Micro-ATP 独立实现并拥有版权的新增内容适用仓库根目录《PCL Aurora 严格专有迁移许可证》；来源内容不在该许可证授权范围内。",
            "source",
            "license"),
        new(
            "Plain Craft Launcher (PCL)",
            "Copyright © 龙腾猫跃。Aurora 中来源于 PCL 的代码、界面结构和资源继续受 PCL 专门许可约束，并保留相应署名、分发与二次创作条件。",
            "pcl",
            "pcl-license"),
        new(
            "PCL-CE / Plain Craft Launcher 2",
            "Copyright © PCL Community & 龙腾猫跃。Aurora 迁移或复用的 PCL-CE 启动器代码、界面与资源继续受其 Plain Craft Launcher 2 专门许可约束。",
            "pcl-ce",
            "pcl-ce-license"),
        new(
            "PCL.Core",
            "Copyright © PCL Community。Aurora 直接适配的 PCL.Core 通用代码以及其许可范围内的内容按 Apache License 2.0 使用；具体来源见 NOTICE。",
            "pcl-core",
            "apache-license"),
        new(
            "Avalonia UI",
            "Copyright © AvaloniaUI Project。桌面界面、Fluent 主题、平台后端与 Avalonia.Fonts.Inter 运行组件采用 MIT License。",
            "avalonia",
            "mit-license"),
        new(
            "CommunityToolkit.Mvvm",
            "Copyright © .NET Foundation and Contributors。用于视图模型通知与命令生成，采用 MIT License。",
            "community-toolkit",
            "mit-license"),
        new(
            ".NET 与 Microsoft.Extensions",
            "Copyright © .NET Foundation and Contributors。应用运行时、依赖注入及日志抽象组件采用 MIT License。",
            "dotnet",
            "mit-license"),
        new(
            "protobuf-net",
            "Copyright © Marc Gravell and Contributors。用于读取随项目分发的 PCL-CE 中文资源名称数据库，采用 Apache License 2.0。",
            "protobuf-net",
            "apache-license"),
        new(
            "Lucide Icons",
            "Copyright © Lucide Contributors。Aurora 使用的部分图标路径采用 ISC License，并包含源自 Feather Icons、按 MIT License 提供的部分。",
            "lucide",
            "lucide-license"),
        new(
            "HarmonyOS Sans SC",
            "Copyright © 2021 Huawei Device Co., Ltd.。Aurora 嵌入 Light、Regular、Medium 与 Bold 字体文件，适用 HarmonyOS Sans Fonts License Agreement，不按 Aurora 根许可证授权。",
            "harmony-font",
            "harmony-license"),
        new(
            "SkiaSharp 与 HarfBuzzSharp",
            "Copyright © Microsoft Corporation and Contributors。由 Avalonia 带入并用于跨平台图形绘制与字体塑形，采用 MIT License。",
            "skiasharp",
            "mit-license"),
        new(
            "桌面平台传递组件",
            "MicroCom.Runtime、Tmds.DBus.Protocol 与 Microsoft.IO.RecyclableMemoryStream 由 Avalonia 平台后端传递引用，均采用 MIT License。",
            "runtime-components",
            "mit-license"),
        new(
            "ANGLE Windows Natives",
            "Copyright © 2018 The ANGLE Project Authors。由 Avalonia Windows 图形后端传递引用，采用 BSD 三条款许可证。",
            "angle",
            "angle-license"),
    ];

    public string MinecraftRootDirectory { get; } = minecraftDirectoryService.GetRootDirectory();

    private string LauncherVersionName { get; } =
        typeof(MainViewModel).Assembly.GetName().Version?.ToString(3) ?? "0.0.0";

    public string LauncherVersionDisplay { get; } =
        $"PCL Aurora {typeof(MainViewModel).Assembly.GetName().Version?.ToString(3) ?? "开发版本"}";

    public IReadOnlyList<ThemeOption> ThemeModes { get; } =
    [
        new(LauncherThemeMode.System, "跟随系统"),
        new(LauncherThemeMode.Light, "月之亮面"),
        new(LauncherThemeMode.Dark, "月之暗面"),
    ];

    public IReadOnlyList<GameManagementOption<LauncherUpdateChannel>> UpdateChannels { get; } =
    [
        new(LauncherUpdateChannel.Release, "正式版 / Release"),
        new(LauncherUpdateChannel.Beta, "测试版 / Beta"),
    ];

    public IReadOnlyList<GameManagementOption<LauncherAutoUpdateBehavior>> AutoUpdateBehaviors { get; } =
    [
        new(LauncherAutoUpdateBehavior.DownloadAndInstall, "自动下载并安装更新"),
        new(LauncherAutoUpdateBehavior.DownloadAndNotify, "自动下载并提示更新"),
        new(LauncherAutoUpdateBehavior.NotifyOnly, "提示更新"),
        new(LauncherAutoUpdateBehavior.Disabled, "不自动检查更新（不推荐）"),
    ];

    public IReadOnlyList<GameManagementOption<DownloadSourcePreference>> FileSourceOptions { get; } =
    [
        new(DownloadSourcePreference.Mirror, "尽量使用镜像源"),
        new(DownloadSourcePreference.PreferOfficialWithFallback, "优先使用官方源，在加载缓慢时换用镜像源"),
        new(DownloadSourcePreference.Official, "尽量使用官方源"),
    ];

    public IReadOnlyList<GameManagementOption<DownloadSourcePreference>> VersionListSourceOptions { get; } =
    [
        new(DownloadSourcePreference.Mirror, "尽量使用镜像源（可能缺少刚更新的版本）"),
        new(DownloadSourcePreference.PreferOfficialWithFallback, "优先使用官方源，在加载缓慢时换用镜像源"),
        new(DownloadSourcePreference.Official, "尽量使用官方源"),
    ];

    public IReadOnlyList<GameManagementOption<DownloadSourcePreference>> CommunitySourceOptions { get; } =
    [
        new(DownloadSourcePreference.Mirror, "尽量使用镜像源"),
        new(DownloadSourcePreference.PreferOfficialWithFallback, "优先使用官方源，失败时换用镜像源"),
        new(DownloadSourcePreference.Official, "尽量使用官方源"),
    ];

    public IReadOnlyList<GameManagementOption<CommunityFileNameFormat>> CommunityFileNameFormatOptions { get; } =
    [
        new(CommunityFileNameFormat.ChineseBrackets, "【机械动力】create-1.21.1-6.0.4"),
        new(CommunityFileNameFormat.SquareBrackets, "[机械动力] create-1.21.1-6.0.4"),
        new(CommunityFileNameFormat.TranslatedNameFirst, "机械动力-create-1.21.1-6.0.4"),
        new(CommunityFileNameFormat.OriginalNameFirst, "create-1.21.1-6.0.4-机械动力"),
        new(CommunityFileNameFormat.OriginalNameOnly, "create-1.21.1-6.0.4"),
    ];

    public IReadOnlyList<GameManagementOption<CommunityModNameStyle>> CommunityModNameStyleOptions { get; } =
    [
        new(CommunityModNameStyle.TranslationTitle, "标题显示译名，详情显示文件名"),
        new(CommunityModNameStyle.FileNameTitle, "标题显示文件名，详情显示译名"),
    ];

    public IReadOnlyList<GameManagementOption<CommunityQuickDownloadBehavior>> CommunityQuickDownloadBehaviorOptions { get; } =
    [
        new(CommunityQuickDownloadBehavior.AlwaysAsk, "总是询问"),
        new(CommunityQuickDownloadBehavior.CurrentInstance, "下载到当前选中实例"),
        new(CommunityQuickDownloadBehavior.AskInstance, "询问并下载到选择的实例"),
        new(CommunityQuickDownloadBehavior.AskPath, "询问并下载到一个路径"),
    ];

    public IReadOnlyList<MinecraftGameWindowModeOption> GameWindowModes { get; } =
    [
        new(MinecraftGameWindowMode.Fullscreen, "全屏"),
        new(MinecraftGameWindowMode.Default, "默认"),
        new(MinecraftGameWindowMode.Launcher, "与启动器尺寸一致"),
        new(MinecraftGameWindowMode.Custom, "自定义"),
        new(MinecraftGameWindowMode.Maximized, "最大化"),
    ];

    public IReadOnlyList<MinecraftMemoryAllocationModeOption> MemoryAllocationModes { get; } =
    [
        new(MinecraftMemoryAllocationMode.Automatic, "自动分配"),
        new(MinecraftMemoryAllocationMode.Custom, "自定义 MiB"),
    ];

    public IReadOnlyList<MinecraftInstanceIsolationModeOption> InstanceIsolationModes { get; } =
    [
        new(MinecraftInstanceIsolationMode.Disabled, "关闭"),
        new(MinecraftInstanceIsolationMode.ModdedOnly, "隔离可安装模组的实例"),
        new(MinecraftInstanceIsolationMode.NonReleaseOnly, "隔离非正式版"),
        new(MinecraftInstanceIsolationMode.ModdedAndNonRelease, "隔离可安装模组的实例与非正式版"),
        new(MinecraftInstanceIsolationMode.All, "隔离所有实例"),
    ];

    public IReadOnlyList<MinecraftLauncherVisibilityOption> LauncherVisibilityModes { get; } =
    [
        new(MinecraftLauncherVisibility.ExitImmediately, "游戏启动后立即关闭"),
        new(MinecraftLauncherVisibility.HideAndExit, "游戏启动后隐藏，游戏退出后自动关闭"),
        new(MinecraftLauncherVisibility.HideAndReopen, "游戏启动后隐藏，游戏退出后重新打开"),
        new(MinecraftLauncherVisibility.MinimizeAndReopen, "游戏启动后最小化"),
        new(MinecraftLauncherVisibility.DoNothing, "游戏启动后仍保持不变"),
    ];

    public IReadOnlyList<MinecraftGameProcessPriorityOption> GameProcessPriorities { get; } =
    [
        new(MinecraftGameProcessPriority.RealTime, "实时（使游戏以最高优先级运行）"),
        new(MinecraftGameProcessPriority.High, "极高（谨慎使用）"),
        new(MinecraftGameProcessPriority.AboveNormal, "高（优先保证游戏运行）"),
        new(MinecraftGameProcessPriority.Normal, "中（平衡）"),
        new(MinecraftGameProcessPriority.BelowNormal, "低（优先保证其他程序运行）"),
    ];

    public IReadOnlyList<MinecraftPreferredIpStackOption> PreferredIpStacks { get; } =
    [
        new(MinecraftPreferredIpStack.PreferIpv4, "IPv4 优先"),
        new(MinecraftPreferredIpStack.JavaDefault, "Java 默认"),
        new(MinecraftPreferredIpStack.PreferIpv6, "IPv6 优先"),
    ];

    public IReadOnlyList<MinecraftRendererModeOption> RendererModes { get; } =
    [
        new(MinecraftRendererMode.GameDefault, "游戏默认"),
        new(MinecraftRendererMode.Software, "软渲染（llvmpipe）"),
        new(MinecraftRendererMode.DirectX12, "DirectX12（d3d12）"),
        new(MinecraftRendererMode.Vulkan, "Vulkan（zink）"),
    ];

    public bool IsRendererSelectionSupported => System.OperatingSystem.IsLinux();

    public bool IsWindowsLaunchOptionSupported => System.OperatingSystem.IsWindows();

    public bool HasPreLaunchCommand => !string.IsNullOrWhiteSpace(PreLaunchCommand);

    public IReadOnlyList<CommunityResourceSortOption> CommunityResourceSortOptions { get; } =
    [
        new(CommunityResourceSort.Default, "默认"),
        new(CommunityResourceSort.Relevance, "相关性"),
        new(CommunityResourceSort.Downloads, "下载量"),
        new(CommunityResourceSort.Follows, "关注量"),
        new(CommunityResourceSort.Newest, "最新发布"),
        new(CommunityResourceSort.Updated, "最近更新"),
    ];

    [ObservableProperty]
    private ThemeOption selectedThemeMode = new(themeService.CurrentMode, themeService.CurrentMode switch
    {
        LauncherThemeMode.System => "跟随系统",
        LauncherThemeMode.Light => "月之亮面",
        LauncherThemeMode.Dark => "月之暗面",
        _ => throw new ArgumentOutOfRangeException(nameof(themeService.CurrentMode)),
    });

    [ObservableProperty]
    private string themeSummary = "正在读取本地主题偏好…";

    [ObservableProperty]
    private int interfaceWindowOpacity = InterfaceSettings.Default.WindowOpacity;

    [ObservableProperty]
    private int lightThemeColorIndex = (int)InterfaceSettings.Default.LightColor;

    [ObservableProperty]
    private int darkThemeColorIndex = (int)InterfaceSettings.Default.DarkColor;

    [ObservableProperty]
    private bool showStartupLogo = InterfaceSettings.Default.ShowStartupLogo;

    [ObservableProperty]
    private bool lockWindowSize = InterfaceSettings.Default.LockWindowSize;

    [ObservableProperty]
    private bool showLaunchingHint = InterfaceSettings.Default.ShowLaunchingHint;

    [ObservableProperty]
    private bool enableAdvancedMaterial = InterfaceSettings.Default.EnableAdvancedMaterial;

    [ObservableProperty]
    private int interfaceBlurRadius = InterfaceSettings.Default.BlurRadius;

    [ObservableProperty]
    private int interfaceBlurSamplingRate = InterfaceSettings.Default.BlurSamplingRate;

    [ObservableProperty]
    private int interfaceBlurKernelIndex = (int)InterfaceSettings.Default.BlurKernel;

    [ObservableProperty]
    private string globalInterfaceFont = InterfaceSettings.Default.GlobalFont;

    [ObservableProperty]
    private string motdInterfaceFont = InterfaceSettings.Default.MotdFont;

    [ObservableProperty]
    private int backgroundSuitIndex = (int)InterfaceSettings.Default.BackgroundSuit;

    [ObservableProperty]
    private int interfaceBackgroundOpacity = InterfaceSettings.Default.BackgroundOpacity;

    [ObservableProperty]
    private int interfaceBackgroundBlurRadius = InterfaceSettings.Default.BackgroundBlurRadius;

    [ObservableProperty]
    private bool autoPauseBackgroundVideo = InterfaceSettings.Default.AutoPauseVideo;

    [ObservableProperty]
    private bool useColorfulBackground = InterfaceSettings.Default.BackgroundColorful;

    [ObservableProperty]
    private int interfaceMusicVolume = InterfaceSettings.Default.MusicVolume;

    [ObservableProperty]
    private bool shuffleBackgroundMusic = InterfaceSettings.Default.ShuffleMusic;

    [ObservableProperty]
    private bool autoPlayBackgroundMusic = InterfaceSettings.Default.AutoPlayMusic;

    [ObservableProperty]
    private bool startBackgroundMusicInGame = InterfaceSettings.Default.StartMusicInGame;

    [ObservableProperty]
    private bool stopBackgroundMusicInGame = InterfaceSettings.Default.StopMusicInGame;

    [ObservableProperty]
    private bool enableSystemMediaControls = InterfaceSettings.Default.EnableSystemMediaControls;

    [ObservableProperty]
    private int titleContentTypeIndex = (int)InterfaceSettings.Default.TitleType;

    [ObservableProperty]
    private bool titleLeftAligned = InterfaceSettings.Default.TitleLeftAligned;

    [ObservableProperty]
    private string customTitleText = InterfaceSettings.Default.CustomTitleText;

    [ObservableProperty]
    private int homepageTypeIndex = (int)InterfaceSettings.Default.HomepageType;

    [ObservableProperty]
    private int homepagePresetIndex = InterfaceSettings.Default.HomepagePreset;

    [ObservableProperty]
    private string homepageUrl = InterfaceSettings.Default.HomepageUrl;

    [ObservableProperty] private bool hidePageDownload;
    [ObservableProperty] private bool hidePageSettings;
    [ObservableProperty] private bool hidePageTools;
    [ObservableProperty] private bool hideSetupLaunch;
    [ObservableProperty] private bool hideSetupJava;
    [ObservableProperty] private bool hideSetupManage;
    [ObservableProperty] private bool hideSetupLink;
    [ObservableProperty] private bool hideSetupInterface;
    [ObservableProperty] private bool hideSetupLanguage;
    [ObservableProperty] private bool hideSetupMisc;
    [ObservableProperty] private bool hideSetupUpdate;
    [ObservableProperty] private bool hideSetupAbout;
    [ObservableProperty] private bool hideSetupFeedback;
    [ObservableProperty] private bool hideSetupLog;
    [ObservableProperty] private bool hideToolsLink;
    [ObservableProperty] private bool hideToolsToolbox;
    [ObservableProperty] private bool hideInstanceEdit;
    [ObservableProperty] private bool hideInstanceExport;
    [ObservableProperty] private bool hideInstanceSave;
    [ObservableProperty] private bool hideInstanceScreenshot;
    [ObservableProperty] private bool hideInstanceMod;
    [ObservableProperty] private bool hideInstanceResourcePack;
    [ObservableProperty] private bool hideInstanceShader;
    [ObservableProperty] private bool hideInstanceSchematic;
    [ObservableProperty] private bool hideInstanceServer;
    [ObservableProperty] private bool hideFunctionInstanceSelect;
    [ObservableProperty] private bool hideFunctionModUpdate;
    [ObservableProperty] private bool hideFunctionSettings;

    public double InterfaceWindowOpacityFraction => InterfaceWindowOpacity / 1000d + 0.4d;

    public bool UsesAdvancedMaterialSettings => EnableAdvancedMaterial;

    public bool UsesCustomTitleText => TitleContentTypeIndex == (int)LauncherTitleContentType.Text;

    public bool UsesCustomTitleImage => TitleContentTypeIndex == (int)LauncherTitleContentType.Image;

    public bool ShowsTitleLeftAlignment => TitleContentTypeIndex == (int)LauncherTitleContentType.None;

    public bool IsTitleNone { get => TitleContentTypeIndex == 0; set { if (value) TitleContentTypeIndex = 0; } }
    public bool IsTitleDefault { get => TitleContentTypeIndex == 1; set { if (value) TitleContentTypeIndex = 1; } }
    public bool IsTitleText { get => TitleContentTypeIndex == 2; set { if (value) TitleContentTypeIndex = 2; } }
    public bool IsTitleImage { get => TitleContentTypeIndex == 3; set { if (value) TitleContentTypeIndex = 3; } }

    public bool UsesLocalHomepage => HomepageTypeIndex == (int)LauncherHomepageType.LocalFile;

    public bool UsesOnlineHomepage => HomepageTypeIndex == (int)LauncherHomepageType.Online;

    public bool UsesPresetHomepage => HomepageTypeIndex == (int)LauncherHomepageType.Preset;

    public bool IsHomepageBlank { get => HomepageTypeIndex == 0; set { if (value) HomepageTypeIndex = 0; } }
    public bool IsHomepageLocal { get => HomepageTypeIndex == 1; set { if (value) HomepageTypeIndex = 1; } }
    public bool IsHomepageOnline { get => HomepageTypeIndex == 2; set { if (value) HomepageTypeIndex = 2; } }
    public bool IsHomepagePreset { get => HomepageTypeIndex == 3; set { if (value) HomepageTypeIndex = 3; } }

    public bool SupportsSystemMediaControls => System.OperatingSystem.IsWindows();

    public bool SupportsAdvancedWindowMaterial => System.OperatingSystem.IsWindows();

    public IReadOnlyList<LauncherLanguageOption> LauncherLanguages { get; } = CreateLanguageOptions();

    public IReadOnlyList<LauncherFormatCultureOption> LauncherFormatCultures { get; } = CreateFormatCultureOptions();

    [ObservableProperty]
    private LauncherLanguageOption selectedLauncherLanguage = CreateLanguageOptions()[0];

    [ObservableProperty]
    private LauncherFormatCultureOption selectedLauncherFormatCulture = CreateFormatCultureOptions()[0];

    [ObservableProperty]
    private int announcementModeIndex = (int)LauncherMiscSettings.Default.AnnouncementMode;

    [ObservableProperty]
    private int animationFpsLimitStep = LauncherMiscSettings.Default.AnimationFpsLimitStep;

    [ObservableProperty]
    private int maximumGameLogLinesStep = LauncherMiscSettings.Default.MaximumGameLogLinesStep;

    [ObservableProperty]
    private bool disableHardwareAcceleration = LauncherMiscSettings.Default.DisableHardwareAcceleration;

    [ObservableProperty]
    private bool telemetryEnabled = LauncherMiscSettings.Default.Telemetry;

    [ObservableProperty]
    private bool enableDoh = LauncherMiscSettings.Default.EnableDoh;

    [ObservableProperty]
    private int proxyModeIndex = (int)LauncherMiscSettings.Default.ProxyMode;

    [ObservableProperty]
    private string customProxyAddress = LauncherMiscSettings.Default.CustomProxyAddress;

    [ObservableProperty]
    private string customProxyUsername = LauncherMiscSettings.Default.CustomProxyUsername;

    [ObservableProperty]
    private string customProxyPassword = string.Empty;

    [ObservableProperty]
    private int debugAnimationSpeedStep = LauncherMiscSettings.Default.DebugAnimationSpeedStep;

    [ObservableProperty]
    private bool debugSkipCopy = LauncherMiscSettings.Default.DebugSkipCopy;

    [ObservableProperty]
    private bool debugMode = LauncherMiscSettings.Default.DebugMode;

    [ObservableProperty]
    private bool debugDelay = LauncherMiscSettings.Default.DebugDelay;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasMiscSettingsStatus))]
    private string miscSettingsStatus = string.Empty;

    public string AnimationFpsLimitDisplay => $"{AnimationFpsLimitStep + 1} FPS";

    public string MaximumGameLogLinesDisplay => MaximumGameLogLinesStep > 28
        ? "无限制"
        : CreateMiscSettings().MaximumGameLogLines.ToString("N0", CultureInfo.CurrentCulture);

    public string DebugAnimationSpeedDisplay => DebugAnimationSpeedStep > 29
        ? "关闭"
        : $"{DebugAnimationSpeedStep / 10d + 0.1d:N1}x";

    public bool UsesCustomProxy => ProxyModeIndex == (int)LauncherProxyMode.Custom;

    public bool IsProxyDisabled { get => ProxyModeIndex == 0; set { if (value) ProxyModeIndex = 0; } }
    public bool IsProxySystem { get => ProxyModeIndex == 1; set { if (value) ProxyModeIndex = 1; } }
    public bool IsProxyCustom { get => ProxyModeIndex == 2; set { if (value) ProxyModeIndex = 2; } }
    public bool HasMiscSettingsStatus => !string.IsNullOrWhiteSpace(MiscSettingsStatus);

    [ObservableProperty]
    private GameManagementOption<LauncherUpdateChannel> selectedUpdateChannel =
        new(LauncherUpdateChannel.Release, "正式版 / Release");

    [ObservableProperty]
    private GameManagementOption<LauncherAutoUpdateBehavior> selectedAutoUpdateBehavior =
        new(LauncherAutoUpdateBehavior.DownloadAndNotify, "自动下载并提示更新");

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanCheckForUpdates))]
    private bool isCheckingForUpdates;

    [ObservableProperty]
    private bool hasAvailableUpdate;

    [ObservableProperty]
    private string updateStatusText = "尚未检查更新";

    [ObservableProperty]
    private string availableUpdateVersionDisplay = "PCL Aurora";

    [ObservableProperty]
    private string availableUpdateSummary = "正在获取更新日志…";

    [ObservableProperty]
    private string updateChangelog = "暂无可用的更新日志。";

    private Uri? latestUpdateReleaseUri;

    public bool CanCheckForUpdates => !IsCheckingForUpdates;

    [ObservableProperty]
    private string contributorSummary = "正在读取 GitHub 贡献者…";

    [ObservableProperty]
    private bool hasContributors;

    [ObservableProperty]
    private bool isLoadingContributors;

    [ObservableProperty]
    private int selectedDownloadConcurrency = LauncherDownloadSettings.DefaultConcurrency;

    [ObservableProperty]
    private int selectedDownloadSpeedLimitStep = LauncherDownloadSettings.UnlimitedSpeedLimitStep;

    public string DownloadConcurrencyDisplay => SelectedDownloadConcurrency.ToString();

    public string DownloadSpeedLimitDisplay =>
        LauncherDownloadSettings.GetSpeedLimitDisplayName(SelectedDownloadSpeedLimitStep);

    [ObservableProperty]
    private GameManagementOption<DownloadSourcePreference> selectedFileSource =
        new(DownloadSourcePreference.PreferOfficialWithFallback, "优先使用官方源，在加载缓慢时换用镜像源");

    [ObservableProperty]
    private GameManagementOption<DownloadSourcePreference> selectedVersionListSource =
        new(DownloadSourcePreference.PreferOfficialWithFallback, "优先使用官方源，在加载缓慢时换用镜像源");

    [ObservableProperty]
    private bool autoSelectNewInstance = true;

    [ObservableProperty]
    private bool fixAuthlib = true;

    [ObservableProperty]
    private GameManagementOption<DownloadSourcePreference> selectedCommunitySource =
        new(DownloadSourcePreference.PreferOfficialWithFallback, "优先使用官方源，失败时换用镜像源");

    [ObservableProperty]
    private GameManagementOption<CommunityFileNameFormat> selectedCommunityFileNameFormat =
        new(CommunityFileNameFormat.SquareBrackets, "[机械动力] create-1.21.1-6.0.4");

    [ObservableProperty]
    private GameManagementOption<CommunityModNameStyle> selectedCommunityModNameStyle =
        new(CommunityModNameStyle.TranslationTitle, "标题显示译名，详情显示文件名");

    [ObservableProperty]
    private GameManagementOption<CommunityQuickDownloadBehavior> selectedCommunityQuickDownloadBehavior =
        new(CommunityQuickDownloadBehavior.AlwaysAsk, "总是询问");

    [ObservableProperty]
    private bool ignoreQuilt = true;

    [ObservableProperty]
    private bool autoInstallDependencies = true;

    [ObservableProperty]
    private bool releaseNotifications;

    [ObservableProperty]
    private bool snapshotNotifications;

    [ObservableProperty]
    private bool autoChangeGameLanguage = true;

    [ObservableProperty]
    private bool readClipboard;

    [ObservableProperty]
    private string additionalJvmArguments = string.Empty;

    [ObservableProperty]
    private string additionalGameArguments = string.Empty;

    [ObservableProperty]
    private MinecraftInstanceIsolationModeOption selectedInstanceIsolationMode = new(
        MinecraftInstanceIsolationMode.All,
        "隔离所有实例");

    [ObservableProperty]
    private string windowTitle = string.Empty;

    [ObservableProperty]
    private string customInfo = "PCL Aurora";

    [ObservableProperty]
    private MinecraftLauncherVisibilityOption selectedLauncherVisibility = new(
        MinecraftLauncherVisibility.DoNothing,
        "游戏启动后仍保持不变");

    [ObservableProperty]
    private MinecraftGameProcessPriorityOption selectedGameProcessPriority = new(
        MinecraftGameProcessPriority.Normal,
        "中（平衡）");

    [ObservableProperty]
    private MinecraftPreferredIpStackOption selectedPreferredIpStack = new(
        MinecraftPreferredIpStack.JavaDefault,
        "Java 默认");

    [ObservableProperty]
    private MinecraftRendererModeOption selectedRendererMode = new(
        MinecraftRendererMode.GameDefault,
        "游戏默认");

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasPreLaunchCommand))]
    private string preLaunchCommand = string.Empty;

    [ObservableProperty]
    private bool waitForPreLaunchCommand = true;

    [ObservableProperty]
    private bool disableJavaLaunchWrapper = true;

    [ObservableProperty]
    private bool disableLegacyFix;

    [ObservableProperty]
    private bool preferDedicatedGpu = true;

    [ObservableProperty]
    private bool useJavaExecutable;

    [ObservableProperty]
    private bool disableLwjglUnsafeAgent;

    [ObservableProperty]
    private bool disableCrashAnalysis;

    [ObservableProperty]
    private bool lockMemory;

    [ObservableProperty]
    private MinecraftGameWindowModeOption selectedGameWindowMode = new(
        MinecraftGameWindowMode.Default,
        "默认窗口（854 × 480）");

    [ObservableProperty]
    private string customGameWindowWidth = MinecraftLaunchOptions.DefaultWindowWidth.ToString(System.Globalization.CultureInfo.InvariantCulture);

    [ObservableProperty]
    private string customGameWindowHeight = MinecraftLaunchOptions.DefaultWindowHeight.ToString(System.Globalization.CultureInfo.InvariantCulture);

    [ObservableProperty]
    private bool usesCustomGameWindowSize;

    [ObservableProperty]
    private MinecraftMemoryAllocationModeOption selectedMemoryAllocationMode = new(
        MinecraftMemoryAllocationMode.Automatic,
        "自动分配");

    [ObservableProperty]
    private string customMemoryMiB = MinecraftLaunchOptions.DefaultCustomMemoryMiB.ToString(System.Globalization.CultureInfo.InvariantCulture);

    [ObservableProperty]
    private bool usesCustomMemoryAllocation;

    [ObservableProperty]
    private bool usesAutomaticMemoryAllocation = true;

    [ObservableProperty]
    private double customMemorySliderValue = 15;

    [ObservableProperty]
    private double customMemorySliderMaximum = 49;

    [ObservableProperty]
    private string memoryUsedDisplay = "0.0 GiB";

    [ObservableProperty]
    private string memoryTotalDisplay = " / 0.0 GiB";

    [ObservableProperty]
    private string memoryGameDisplay = "3.0 GiB";

    [ObservableProperty]
    private GridLength memoryUsedWidth = new(1, GridUnitType.Star);

    [ObservableProperty]
    private GridLength memoryGameWidth = new(1, GridUnitType.Star);

    [ObservableProperty]
    private GridLength memoryEmptyWidth = new(1, GridUnitType.Star);

    [ObservableProperty]
    private string launchOptionsSummary = "正在读取本地启动选项…";

    [ObservableProperty]
    private string javaRequirementSummary = "将在读取版本元数据后检查 Java 版本要求。";

    [ObservableProperty]
    private string memoryAllocationSummary = "将在读取版本元数据和系统内存后计算堆大小。";

    [ObservableProperty]
    private string operatingSystem = "正在读取系统信息…";

    [ObservableProperty]
    private string architecture = "—";

    [ObservableProperty]
    private string runtime = "—";

    [ObservableProperty]
    private string applicationDataDirectory = "—";

    [ObservableProperty]
    private string cacheDirectory = "—";

    [ObservableProperty]
    private string javaSummary = "正在扫描 Java…";

    [ObservableProperty]
    private JavaInstallation? selectedJava;

    [ObservableProperty]
    private bool hasAvailableJavaInstallations;

    [ObservableProperty]
    private string instanceSummary = "正在扫描本地实例…";

    [ObservableProperty]
    private MinecraftInstance? selectedInstance;

    [ObservableProperty]
    private bool hasAvailableInstances;

    public bool NeedsGameDownload => !HasAvailableInstances;

    [ObservableProperty]
    private string versionMetadataSummary = "将在发现本地实例后读取版本元数据。";

    [ObservableProperty]
    private string downloadPreparationSummary = "版本下载计划尚未生成。";

    [ObservableProperty]
    private string launchArgumentSummary = "正在等待版本元数据与账户信息。";

    [ObservableProperty]
    private string classpathSummary = "正在等待版本元数据。";

    [ObservableProperty]
    private string gameLaunchSummary = "正在检查游戏进程启动条件。";

    [ObservableProperty]
    private string gameDirectorySummary = "仅在点击“打开游戏目录”后调用系统文件管理器；不会创建目录。";

    [ObservableProperty]
    private string auroraReleaseSummary = "发行版本与更新说明由 PCL Aurora 的 GitHub Releases 提供。";

    [ObservableProperty]
    private string gameLogSummary = "尚未启动游戏，本次会话没有可查看的进程输出。";

    [ObservableProperty]
    private string toolboxStatusText = "常用工具已就绪。";

    [ObservableProperty]
    private bool isToolboxCacheClearing;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ToolboxLaunchCountDisplay))]
    private int toolboxLaunchCount;

    public string ToolboxLaunchCountDisplay => $"已启动 {ToolboxLaunchCount} 次";

    public bool IsWindowsToolboxCapability => System.OperatingSystem.IsWindows();

    public string ToolboxMemoryOptimizationSummary => System.OperatingSystem.IsWindows()
        ? "Windows 可提供工作集优化；启动参数中的锁定内存仍可跨平台使用。"
        : "当前平台不使用 Windows 工作集压缩；可在启动设置中启用锁定内存分配（-Xms = -Xmx）。";

    public bool HasToolboxCacheDirectory =>
        !string.IsNullOrWhiteSpace(CacheDirectory) &&
        !string.Equals(CacheDirectory, "—", StringComparison.Ordinal) &&
        Directory.Exists(CacheDirectory);

    [ObservableProperty]
    private bool hasGameLogLines;

    [ObservableProperty]
    private bool isLoadingFeedback;

    [ObservableProperty]
    private bool hasFeedbackGroups;

    [ObservableProperty]
    private bool hasFeedbackLoadError;

    [ObservableProperty]
    private string feedbackStatusText = "正在获取反馈列表";

    [ObservableProperty]
    private bool isLoadingLauncherLogs;

    [ObservableProperty]
    private bool hasLauncherLogFiles;

    [ObservableProperty]
    private string launcherLogStatusText = "正在读取日志列表";

    [ObservableProperty]
    private bool canLaunchGame;

    [ObservableProperty]
    private bool canInstallGame;

    [ObservableProperty]
    private bool isInstallationRunning;

    [ObservableProperty]
    private bool canCancelInstallation;

    [ObservableProperty]
    private string installationSummary = "选择本地实例后可查看安装计划。";

    [ObservableProperty]
    private MinecraftVersionCatalogEntry? selectedCatalogVersion;

    [ObservableProperty]
    private MinecraftVersionCatalogEntry? latestReleaseVersion;

    [ObservableProperty]
    private MinecraftVersionCatalogEntry? latestSnapshotVersion;

    [ObservableProperty]
    private string versionSearchText = string.Empty;

    [ObservableProperty]
    private MinecraftVersionCatalogCategory selectedVersionCategory = MinecraftVersionCatalogCategory.Release;

    [ObservableProperty]
    private bool canProvisionSelectedVersion;

    [ObservableProperty]
    private string versionCatalogSummary = "进入原版下载页后将自动加载可选版本。";

    [ObservableProperty]
    private bool isVersionCatalogLoading;

    [ObservableProperty]
    private string loaderCatalogPath = string.Empty;

    [ObservableProperty]
    private string loaderCatalogSummary = "可导入本地加载器目录 JSON。";

    [ObservableProperty]
    private bool isOfficialLoaderCatalogLoading;

    [ObservableProperty]
    private string loaderSelectionSummary = "请先导入目录并选择一个 Minecraft 版本；不会下载或执行安装器。";

    [ObservableProperty]
    private bool isLoaderDirectoryLoading;

    [ObservableProperty]
    private bool isLoaderPackageDownloading;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasLoaderDirectorySummary))]
    private string loaderDirectorySummary = string.Empty;

    [ObservableProperty]
    private bool hasAvailableLoaders;

    [ObservableProperty]
    private MinecraftLoaderCatalogEntry? selectedLoader;

    [ObservableProperty]
    private bool canInstallSelectedLoader;

    [ObservableProperty]
    private bool isCombinedInstallerLoading;

    [ObservableProperty]
    private bool canStartCombinedInstallation;

    [ObservableProperty]
    private string combinedInstallationName = string.Empty;

    [ObservableProperty]
    private string combinedInstallationSummary = "选择 Minecraft 版本后可添加安装组件。";

    [ObservableProperty]
    private string communitySearchText = string.Empty;

    [ObservableProperty]
    private string communityFavoriteSearchText = string.Empty;

    [ObservableProperty]
    private CommunityFavoriteFolder? selectedCommunityFavoriteFolder;

    [ObservableProperty]
    private bool isCommunityFavoritesPage;

    [ObservableProperty]
    private string communityFavoriteEmptyTitle = "还没有收藏内容";

    [ObservableProperty]
    private string communityFavoriteEmptyDescription = "在资源详细信息界面中可以点击收藏按钮进行收藏";

    [ObservableProperty]
    private string communityGameVersion = string.Empty;

    [ObservableProperty]
    private CommunityResourceSortOption selectedCommunityResourceSort = new(CommunityResourceSort.Default, "默认");

    [ObservableProperty]
    private IReadOnlyList<CommunityResourceCategoryOption> communityResourceCategoryOptions =
        [AllCommunityResourceCategories];

    [ObservableProperty]
    private CommunityResourceCategoryOption selectedCommunityResourceCategory = AllCommunityResourceCategories;

    [ObservableProperty]
    private IReadOnlyList<CommunityResourceLoaderOption> communityResourceLoaderOptions = ModLoaderOptions;

    [ObservableProperty]
    private CommunityResourceLoaderOption selectedCommunityResourceLoader = ModLoaderOptions[0];

    [ObservableProperty]
    private CommunityResourceItemViewModel? selectedCommunityResource;

    [ObservableProperty]
    private CommunityResourceVersion? selectedCommunityResourceVersion;

    [ObservableProperty]
    private string communityResourceSummary = "选择社区资源类型后可使用公开目录搜索。";

    [ObservableProperty]
    private string communityLoadingText = "正在获取资源列表…";

    [ObservableProperty]
    private bool hasCommunityResources;

    [ObservableProperty]
    private bool isCommunitySearchRunning;

    [ObservableProperty]
    private bool canCancelCommunitySearch;

    [ObservableProperty]
    private bool canSearchCommunityResources;

    [ObservableProperty]
    private bool isCommunityCatalogAvailable;

    [ObservableProperty]
    private bool canOpenCommunityResource;

    [ObservableProperty]
    private bool isCommunityVersionLoading;

    [ObservableProperty]
    private bool hasCommunityResourceVersions;

    [ObservableProperty]
    private bool canLoadCommunityResourceVersions;

    [ObservableProperty]
    private bool canDownloadCommunityResource;

    [ObservableProperty]
    private bool canCancelCommunityResourceOperation;

    [ObservableProperty]
    private bool canTranslateCommunityDescription;

    [ObservableProperty]
    private bool isCommunityDescriptionTranslationRunning;

    [ObservableProperty]
    private bool isCommunityDescriptionTranslationPanelVisible;

    [ObservableProperty]
    private string communityDescriptionTranslation = string.Empty;

    [ObservableProperty]
    private bool hasCommunityDescriptionTranslation;

    [ObservableProperty]
    private string communityDescriptionTranslationSummary = string.Empty;

    [ObservableProperty]
    private string communityVersionSummary = "选择项目后可查看适合当前实例的版本。";

    [ObservableProperty]
    private bool canGoToPreviousCommunityPage;

    [ObservableProperty]
    private bool canGoToNextCommunityPage;

    [ObservableProperty]
    private bool isCommunityLoaderFilterVisible;

    [ObservableProperty]
    private int communityPage;

    public int CommunityPageNumber => CommunityPage + 1;

    public bool IsCommunityResultListVisible =>
        !IsCommunityFavoritesPage && HasCommunityResources && !IsCommunitySearchRunning;

    public bool IsCommunityFavoriteListVisible =>
        IsCommunityFavoritesPage && HasCommunityResources && !IsCommunitySearchRunning;

    public bool IsCommunityFavoriteEmptyVisible =>
        IsCommunityFavoritesPage && !HasCommunityResources && !IsCommunitySearchRunning;

    public bool IsCommunityFavoriteSearchVisible =>
        SelectedCommunityFavoriteFolder?.Projects.Count > 0;

    public bool IsCommunityFooterVisible => IsCommunityCatalogAvailable && !IsCommunitySearchRunning;

    public bool IsCommunityStatusVisible =>
        !IsCommunitySearchRunning && !HasCommunityResources && !string.IsNullOrWhiteSpace(CommunityResourceSummary);

    public bool IsCommunityVersionCardVisible => SelectedCommunityResource is not null && !IsCommunitySearchRunning;

    public bool IsCommunityVersionFilterVisible =>
        CommunityGameVersionFilters.Count > 2 || CommunityLoaderVersionFilters.Count > 2;

    public bool IsCommunityGameVersionFilterVisible => CommunityGameVersionFilters.Count > 2;

    public bool IsCommunityLoaderVersionFilterVisible => CommunityLoaderVersionFilters.Count > 2;

    public bool IsLoaderPageLoading => IsLoaderDirectoryLoading;

    public bool HasLoaderDirectorySummary => !string.IsNullOrWhiteSpace(LoaderDirectorySummary);

    public Task LoadVersionCatalogPageAsync() => RefreshVersionCatalogAsync();

    public Task LoadOfficialLoaderCatalogPageAsync() => RefreshOfficialLoaderCatalogAsync();

    public async Task LoadOfficialLoaderDirectoryPageAsync(MinecraftLoaderKind kind)
    {
        var displayName = GetLoaderDisplayName(kind);
        loaderDirectoryCancellation?.Cancel();
        loaderDirectoryCancellation?.Dispose();
        loaderDirectoryCancellation = new();
        var cancellationToken = loaderDirectoryCancellation.Token;
        IsLoaderDirectoryLoading = true;
        LoaderDirectoryGroups.Clear();
        LoaderDirectorySummary = string.Empty;
        try
        {
            var result = await officialLoaderCatalogService.FetchDirectoryAsync(kind, cancellationToken);
            if (result.Directory is null)
            {
                LoaderDirectorySummary = string.Join(Environment.NewLine, result.Errors);
                return;
            }

            foreach (var group in result.Directory.Groups)
            {
                LoaderDirectoryGroups.Add(new(kind, group));
            }

            LoaderDirectorySummary = result.Errors.Count == 0
                ? string.Empty
                : string.Join(Environment.NewLine, result.Errors);
        }
        catch (OperationCanceledException)
        {
            LoaderDirectorySummary = string.Empty;
        }
        catch (Exception exception)
        {
            LoaderDirectorySummary = $"获取 {displayName} 列表失败：{exception.Message}";
        }
        finally
        {
            if (!cancellationToken.IsCancellationRequested)
            {
                IsLoaderDirectoryLoading = false;
            }
        }
    }

    public async Task LoadLoaderDirectoryGroupAsync(MinecraftLoaderDirectoryGroupViewModel group)
    {
        ArgumentNullException.ThrowIfNull(group);
        if (!group.IsLazy || group.IsLoaded || group.IsLoading)
        {
            return;
        }

        group.IsLoading = true;
        group.Error = string.Empty;
        try
        {
            var result = await officialLoaderCatalogService.FetchDirectoryGroupAsync(group.Kind, group.Key);
            var loadedGroup = result.Directory?.Groups.SingleOrDefault();
            if (loadedGroup is null)
            {
                group.IsLoading = false;
                group.Error = string.Join(Environment.NewLine, result.Errors);
                return;
            }

            group.ReplaceEntries(loadedGroup.Entries);
        }
        catch (OperationCanceledException)
        {
            group.IsLoading = false;
        }
        catch (Exception exception)
        {
            group.IsLoading = false;
            group.Error = $"获取版本列表失败：{exception.Message}";
        }
    }

    public async Task SaveLoaderPackageAsync(MinecraftLoaderPackageEntry package, string destinationFile)
    {
        if (IsLoaderPackageDownloading)
        {
            return;
        }

        var displayName = GetLoaderDisplayName(package.Kind);
        IsLoaderPackageDownloading = true;
        LoaderDirectorySummary = $"正在下载 {displayName} {package.DisplayName}…";
        try
        {
            var savedPath = await loaderPackageDownloadService.DownloadAsync(package, destinationFile);
            LoaderDirectorySummary = $"{displayName} {package.DisplayName} 已保存到 {savedPath}。";
        }
        catch (OperationCanceledException)
        {
            LoaderDirectorySummary = "安装包下载已取消。";
        }
        catch (Exception exception)
        {
            LoaderDirectorySummary = $"安装包下载失败：{exception.Message}";
        }
        finally
        {
            IsLoaderPackageDownloading = false;
        }
    }

    public async Task OpenLoaderPackageChangelogAsync(MinecraftLoaderPackageEntry package)
    {
        if (package.ChangelogUri is null)
        {
            return;
        }

        try
        {
            await openPathService.OpenUriAsync(package.ChangelogUri);
        }
        catch (Exception exception)
        {
            LoaderDirectorySummary = $"无法打开更新日志：{exception.Message}";
        }
    }

    public async Task OpenLoaderWebsiteAsync(MinecraftLoaderKind kind)
    {
        var uri = kind switch
        {
            MinecraftLoaderKind.Forge => new Uri("https://files.minecraftforge.net"),
            MinecraftLoaderKind.NeoForge => new Uri("https://neoforged.net"),
            MinecraftLoaderKind.Fabric => new Uri("https://www.fabricmc.net"),
            MinecraftLoaderKind.OptiFine => new Uri("https://www.optifine.net"),
            MinecraftLoaderKind.Cleanroom => new Uri("https://cleanroommc.com/zh/"),
            MinecraftLoaderKind.LegacyFabric => new Uri("https://legacyfabric.net/"),
            MinecraftLoaderKind.LabyMod => new Uri("https://www.labymod.net/"),
            MinecraftLoaderKind.LiteLoader => new Uri("https://www.liteloader.com/"),
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
        };
        try
        {
            await openPathService.OpenUriAsync(uri);
        }
        catch (Exception exception)
        {
            LoaderDirectorySummary = $"无法打开官方网站：{exception.Message}";
        }
    }

    private static string GetLoaderDisplayName(MinecraftLoaderKind kind) => kind switch
    {
        MinecraftLoaderKind.LegacyFabric => "Legacy Fabric",
        _ => kind.ToString(),
    };

    public async Task SaveSelectedVersionServerAsync(string destinationFile)
    {
        if (SelectedCatalogVersion is null)
        {
            return;
        }

        try
        {
            VersionCatalogSummary = $"正在下载 {SelectedCatalogVersion.Id} 服务端…";
            await versionArchiveService.SaveServerAsync(SelectedCatalogVersion, destinationFile);
            VersionCatalogSummary = $"服务端已保存到 {destinationFile}。";
        }
        catch (OperationCanceledException)
        {
            VersionCatalogSummary = "服务端下载已取消。";
        }
        catch (Exception exception)
        {
            VersionCatalogSummary = $"服务端下载失败：{exception.Message}";
        }
    }

    public async Task OpenSelectedVersionChangelogAsync()
    {
        if (SelectedCatalogVersion is null)
        {
            return;
        }

        try
        {
            await openPathService.OpenUriAsync(CreateMinecraftWikiUri(SelectedCatalogVersion.Id));
            VersionCatalogSummary = $"已打开 {SelectedCatalogVersion.Id} 的更新日志。";
        }
        catch (Exception exception)
        {
            VersionCatalogSummary = $"无法打开更新日志：{exception.Message}";
        }
    }

    public Task LoadCommunityResourcePageAsync() =>
        isCommunityFavoritesSection ? LoadFavoriteResourcesAsync() : LoadCommunityResourcesAsync(0);

    [ObservableProperty]
    private string offlinePlayerName = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsOfflineAccountMode))]
    private bool isMicrosoftAccountMode;

    public bool IsOfflineAccountMode => !IsMicrosoftAccountMode;

    [ObservableProperty]
    private string accountSummary = "未选择账户。可创建只在本次会话使用的离线账户。";

    [ObservableProperty]
    private string accountLicenseGuidance = "选择账户后会显示正版购买与上游赞助提示。";

    [ObservableProperty]
    private bool requiresAccountGuidance;

    [ObservableProperty]
    private bool hasAcknowledgedAccountGuidance;

    [ObservableProperty]
    private string microsoftLoginSummary = "Microsoft 登录尚未开始。";

    [ObservableProperty]
    private string microsoftDeviceCode = "—";

    [ObservableProperty]
    private bool isMicrosoftLoginRunning;

    [ObservableProperty]
    private bool hasMicrosoftDeviceCode;

    [ObservableProperty]
    private bool canOpenMicrosoftVerificationPage;

    [ObservableProperty]
    private bool hasMicrosoftAccountProfile;

    [ObservableProperty]
    private string microsoftAccountDisplayName = "尚未登录";

    [ObservableProperty]
    private bool canStartMicrosoftLogin;

    [ObservableProperty]
    private bool canRestoreMicrosoftLogin;

    [ObservableProperty]
    private bool canCancelMicrosoftLogin;

    [ObservableProperty]
    private bool canClearMicrosoftLogin;

    [ObservableProperty]
    private string launchPreflightSummary = "正在检查启动条件…";

    [RelayCommand]
    private async Task RefreshAsync()
    {
        var shouldPersistSelectedInstance = false;
        try
        {
            isRefreshing = true;
            var diagnosticsTask = diagnosticsService.GetAsync();
            var instancesTask = instanceCatalogService.GetAllAsync();
            await Task.WhenAll(diagnosticsTask, instancesTask);

            var diagnostics = await diagnosticsTask;
            var instances = await instancesTask;
            var selectedDirectory = SelectedInstance?.DirectoryPath;
            var preferredInstanceName = SelectedInstance?.Name ?? currentPreferences.SelectedInstanceName;
            AvailableInstances.Clear();
            foreach (var instance in instances.Where(instance => instance.Status == MinecraftInstanceStatus.Valid))
            {
                AvailableInstances.Add(instance);
            }

            HasAvailableInstances = AvailableInstances.Count > 0;
            SelectedInstance = AvailableInstances.FirstOrDefault(instance => instance.DirectoryPath == selectedDirectory)
                ?? AvailableInstances.FirstOrDefault(instance => instance.Name == preferredInstanceName)
                ?? AvailableInstances.FirstOrDefault();
            shouldPersistSelectedInstance = !string.Equals(
                currentPreferences.SelectedInstanceName,
                SelectedInstance?.Name,
                StringComparison.Ordinal);
            var selectedJavaPath = SelectedJava?.ExecutablePath;
            var discoveredJava = diagnostics.JavaInstallations.ToList();
            foreach (var executablePath in currentPreferences.EffectiveManualJavaExecutablePaths)
            {
                var manualJava = await javaInstallationInspector.InspectAsync(executablePath);
                if (manualJava is not null)
                {
                    discoveredJava.Add(manualJava);
                }
            }

            var uniqueJava = discoveredJava
                .GroupBy(java => java.ExecutablePath, StringComparer.OrdinalIgnoreCase)
                .Select(group => group.OrderByDescending(java => java.Source == JavaSource.Manual).First())
                .ToArray();
            AvailableJavaInstallations.Clear();
            foreach (var java in uniqueJava.Where(java => java.IsCompatible))
            {
                AvailableJavaInstallations.Add(java);
            }

            HasAvailableJavaInstallations = AvailableJavaInstallations.Count > 0;
            SelectedJava = AvailableJavaInstallations.FirstOrDefault(java => java.ExecutablePath == selectedJavaPath)
                ?? AvailableJavaInstallations.FirstOrDefault();
            OperatingSystem = $"{diagnostics.Platform.OperatingSystem} ({diagnostics.Platform.Version})";
            Architecture = diagnostics.Platform.Architecture.ToString();
            Runtime = diagnostics.Platform.RuntimeVersion;
            ApplicationDataDirectory = diagnostics.Paths.ApplicationDataDirectory;
            CacheDirectory = diagnostics.Paths.CacheDirectory;
            OnPropertyChanged(nameof(HasToolboxCacheDirectory));
            JavaSummary = uniqueJava.Length == 0
                ? "未发现可用 Java。"
                : $"发现 {uniqueJava.Length} 个 Java，其中 {AvailableJavaInstallations.Count} 个与当前架构兼容。";
            InstanceSummary = instances.Count == 0
                ? "未在 macOS 默认 Minecraft 目录中发现实例。"
                : $"发现 {instances.Count} 个本地实例，其中 {instances.Count(instance => instance.Status == MinecraftInstanceStatus.Valid)} 个可读取版本元数据。";
            await RefreshSelectedInstanceStateAsync();
        }
        catch (Exception exception)
        {
            JavaSummary = $"Java 扫描失败：{exception.Message}";
            InstanceSummary = $"实例扫描失败：{exception.Message}";
            VersionMetadataSummary = "无法读取版本元数据。";
            DownloadPreparationSummary = "无法生成下载计划。";
            LaunchArgumentSummary = "无法准备启动参数。";
            ClasspathSummary = "无法解析类路径。";
            GameLaunchSummary = "无法检查游戏进程启动条件。";
            CanLaunchGame = false;
            CanInstallGame = false;
        }
        finally
        {
            isRefreshing = false;
        }

        if (shouldPersistSelectedInstance)
        {
            _ = SaveSelectedInstancePreferenceAsync(SelectedInstance?.Name);
        }
    }

    public async Task InitializeAsync()
    {
        try
        {
            await launcherLogService.InitializeAsync();
            await launcherLogService.AppendAsync("Launcher", "开始初始化启动器界面。");
        }
        catch
        {
            // Logging must never prevent the launcher from opening.
        }

        await LoadPreferencesAsync();
        await LoadCommunityFavoritesAsync();
        await RefreshAsync();

        try
        {
            await launcherLogService.AppendAsync("Launcher", "启动器界面初始化完成。");
        }
        catch
        {
            // Logging must never prevent initialization from completing.
        }
    }

    public async Task<JavaInstallation> AddManualJavaAsync(string executablePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(executablePath);
        var installation = await javaInstallationInspector.InspectAsync(executablePath)
            ?? throw new InvalidOperationException("该文件不是可用的 Java 程序，或当前用户没有执行权限。");
        if (!installation.IsCompatible)
        {
            throw new InvalidOperationException(
                $"该 Java 的架构为 {installation.Architecture}，与当前系统架构不兼容。");
        }

        if (AvailableJavaInstallations.Any(java => string.Equals(
                java.ExecutablePath,
                installation.ExecutablePath,
                StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException("该 Java 已经存在于列表中。");
        }

        var paths = currentPreferences.EffectiveManualJavaExecutablePaths
            .Append(installation.ExecutablePath)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        await preferencesService.SaveManualJavaExecutablePathsAsync(paths);
        currentPreferences = preferencesService.Current;
        await RefreshAsync();
        SelectedJava = AvailableJavaInstallations.FirstOrDefault(java =>
            string.Equals(java.ExecutablePath, installation.ExecutablePath, StringComparison.OrdinalIgnoreCase));
        return installation;
    }

    private async Task LoadPreferencesAsync()
    {
        try
        {
            isLoadingPreferences = true;
            var result = await preferencesService.LoadAsync();
            currentPreferences = result.Preferences;
            ToolboxLaunchCount = result.Preferences.LaunchCount;
            var option = ThemeModes.Single(item => item.Mode == result.Preferences.ThemeMode);
            SelectedThemeMode = option;
            themeService.Apply(option.Mode);
            ThemeSummary = result.Warning ?? $"当前使用{option.DisplayName}主题；该偏好已保存到本机。";
            ApplyInterfaceSettings(result.Preferences.EffectiveInterfaceSettings);
            ApplyLocalizationSettings(result.Preferences.EffectiveLocalizationSettings);
            await ApplyMiscSettingsAsync(result.Preferences.EffectiveMiscSettings);
            var updateSettings = result.Preferences.EffectiveUpdateSettings;
            SelectedUpdateChannel = UpdateChannels.Single(option => option.Value == updateSettings.Channel);
            SelectedAutoUpdateBehavior = AutoUpdateBehaviors.Single(option => option.Value == updateSettings.AutoUpdateBehavior);
            SelectedDownloadConcurrency = result.Preferences.DownloadConcurrency;
            SelectedDownloadSpeedLimitStep = result.Preferences.DownloadSpeedLimitStep;
            var managementOptions = result.Preferences.EffectiveGameManagementOptions;
            SelectedFileSource = FileSourceOptions.Single(option => option.Value == managementOptions.FileSource);
            SelectedVersionListSource = VersionListSourceOptions.Single(option => option.Value == managementOptions.VersionListSource);
            AutoSelectNewInstance = managementOptions.AutoSelectNewInstance;
            FixAuthlib = managementOptions.FixAuthlib;
            SelectedCommunitySource = CommunitySourceOptions.Single(option => option.Value == managementOptions.CommunitySource);
            SelectedCommunityFileNameFormat = CommunityFileNameFormatOptions.Single(option => option.Value == managementOptions.CommunityFileNameFormat);
            SelectedCommunityModNameStyle = CommunityModNameStyleOptions.Single(option => option.Value == managementOptions.CommunityModNameStyle);
            SelectedCommunityQuickDownloadBehavior = CommunityQuickDownloadBehaviorOptions.Single(option => option.Value == managementOptions.QuickDownloadBehavior);
            IgnoreQuilt = managementOptions.IgnoreQuilt;
            AutoInstallDependencies = managementOptions.AutoInstallDependencies;
            ReleaseNotifications = managementOptions.ReleaseNotifications;
            SnapshotNotifications = managementOptions.SnapshotNotifications;
            AutoChangeGameLanguage = managementOptions.AutoChangeGameLanguage;
            ReadClipboard = managementOptions.ReadClipboard;
            var launchOptions = result.Preferences.EffectiveLaunchOptions;
            AdditionalJvmArguments = launchOptions.AdditionalJvmArguments ?? string.Empty;
            AdditionalGameArguments = launchOptions.AdditionalGameArguments ?? string.Empty;
            SelectedInstanceIsolationMode = InstanceIsolationModes.Single(option => option.Mode == launchOptions.InstanceIsolationMode);
            WindowTitle = launchOptions.WindowTitle ?? string.Empty;
            CustomInfo = launchOptions.CustomInfo ?? string.Empty;
            SelectedLauncherVisibility = LauncherVisibilityModes.Single(option => option.Mode == launchOptions.LauncherVisibility);
            SelectedGameProcessPriority = GameProcessPriorities.Single(option => option.Priority == launchOptions.ProcessPriority);
            SelectedPreferredIpStack = PreferredIpStacks.Single(option => option.Stack == launchOptions.PreferredIpStack);
            SelectedRendererMode = RendererModes.Single(option => option.Mode == launchOptions.Renderer);
            PreLaunchCommand = launchOptions.PreLaunchCommand ?? string.Empty;
            WaitForPreLaunchCommand = launchOptions.WaitForPreLaunchCommand;
            DisableJavaLaunchWrapper = launchOptions.DisableJavaLaunchWrapper;
            DisableLegacyFix = launchOptions.DisableLegacyFix;
            PreferDedicatedGpu = launchOptions.PreferDedicatedGpu;
            UseJavaExecutable = launchOptions.UseJavaExecutable;
            DisableLwjglUnsafeAgent = launchOptions.DisableLwjglUnsafeAgent;
            DisableCrashAnalysis = launchOptions.DisableCrashAnalysis;
            LockMemory = launchOptions.LockMemory;
            SelectedGameWindowMode = GameWindowModes.Single(option => option.Mode == launchOptions.WindowMode);
            CustomGameWindowWidth = launchOptions.WindowWidth.ToString(System.Globalization.CultureInfo.InvariantCulture);
            CustomGameWindowHeight = launchOptions.WindowHeight.ToString(System.Globalization.CultureInfo.InvariantCulture);
            UsesCustomGameWindowSize = launchOptions.WindowMode == MinecraftGameWindowMode.Custom;
            SelectedMemoryAllocationMode = MemoryAllocationModes.Single(option => option.Mode == launchOptions.MemoryAllocationMode);
            CustomMemoryMiB = launchOptions.CustomMemoryMiB.ToString(System.Globalization.CultureInfo.InvariantCulture);
            CustomMemorySliderValue = MemoryMiBToSliderStep(launchOptions.CustomMemoryMiB);
            UsesCustomMemoryAllocation = launchOptions.MemoryAllocationMode == MinecraftMemoryAllocationMode.Custom;
            RefreshMemoryDisplay();
            LaunchOptionsSummary = result.Warning ?? GetLaunchOptionsSummary(launchOptions);
            RestoreOfflineAccount(result.Preferences.OfflinePlayerName);
            UpdateMicrosoftLoginAvailability(result.Preferences.MicrosoftAccount);
        }
        catch (Exception exception)
        {
            ThemeSummary = $"无法读取本地主题偏好：{exception.Message}；当前跟随系统主题。";
            LaunchOptionsSummary = "无法读取本地启动选项；已使用安全默认值。";
            JavaRequirementSummary = "无法读取版本元数据中的 Java 版本要求。";
            MemoryAllocationSummary = "无法读取内存分配设置。";
            MicrosoftLoginSummary = "无法读取本地 Microsoft 账户档案。";
        }
        finally
        {
            isLoadingPreferences = false;
        }
    }

    private async Task RefreshSelectedInstanceStateAsync()
    {
        try
        {
            await RefreshVersionPreparationAsync();
            SelectJavaForCurrentRequirement();
            await RefreshLaunchArgumentPreparationAsync();
            UpdateLaunchPreflight();
            await RefreshGameLaunchPreparationAsync();
        }
        catch (Exception exception)
        {
            VersionMetadataSummary = $"无法读取版本元数据：{exception.Message}";
            DownloadPreparationSummary = "无法生成下载计划。";
            LaunchArgumentSummary = "无法准备启动参数。";
            ClasspathSummary = "无法解析类路径。";
            GameLaunchSummary = "无法检查游戏进程启动条件。";
            CanLaunchGame = false;
            CanInstallGame = false;
        }
    }

    private async Task RefreshVersionPreparationAsync()
    {
        if (SelectedInstance is null)
        {
            currentJavaRequirement = null;
            VersionMetadataSummary = "未选择可读取版本元数据的本地实例。";
            DownloadPreparationSummary = "需先发现有效本地实例；不会创建目录或下载文件。";
            InstallationSummary = "未选择可安装的本地实例。";
            CanInstallGame = false;
            CanInstallSelectedLoader = false;
            return;
        }

        var preparation = await versionPreparationService.PrepareAsync(SelectedInstance);
        if (!preparation.Inspection.IsSuccess || preparation.Inspection.EffectiveMetadata is null)
        {
            currentJavaRequirement = null;
            VersionMetadataSummary = string.Join(Environment.NewLine, preparation.Inspection.Errors);
            DownloadPreparationSummary = "版本元数据无效，未生成下载计划。";
            InstallationSummary = "版本元数据无效，无法开始下载。";
            CanInstallGame = false;
            return;
        }

        var metadata = preparation.Inspection.EffectiveMetadata;
        currentJavaRequirement = Pcl2MinecraftJavaRequirementEvaluator.Evaluate(metadata, SelectedInstance);
        VersionMetadataSummary = $"{metadata.Id} · {metadata.Type ?? "未知类型"} · 继承链：{string.Join(" → ", preparation.Inspection.InheritanceChain.Select(item => item.Id))}";
        DownloadPreparationSummary = preparation.DownloadPlan.IsReady
            ? $"已生成 {preparation.DownloadPlan.Artifacts.Count} 个游戏与库下载计划项；等待用户确认安装。"
            : string.Join(Environment.NewLine, preparation.DownloadPlan.BlockingReasons);
        CanInstallGame = preparation.DownloadPlan.IsReady;
        InstallationSummary = preparation.DownloadPlan.IsReady
            ? "点击“安装所选本地实例”后才会下载游戏文件、支持库、资源索引和资源对象。"
            : "安装计划不完整，无法开始下载。";
    }

    private async Task RefreshLaunchArgumentPreparationAsync()
    {
        if (SelectedInstance is null)
        {
            LaunchArgumentSummary = "未选择可读取版本元数据的本地实例。";
            ClasspathSummary = "未选择可读取版本元数据的本地实例。";
            return;
        }

        var preparation = await launchPreparationService.PrepareAsync(SelectedInstance, selectedAccount, SelectedJava);
        ClasspathSummary = preparation.ClasspathInspection.IsReady
            ? $"已发现 {preparation.ClasspathInspection.Entries.Count} 个本地类路径条目。"
            : string.Join(
                Environment.NewLine,
                preparation.ClasspathInspection.BlockingReasons
                    .Concat(preparation.ClasspathInspection.MissingFiles.Select(file => $"缺少文件：{file}")));
        LaunchArgumentSummary = preparation.ArgumentPreparation.IsReady
            ? $"已准备 {preparation.ArgumentPreparation.Arguments!.JvmArguments.Count} 个 JVM 参数与 {preparation.ArgumentPreparation.Arguments.GameArguments.Count} 个游戏参数；等待进程启动条件检查。"
            : string.Join(Environment.NewLine, preparation.ArgumentPreparation.BlockingReasons);
        JavaRequirementSummary = GetJavaRequirementSummary(preparation.JavaRequirement, SelectedJava);
        MemoryAllocationSummary = GetMemoryAllocationSummary(preparation);
        RefreshMemoryDisplay(preparation.MemoryAllocation?.Allocation?.MaximumMemoryMiB);
    }

    private async Task RefreshGameLaunchPreparationAsync()
    {
        gameLaunchPreparation = await gameLaunchService.PrepareAsync(
            SelectedInstance,
            selectedAccount,
            SelectedJava,
            HasAcknowledgedAccountGuidance);
        AccountLicenseGuidance = gameLaunchPreparation.AccountGuidance.Message;
        RequiresAccountGuidance = gameLaunchPreparation.AccountGuidance.RequiresAcknowledgement;
        CanLaunchGame = gameLaunchPreparation.CanLaunch;
        GameLaunchSummary = gameLaunchPreparation.CanLaunch
            ? "启动条件和进程请求均已准备。点击“启动游戏”后将先安全准备 native 库，再启动 Java 进程。"
            : string.Join(Environment.NewLine, gameLaunchPreparation.BlockingReasons);
    }

    [RelayCommand]
    private async Task RefreshVersionCatalogAsync()
    {
        if (IsVersionCatalogLoading)
        {
            return;
        }

        IsVersionCatalogLoading = true;
        try
        {
            VersionCatalogSummary = "正在获取官方版本清单…";
            var result = await versionCatalogService.FetchAsync();
            if (result.Catalog is null)
            {
                allCatalogVersions.Clear();
                AvailableVersions.Clear();
                ReleaseVersions.Clear();
                SnapshotVersions.Clear();
                LegacyVersions.Clear();
                AprilFoolsVersions.Clear();
                SelectedCatalogVersion = null;
                LatestReleaseVersion = null;
                LatestSnapshotVersion = null;
                VersionCatalogSummary = string.Join(Environment.NewLine, result.Errors);
                return;
            }

            allCatalogVersions.Clear();
            allCatalogVersions.AddRange(result.Catalog.Versions);
            LatestReleaseVersion = allCatalogVersions.FirstOrDefault(version =>
                MinecraftVersionCatalogFilter.GetCategory(version) == MinecraftVersionCatalogCategory.Release);
            LatestSnapshotVersion = allCatalogVersions.FirstOrDefault(version =>
                MinecraftVersionCatalogFilter.GetCategory(version) == MinecraftVersionCatalogCategory.Snapshot);
            ApplyVersionFilters(result.Catalog.LatestRelease);
            await NotifyNewMinecraftVersionAsync(LatestReleaseVersion, snapshot: false);
            await NotifyNewMinecraftVersionAsync(LatestSnapshotVersion, snapshot: true);
        }
        catch (OperationCanceledException)
        {
            VersionCatalogSummary = "获取版本清单已取消。";
        }
        catch (Exception exception)
        {
            VersionCatalogSummary = $"获取版本清单失败：{exception.Message}";
        }
        finally
        {
            IsVersionCatalogLoading = false;
        }
    }

    private async Task NotifyNewMinecraftVersionAsync(
        MinecraftVersionCatalogEntry? version,
        bool snapshot)
    {
        if (version is null)
        {
            return;
        }

        var management = currentPreferences.EffectiveGameManagementOptions;
        if ((snapshot && !management.SnapshotNotifications) ||
            (!snapshot && !management.ReleaseNotifications))
        {
            return;
        }

        var previous = snapshot
            ? currentPreferences.LastNotifiedSnapshotVersion
            : currentPreferences.LastNotifiedReleaseVersion;
        if (string.Equals(previous, version.Id, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        VersionCatalogSummary = snapshot
            ? $"发现新的快照版 Minecraft：{version.Id}。"
            : $"发现新的正式版 Minecraft：{version.Id}。";
        try
        {
            await preferencesService.SaveLastNotifiedVersionAsync(snapshot, version.Id);
            currentPreferences = preferencesService.Current;
        }
        catch
        {
            // A notification is useful but must never make catalog refresh fail.
        }

        MinecraftVersionUpdateAvailable?.Invoke(this, version);
    }

    public async Task<CommunityResourceItemViewModel?> ResolveCommunityResourceLinkAsync(Uri uri)
    {
        ArgumentNullException.ThrowIfNull(uri);
        if (uri.Scheme != Uri.UriSchemeHttps)
        {
            return null;
        }

        var host = uri.Host.Trim().ToLowerInvariant();
        var segments = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (segments.Length < 2)
        {
            return null;
        }

        CommunityResourceType type;
        var isCurseForge = false;
        string slug;
        if (host is "modrinth.com" or "www.modrinth.com")
        {
            type = segments[0].ToLowerInvariant() switch
            {
                "mod" => CommunityResourceType.Mod,
                "modpack" => CommunityResourceType.ModPack,
                "datapack" => CommunityResourceType.DataPack,
                "resourcepack" => CommunityResourceType.ResourcePack,
                "shader" => CommunityResourceType.Shader,
                _ => throw new InvalidOperationException("该 Modrinth 链接不是支持的社区资源类型。"),
            };
            slug = segments[1];
        }
        else if ((host is "curseforge.com" or "www.curseforge.com") &&
                 segments.Length >= 3 && segments[0].Equals("minecraft", StringComparison.OrdinalIgnoreCase))
        {
            type = segments[1].ToLowerInvariant() switch
            {
                "mc-mods" => CommunityResourceType.Mod,
                "modpacks" => CommunityResourceType.ModPack,
                "data-packs" => CommunityResourceType.DataPack,
                "texture-packs" => CommunityResourceType.ResourcePack,
                "shaders" => CommunityResourceType.Shader,
                "worlds" => CommunityResourceType.World,
                _ => throw new InvalidOperationException("该 CurseForge 链接不是支持的社区资源类型。"),
            };
            isCurseForge = true;
            slug = segments[2];
        }
        else
        {
            return null;
        }

        var result = await (isCurseForge ? curseForgeCommunityResourceSearchService : communityResourceSearchService).SearchAsync(
            new CommunityResourceSearchRequest(type, slug, null, CommunityResourceLoader.Any,
                CommunityResourceSort.Relevance, 0, 20));
        var project = result.Projects.FirstOrDefault(item =>
            item.Slug.Equals(slug, StringComparison.OrdinalIgnoreCase) ||
            item.Title.Equals(slug, StringComparison.OrdinalIgnoreCase));
        return project is null ? null : new CommunityResourceItemViewModel(project)
        {
            IsFavorite = IsCommunityResourceFavorite(project.Id),
        };
    }

    [RelayCommand]
    private async Task ProvisionSelectedVersionAsync()
    {
        if (SelectedCatalogVersion is null)
        {
            VersionCatalogSummary = "请先选择官方版本；不会创建实例。";
            return;
        }

        try
        {
            CanProvisionSelectedVersion = false;
            VersionCatalogSummary = $"正在创建 {SelectedCatalogVersion.Id} 的本地实例元数据…";
            var instance = await versionProvisioningService.ProvisionAsync(SelectedCatalogVersion);
            VersionCatalogSummary = $"已创建 {instance.Name}。请点击“安装所选本地实例”下载游戏文件。";
            await RefreshAsync();
            if (currentPreferences.EffectiveGameManagementOptions.AutoSelectNewInstance)
            {
                SelectedInstance = AvailableInstances.FirstOrDefault(candidate =>
                    string.Equals(candidate.DirectoryPath, instance.DirectoryPath, StringComparison.Ordinal));
            }
        }
        catch (OperationCanceledException)
        {
            VersionCatalogSummary = "创建实例已取消。";
        }
        catch (Exception exception)
        {
            VersionCatalogSummary = $"创建实例失败：{exception.Message}";
        }
        finally
        {
            CanProvisionSelectedVersion = SelectedCatalogVersion is not null;
        }
    }

    public async Task InstallSelectedOfficialVersionAsync(string minecraftRootDirectory)
    {
        if (SelectedCatalogVersion is null || IsInstallationRunning)
        {
            return;
        }

        var version = SelectedCatalogVersion;
        try
        {
            var rootDirectory = Path.GetFullPath(minecraftRootDirectory);
            await InstallSelectedOfficialVersionCoreAsync(version, rootDirectory);
        }
        catch (OperationCanceledException)
        {
            InstallationSummary = "安装已取消。";
        }
        catch (Exception exception)
        {
            InstallationSummary = $"安装失败：{exception.Message}";
        }
    }

    public async Task PrepareCombinedInstallerAsync(MinecraftVersionCatalogEntry version)
    {
        ArgumentNullException.ThrowIfNull(version);
        SelectedCatalogVersion = version;
        CombinedInstallationName = version.Id;
        CombinedInstallationSummary = $"正在获取 Minecraft {version.Id} 的可选组件…";
        CanStartCombinedInstallation = false;
        IsCombinedInstallerLoading = true;
        foreach (var component in CombinedInstallComponents)
        {
            component.ResetForLoading();
        }

        try
        {
            var result = await officialLoaderCatalogService.FetchAsync(version.Id);
            loaderCatalog = result.Catalog;
            var failure = result.Errors.Count == 0
                ? $"没有适用于 Minecraft {version.Id} 的版本"
                : string.Join("；", result.Errors);
            foreach (var component in CombinedInstallComponents)
            {
                var entries = result.Catalog is null
                    ? []
                    : MinecraftLoaderCatalogFilter.ForMinecraftVersion(result.Catalog, version.Id, component.Kind);
                component.ReplaceVersions(entries, failure);
            }

            UpdateCombinedInstallationSelection();
            CombinedInstallationSummary = result.Catalog is null
                ? failure
                : result.Errors.Count == 0
                    ? "可以直接安装原版游戏，或展开下方卡片添加组件。"
                    : $"部分组件目录未能获取：{failure}";
        }
        catch (OperationCanceledException)
        {
            CombinedInstallationSummary = "获取安装组件已取消。";
            throw;
        }
        catch (Exception exception)
        {
            foreach (var component in CombinedInstallComponents)
            {
                component.ReplaceVersions([], "获取版本列表失败");
            }
            CombinedInstallationSummary = $"获取安装组件失败：{exception.Message}";
        }
        finally
        {
            IsCombinedInstallerLoading = false;
            CanStartCombinedInstallation = SelectedCatalogVersion is not null;
        }
    }

    public void SelectCombinedInstallComponent(MinecraftLoaderCatalogEntry loader)
    {
        ArgumentNullException.ThrowIfNull(loader);
        var target = CombinedInstallComponents.Single(component => component.Kind == loader.Kind);
        target.SelectedVersion = loader;
        target.IsExpanded = false;

        if (loader.Kind is MinecraftLoaderKind.Forge or MinecraftLoaderKind.NeoForge or MinecraftLoaderKind.Fabric)
        {
            foreach (var component in CombinedInstallComponents.Where(component =>
                         component.Kind is MinecraftLoaderKind.Forge or MinecraftLoaderKind.NeoForge or MinecraftLoaderKind.Fabric &&
                         component.Kind != loader.Kind))
            {
                component.SelectedVersion = null;
                component.IsExpanded = false;
            }
        }

        var optiFine = GetCombinedComponent(MinecraftLoaderKind.OptiFine);
        var forge = GetCombinedComponent(MinecraftLoaderKind.Forge);
        if (loader.Kind is MinecraftLoaderKind.NeoForge or MinecraftLoaderKind.Fabric)
        {
            optiFine.SelectedVersion = null;
            optiFine.IsExpanded = false;
        }
        else if (loader.Kind == MinecraftLoaderKind.OptiFine)
        {
            GetCombinedComponent(MinecraftLoaderKind.NeoForge).SelectedVersion = null;
            GetCombinedComponent(MinecraftLoaderKind.Fabric).SelectedVersion = null;
            if (forge.SelectedVersion is { } selectedForge && !IsOptiFineCompatibleWithForge(loader, selectedForge))
            {
                forge.SelectedVersion = null;
                forge.IsExpanded = false;
            }
        }
        else if (loader.Kind == MinecraftLoaderKind.Forge &&
                 optiFine.SelectedVersion is { } selectedOptiFine &&
                 !IsOptiFineCompatibleWithForge(selectedOptiFine, loader))
        {
            optiFine.SelectedVersion = null;
            optiFine.IsExpanded = false;
        }

        UpdateCombinedInstallationSelection();
    }

    public void ClearCombinedInstallComponent(MinecraftLoaderKind kind)
    {
        var component = GetCombinedComponent(kind);
        component.SelectedVersion = null;
        component.IsExpanded = false;
        UpdateCombinedInstallationSelection();
    }

    public async Task InstallSelectedCombinedVersionAsync(string minecraftRootDirectory)
    {
        if (SelectedCatalogVersion is not { } version || IsInstallationRunning)
        {
            return;
        }

        try
        {
            var rootDirectory = Path.GetFullPath(minecraftRootDirectory);
            var baseInstalled = await InstallSelectedOfficialVersionCoreAsync(version, rootDirectory);
            if (!baseInstalled)
            {
                return;
            }

            var selectedLoaders = GetSelectedCombinedLoaders();
            foreach (var loader in selectedLoaders)
            {
                CombinedInstallationSummary = $"正在准备 {loader.Kind} {loader.Version}…";
                var plan = await loaderInstallerService.PrepareAsync(loader, rootDirectory, SelectedJava);
                if (!plan.CanInstall)
                {
                    CombinedInstallationSummary = string.Join(Environment.NewLine, plan.BlockingReasons);
                    return;
                }

                var result = await loaderInstallerService.InstallAsync(
                    plan,
                    rootDirectory,
                    hasExplicitUserConfirmation: true);
                if (!result.Succeeded)
                {
                    CombinedInstallationSummary = string.Join(
                        Environment.NewLine,
                        result.Errors.Concat(result.Output.TakeLast(5).Select(line => line.Text)));
                    return;
                }
            }

            CombinedInstallationSummary = selectedLoaders.Count == 0
                ? $"Minecraft {version.Id} 安装完成。"
                : $"Minecraft {version.Id} 与 {string.Join(" + ", selectedLoaders.Select(loader => $"{loader.Kind} {loader.Version}"))} 安装完成。";
            InstallationSummary = CombinedInstallationSummary;
        }
        catch (OperationCanceledException)
        {
            CombinedInstallationSummary = "组合安装已取消。";
        }
        catch (Exception exception)
        {
            CombinedInstallationSummary = $"组合安装失败：{exception.Message}";
        }
    }

    private async Task<bool> InstallSelectedOfficialVersionCoreAsync(
        MinecraftVersionCatalogEntry version,
        string rootDirectory)
    {
        VersionCatalogSummary = $"正在 {rootDirectory} 创建 {version.Id}…";
        var instance = await versionProvisioningService.ProvisionAsync(version, rootDirectory);
        var autoSelect = currentPreferences.EffectiveGameManagementOptions.AutoSelectNewInstance;
        if (autoSelect)
        {
            try
            {
                isRefreshing = true;
                SelectedInstance = instance;
            }
            finally
            {
                isRefreshing = false;
            }

            await RefreshSelectedInstanceStateAsync();
        }

        var installed = await InstallGameCoreAsync(
            refreshDefaultInstanceCatalog: false,
            targetInstance: instance);
        if (installed && autoSelect)
        {
            await SaveSelectedInstancePreferenceAsync(instance.Name);
        }
        return installed;
    }

    private MinecraftInstallComponentViewModel GetCombinedComponent(MinecraftLoaderKind kind) =>
        CombinedInstallComponents.Single(component => component.Kind == kind);

    private IReadOnlyList<MinecraftLoaderCatalogEntry> GetSelectedCombinedLoaders()
    {
        var selected = CombinedInstallComponents
            .Where(component => component.SelectedVersion is not null)
            .Select(component => component.SelectedVersion!)
            .ToList();
        return selected
            .OrderBy(loader => loader.Kind == MinecraftLoaderKind.OptiFine ? 1 : 0)
            .ToArray();
    }

    private void UpdateCombinedInstallationSelection()
    {
        var version = SelectedCatalogVersion?.Id ?? "Minecraft";
        var selected = GetSelectedCombinedLoaders();
        CombinedInstallationName = selected.Count == 0
            ? version
            : $"{version}-{string.Join("-", selected.Select(loader => $"{loader.Kind}_{loader.Version}"))}";
        CombinedInstallationSummary = selected.Count == 0
            ? "未添加额外组件，将安装原版游戏。"
            : $"已选择 {string.Join(" + ", selected.Select(loader => $"{loader.Kind} {loader.Version}"))}。";
        CanStartCombinedInstallation = SelectedCatalogVersion is not null && !IsCombinedInstallerLoading;
    }

    private static bool IsOptiFineCompatibleWithForge(
        MinecraftLoaderCatalogEntry optiFine,
        MinecraftLoaderCatalogEntry forge)
    {
        if (optiFine.Kind != MinecraftLoaderKind.OptiFine ||
            forge.Kind != MinecraftLoaderKind.Forge ||
            !string.Equals(optiFine.MinecraftVersion, forge.MinecraftVersion, StringComparison.OrdinalIgnoreCase) ||
            optiFine.OptiFineEntry?.RequiredForgeVersion is not { } requiredForgeVersion)
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(requiredForgeVersion))
        {
            return true;
        }

        if (requiredForgeVersion.Contains('.', StringComparison.Ordinal))
        {
            return PclCeVersionComparer.CompareVersion(forge.Version, requiredForgeVersion) == 0;
        }

        var segments = forge.Version.Split('.');
        return segments.Length > 0 && string.Equals(segments[^1], requiredForgeVersion, StringComparison.OrdinalIgnoreCase);
    }

    partial void OnSelectedCatalogVersionChanged(MinecraftVersionCatalogEntry? value)
    {
        CanProvisionSelectedVersion = value is not null;
        RefreshLoaderEntries();
    }

    partial void OnVersionSearchTextChanged(string value) => ApplyVersionFilters();

    partial void OnSelectedVersionCategoryChanged(MinecraftVersionCatalogCategory value) => ApplyVersionFilters();

    private void ApplyVersionFilters(string? preferredVersionId = null)
    {
        if (allCatalogVersions.Count == 0)
        {
            return;
        }

        var selectedId = SelectedCatalogVersion?.Id ?? preferredVersionId;
        ReplaceVersions(
            ReleaseVersions,
            MinecraftVersionCatalogFilter.FilterByCategory(
                allCatalogVersions,
                null,
                MinecraftVersionCatalogCategory.Release));
        ReplaceVersions(
            SnapshotVersions,
            MinecraftVersionCatalogFilter.FilterByCategory(
                allCatalogVersions,
                null,
                MinecraftVersionCatalogCategory.Snapshot));
        ReplaceVersions(
            LegacyVersions,
            MinecraftVersionCatalogFilter.FilterByCategory(
                allCatalogVersions,
                null,
                MinecraftVersionCatalogCategory.Legacy));
        ReplaceVersions(
            AprilFoolsVersions,
            MinecraftVersionCatalogFilter.FilterByCategory(
                allCatalogVersions,
                null,
                MinecraftVersionCatalogCategory.AprilFools));

        var filtered = MinecraftVersionCatalogFilter.FilterByCategory(
            allCatalogVersions,
            VersionSearchText,
            SelectedVersionCategory);

        AvailableVersions.Clear();
        foreach (var version in filtered)
        {
            AvailableVersions.Add(version);
        }

        SelectedCatalogVersion = allCatalogVersions.FirstOrDefault(version => version.Id == selectedId)
            ?? LatestReleaseVersion;
        VersionCatalogSummary = $"已加载 {allCatalogVersions.Count} 个官方版本。";
    }

    private static void ReplaceVersions(
        ObservableCollection<MinecraftVersionCatalogEntry> target,
        IEnumerable<MinecraftVersionCatalogEntry> source)
    {
        target.Clear();
        foreach (var version in source)
        {
            target.Add(version);
        }
    }

    private static Uri CreateMinecraftWikiUri(string versionId)
    {
        var normalized = versionId.Trim();
        var page = normalized.Contains('w', StringComparison.OrdinalIgnoreCase)
            ? normalized
            : $"Java版{normalized}";
        return new Uri($"https://zh.minecraft.wiki/w/{Uri.EscapeDataString(page)}");
    }

    partial void OnSelectedInstanceChanged(MinecraftInstance? value)
    {
        RefreshLoaderEntries();
        CanInstallSelectedLoader = CanInstallLoaderForSelectedInstance(SelectedLoader);
        RefreshCommunityDownloadState();
        if (!isRefreshing)
        {
            _ = RefreshSelectedInstanceStateAsync();
            _ = SaveSelectedInstancePreferenceAsync(value?.Name);
            if (SelectedCommunityResource is not null)
            {
                _ = LoadCommunityResourceVersionsAsync();
            }
        }
    }

    [RelayCommand]
    private async Task LoadLoaderCatalogAsync()
    {
        try
        {
            LoaderCatalogSummary = "正在读取本地加载器目录…";
            var result = await loaderCatalogService.ReadAsync(LoaderCatalogPath);
            if (!result.IsSuccess || result.Catalog is null)
            {
                loaderCatalog = null;
                AvailableLoaders.Clear();
                HasAvailableLoaders = false;
                SelectedLoader = null;
                LoaderCatalogSummary = string.Join(Environment.NewLine, result.Errors);
                LoaderSelectionSummary = "目录未通过检查；未选择加载器，也不会发起安装。";
                return;
            }

            loaderCatalog = result.Catalog;
            LoaderCatalogPath = Path.GetFullPath(LoaderCatalogPath);
            LoaderCatalogSummary = $"已读取“{loaderCatalog.SourceName}”：共 {loaderCatalog.Entries.Count} 个加载器版本。";
            RefreshLoaderEntries();
        }
        catch (OperationCanceledException)
        {
            LoaderCatalogSummary = "读取本地加载器目录已取消。";
        }
        catch (Exception exception)
        {
            LoaderCatalogSummary = $"读取本地加载器目录失败：{exception.Message}";
        }
    }

    [RelayCommand]
    private async Task RefreshOfficialLoaderCatalogAsync()
    {
        if (IsOfficialLoaderCatalogLoading)
        {
            return;
        }

        var minecraftVersion = SelectedCatalogVersion?.Id ?? GetMinecraftVersionForLoaders(SelectedInstance);
        if (string.IsNullOrWhiteSpace(minecraftVersion))
        {
            LoaderCatalogSummary = "请先选择下载页中的官方版本，或选择一个本地实例；未访问网络。";
            return;
        }

        IsOfficialLoaderCatalogLoading = true;
        try
        {
            LoaderCatalogSummary = $"正在读取 Minecraft {minecraftVersion} 的官方加载器目录…";
            var result = await officialLoaderCatalogService.FetchAsync(minecraftVersion, loaderKindFilter);
            if (result.Catalog is null)
            {
                loaderCatalog = null;
                AvailableLoaders.Clear();
                HasAvailableLoaders = false;
                SelectedLoader = null;
                LoaderCatalogSummary = string.Join(Environment.NewLine, result.Errors);
                LoaderSelectionSummary = "加载器目录未通过检查；未选择加载器，也不会发起安装。";
                return;
            }

            loaderCatalog = result.Catalog;
            LoaderCatalogSummary = result.Errors.Count == 0
                ? $"已读取官方目录：Minecraft {minecraftVersion} 共 {loaderCatalog.Entries.Count} 个加载器版本。"
                : $"已读取部分官方目录：Minecraft {minecraftVersion} 共 {loaderCatalog.Entries.Count} 个加载器版本。{Environment.NewLine}{string.Join(Environment.NewLine, result.Errors)}";
            RefreshLoaderEntries();
        }
        catch (OperationCanceledException)
        {
            LoaderCatalogSummary = "读取官方加载器目录已取消。";
        }
        catch (Exception exception)
        {
            LoaderCatalogSummary = $"读取官方加载器目录失败：{exception.Message}";
        }
        finally
        {
            IsOfficialLoaderCatalogLoading = false;
        }
    }

    private void RefreshLoaderEntries()
    {
        var selectedKey = SelectedLoader is null
            ? null
            : $"{SelectedLoader.Kind}:{SelectedLoader.MinecraftVersion}:{SelectedLoader.Version}";
        AvailableLoaders.Clear();
        SelectedLoader = null;

        if (loaderCatalog is null)
        {
            HasAvailableLoaders = false;
            CanInstallSelectedLoader = false;
            LoaderSelectionSummary = "请先导入本地加载器目录。";
            return;
        }

        var minecraftVersion = SelectedCatalogVersion?.Id ?? GetMinecraftVersionForLoaders(SelectedInstance);
        if (string.IsNullOrWhiteSpace(minecraftVersion))
        {
            HasAvailableLoaders = false;
            CanInstallSelectedLoader = false;
            LoaderSelectionSummary = "请先选择下载页中的官方版本，或选择一个本地实例，以筛选兼容的加载器。";
            return;
        }

        foreach (var loader in MinecraftLoaderCatalogFilter.ForMinecraftVersion(loaderCatalog, minecraftVersion, loaderKindFilter))
        {
            AvailableLoaders.Add(loader);
        }

        HasAvailableLoaders = AvailableLoaders.Count > 0;
        SelectedLoader = AvailableLoaders.FirstOrDefault(loader =>
                string.Equals($"{loader.Kind}:{loader.MinecraftVersion}:{loader.Version}", selectedKey, StringComparison.OrdinalIgnoreCase))
            ?? AvailableLoaders.FirstOrDefault();
        CanInstallSelectedLoader = CanInstallLoaderForSelectedInstance(SelectedLoader);
        var filterName = loaderKindFilter?.ToString() ?? "加载器";
        LoaderSelectionSummary = HasAvailableLoaders
            ? $"Minecraft {minecraftVersion} 可选 {AvailableLoaders.Count} 个 {filterName} 版本。一次只能选择一个加载器安装器。"
            : $"当前目录中没有兼容 Minecraft {minecraftVersion} 的 {filterName} 版本。";
    }

    partial void OnSelectedLoaderChanged(MinecraftLoaderCatalogEntry? value)
    {
        CanInstallSelectedLoader = CanInstallLoaderForSelectedInstance(value);
        if (value is null)
        {
            return;
        }

        var compatibility = MinecraftLoaderCompatibilityEvaluator.Evaluate(value.MinecraftVersion, [value]);
        LoaderSelectionSummary = compatibility.IsCompatible
            ? value.Kind == MinecraftLoaderKind.OptiFine
                ? $"已选择 OptiFine {value.Version}（Minecraft {value.MinecraftVersion}，{value.Channel}）。目录来自 PCL 使用的公开源；该源未提供稳定 SHA-1，Aurora 会执行最小体积检查。1.14+ 运行官方安装器，旧版会创建受控继承版本。"
                : $"已选择 {value.Kind} {value.Version}（Minecraft {value.MinecraftVersion}，{value.Channel}）。选择本地实例与 Java 后，可由“下载并安装”明确触发。"
            : string.Join(Environment.NewLine, compatibility.Reasons);
    }

    partial void OnSelectedCommunityResourceChanged(CommunityResourceItemViewModel? value)
    {
        communityDescriptionTranslationCancellation?.Cancel();
        communityDescriptionTranslationCancellation = null;
        communityVersionCancellation?.Cancel();
        communityVersionCancellation = null;
        IsCommunityVersionLoading = false;
        if (communityDownloadCancellation is null)
        {
            CanCancelCommunityResourceOperation = false;
        }
        CommunityResourceVersions.Clear();
        CommunityResourceVersionGroups.Clear();
        ClearCommunityVersionFilters();
        SelectedCommunityResourceVersion = null;
        HasCommunityResourceVersions = false;
        if (value is not null)
        {
            value.IsFavorite = IsCommunityResourceFavorite(value.Project.Id);
        }
        CanOpenCommunityResource = value is not null;
        CanTranslateCommunityDescription = value is not null;
        IsCommunityDescriptionTranslationRunning = false;
        IsCommunityDescriptionTranslationPanelVisible = false;
        CommunityDescriptionTranslation = string.Empty;
        HasCommunityDescriptionTranslation = false;
        CommunityDescriptionTranslationSummary = string.Empty;
        CanLoadCommunityResourceVersions = value is not null;
        CommunityVersionSummary = value is null
            ? "选择项目后可查看文件版本。"
            : "正在准备版本列表…";
        OnPropertyChanged(nameof(IsCommunityVersionCardVisible));
        RefreshCommunityDownloadState();
    }

    partial void OnSelectedCommunityResourceVersionChanged(CommunityResourceVersion? value)
    {
        RefreshCommunityDownloadState();
        if (value is not null)
        {
            var versionCount = CommunityResourceVersions.Count;
            var summaryPrefix = versionCount > 0 ? $"共 {versionCount} 个版本；" : string.Empty;
            CommunityVersionSummary = $"{summaryPrefix}{value.FileSummary}；{value.DependencySummary}。";
        }
    }

    partial void OnCommunityPageChanged(int value) => OnPropertyChanged(nameof(CommunityPageNumber));

    partial void OnHasCommunityResourcesChanged(bool value)
    {
        OnPropertyChanged(nameof(IsCommunityResultListVisible));
        OnPropertyChanged(nameof(IsCommunityFavoriteListVisible));
        OnPropertyChanged(nameof(IsCommunityFavoriteEmptyVisible));
        OnPropertyChanged(nameof(IsCommunityStatusVisible));
    }

    partial void OnCommunityResourceSummaryChanged(string value) =>
        OnPropertyChanged(nameof(IsCommunityStatusVisible));

    partial void OnIsCommunityCatalogAvailableChanged(bool value) =>
        OnPropertyChanged(nameof(IsCommunityFooterVisible));

    partial void OnIsCommunitySearchRunningChanged(bool value)
    {
        OnPropertyChanged(nameof(IsCommunityResultListVisible));
        OnPropertyChanged(nameof(IsCommunityFavoriteListVisible));
        OnPropertyChanged(nameof(IsCommunityFavoriteEmptyVisible));
        OnPropertyChanged(nameof(IsCommunityFooterVisible));
        OnPropertyChanged(nameof(IsCommunityStatusVisible));
        OnPropertyChanged(nameof(IsCommunityVersionCardVisible));
    }

    partial void OnIsCommunityFavoritesPageChanged(bool value)
    {
        OnPropertyChanged(nameof(IsCommunityResultListVisible));
        OnPropertyChanged(nameof(IsCommunityFavoriteListVisible));
        OnPropertyChanged(nameof(IsCommunityFavoriteEmptyVisible));
    }

    partial void OnSelectedCommunityFavoriteFolderChanged(CommunityFavoriteFolder? value) =>
        OnPropertyChanged(nameof(IsCommunityFavoriteSearchVisible));

    partial void OnCommunityFavoriteSearchTextChanged(string value)
    {
        if (IsCommunityFavoritesPage && !IsCommunitySearchRunning)
        {
            RebuildCommunityFavoriteGroups();
        }
    }

    partial void OnIsVersionCatalogLoadingChanged(bool value) =>
        OnPropertyChanged(nameof(IsLoaderPageLoading));

    partial void OnIsOfficialLoaderCatalogLoadingChanged(bool value) =>
        OnPropertyChanged(nameof(IsLoaderPageLoading));

    partial void OnIsCombinedInstallerLoadingChanged(bool value) =>
        OnPropertyChanged(nameof(IsLoaderPageLoading));

    partial void OnSelectedJavaChanged(JavaInstallation? value)
    {
        CanInstallSelectedLoader = CanInstallLoaderForSelectedInstance(SelectedLoader);
        if (!isRefreshing && !isSelectingJavaForRequirement)
        {
            _ = RefreshSelectedInstanceStateAsync();
        }
    }

    private void SelectJavaForCurrentRequirement()
    {
        if (currentJavaRequirement is null ||
            (SelectedJava is not null && currentJavaRequirement.GetBlockingReasons(SelectedJava).Count == 0))
        {
            return;
        }

        var compatibleJava = AvailableJavaInstallations.FirstOrDefault(java =>
            currentJavaRequirement.GetBlockingReasons(java).Count == 0);
        if (compatibleJava is null)
        {
            return;
        }

        try
        {
            isSelectingJavaForRequirement = true;
            SelectedJava = compatibleJava;
        }
        finally
        {
            isSelectingJavaForRequirement = false;
        }
    }

    [RelayCommand]
    private async Task InstallSelectedLoaderAsync()
    {
        if (SelectedLoader is not { } loader ||
            SelectedInstance is not { } instance ||
            !string.Equals(loader.MinecraftVersion, GetMinecraftVersionForLoaders(instance), StringComparison.OrdinalIgnoreCase) ||
            (!MinecraftLoaderInstallerPlanBuilder.IsLegacyOptiFine(loader) && SelectedJava is null))
        {
            LoaderSelectionSummary = "请先选择兼容的加载器和本地实例；需要执行安装器时还须选择 Java。未下载或执行安装器。";
            return;
        }

        try
        {
            CanInstallSelectedLoader = false;
            LoaderSelectionSummary = $"正在准备 {loader.Kind} {loader.Version} 官方安装器…";
            var plan = await loaderInstallerService.PrepareAsync(loader, MinecraftRootDirectory, SelectedJava);
            if (!plan.CanInstall)
            {
                LoaderSelectionSummary = string.Join(Environment.NewLine, plan.BlockingReasons);
                return;
            }

            LoaderSelectionSummary = loader.Kind == MinecraftLoaderKind.OptiFine && MinecraftLoaderInstallerPlanBuilder.IsLegacyOptiFine(loader)
                ? $"正在下载并创建旧版 OptiFine {loader.Version} 继承版本…"
                : $"正在下载并执行 {loader.Kind} {loader.Version} 安装器…";
            var result = await loaderInstallerService.InstallAsync(plan, MinecraftRootDirectory, hasExplicitUserConfirmation: true);
            var resultSummary = result.Succeeded
                ? $"{loader.Kind} {loader.Version} 安装完成。请刷新本地实例列表以检查新增版本。"
                : string.Join(Environment.NewLine, result.Errors.Concat(result.Output.TakeLast(5).Select(line => line.Text)));
            await RefreshAsync();
            LoaderSelectionSummary = resultSummary;
        }
        catch (OperationCanceledException)
        {
            LoaderSelectionSummary = "加载器安装已取消；安装器进程已终止。";
        }
        catch (Exception exception)
        {
            LoaderSelectionSummary = $"加载器安装失败：{exception.Message}";
        }
        finally
        {
            CanInstallSelectedLoader = CanInstallLoaderForSelectedInstance(SelectedLoader);
        }
    }

    private bool CanInstallLoaderForSelectedInstance(MinecraftLoaderCatalogEntry? loader) =>
        loader is not null &&
        SelectedInstance is not null &&
        (MinecraftLoaderInstallerPlanBuilder.IsLegacyOptiFine(loader) || SelectedJava is not null) &&
        string.Equals(loader.MinecraftVersion, GetMinecraftVersionForLoaders(SelectedInstance), StringComparison.OrdinalIgnoreCase);

    private static string? GetMinecraftVersionForLoaders(MinecraftInstance? instance) =>
        instance?.BaseVersionId ?? instance?.VersionId;

    public void SetLoaderKindFilter(MinecraftLoaderKind? kind)
    {
        if (loaderKindFilter == kind)
        {
            return;
        }

        loaderKindFilter = kind;
        RefreshLoaderEntries();
    }

    public void SetCommunityResourceSection(string section)
    {
        isCommunityFavoritesSection = section == "favorites";
        IsCommunityFavoritesPage = isCommunityFavoritesSection;
        communityResourceType = section switch
        {
            "mod" => CommunityResourceType.Mod,
            "pack" => CommunityResourceType.ModPack,
            "datapack" => CommunityResourceType.DataPack,
            "resourcepack" => CommunityResourceType.ResourcePack,
            "shader" => CommunityResourceType.Shader,
            "world" => CommunityResourceType.World,
            _ => null,
        };
        communitySearchCancellation?.Cancel();
        communitySearchCancellation = null;
        communityVersionCancellation?.Cancel();
        communityVersionCancellation = null;
        communityDownloadCancellation?.Cancel();
        communityDownloadCancellation = null;
        IsCommunitySearchRunning = false;
        CanCancelCommunitySearch = false;
        IsCommunityVersionLoading = false;
        CanCancelCommunityResourceOperation = false;
        CommunityResourceVersions.Clear();
        CommunityResourceVersionGroups.Clear();
        ClearCommunityVersionFilters();
        SelectedCommunityResourceVersion = null;
        HasCommunityResourceVersions = false;
        CanLoadCommunityResourceVersions = false;
        CanDownloadCommunityResource = false;
        ClearCommunityResources();
        SelectedCommunityResource = null;
        HasCommunityResources = false;
        CommunityPage = 0;
        CanGoToPreviousCommunityPage = false;
        CanGoToNextCommunityPage = false;
        CommunityResourceCategoryOptions = GetCommunityResourceCategories(communityResourceType);
        SelectedCommunityResourceCategory = CommunityResourceCategoryOptions[0];
        CommunityResourceLoaderOptions = communityResourceType == CommunityResourceType.Shader
            ? ShaderLoaderOptions
            : GetVisibleModLoaderOptions();
        SelectedCommunityResourceLoader = CommunityResourceLoaderOptions[0];
        SelectedCommunityResourceSort = CommunityResourceSortOptions[0];
        IsCommunityLoaderFilterVisible = communityResourceType is
            CommunityResourceType.Mod or CommunityResourceType.ModPack or CommunityResourceType.Shader;
        IsCommunityCatalogAvailable = !isCommunityFavoritesSection && communityResourceType is not null;
        CanSearchCommunityResources = IsCommunityCatalogAvailable;
        CommunityResourceSummary = section switch
        {
            "favorites" => string.Empty,
            "world" => string.Empty,
            _ => string.Empty,
        };
    }

    public bool IsCommunityResourceFavorite(string projectId) =>
        CommunityFavoriteFolders.Any(folder => folder.Contains(projectId));

    public async Task ToggleCommunityFavoriteAsync(
        CommunityResourceProject project,
        string folderId)
    {
        ArgumentNullException.ThrowIfNull(project);
        var index = CommunityFavoriteFolders
            .Select((folder, position) => (folder, position))
            .FirstOrDefault(item => string.Equals(item.folder.Id, folderId, StringComparison.OrdinalIgnoreCase));
        if (index.folder is null)
        {
            return;
        }

        if (!index.folder.Contains(project.Id) &&
            index.folder.Projects.Count >= CommunityFavoriteFolder.MaximumProjectCount)
        {
            CommunityResourceSummary = "当前收藏夹已达到项目数量上限。";
            return;
        }

        var projects = index.folder.Contains(project.Id)
            ? index.folder.Projects.Where(item => !string.Equals(item.Id, project.Id, StringComparison.OrdinalIgnoreCase)).ToArray()
            : index.folder.Projects.Append(project).ToArray();
        var replacement = index.folder with { Projects = projects };
        CommunityFavoriteFolders[index.position] = replacement;
        if (SelectedCommunityFavoriteFolder?.Id == replacement.Id)
        {
            SelectedCommunityFavoriteFolder = replacement;
        }

        await SaveCommunityFavoritesAsync();
        UpdateCommunityFavoriteFlags();
        if (IsCommunityFavoritesPage)
        {
            await LoadFavoriteResourcesAsync();
        }
    }

    public async Task CreateCommunityFavoriteFolderAsync(string name)
    {
        var normalizedName = name.Trim();
        if (string.IsNullOrWhiteSpace(normalizedName) ||
            normalizedName.Length > CommunityFavoriteFolder.MaximumNameLength ||
            normalizedName.Any(char.IsControl))
        {
            CommunityResourceSummary = "收藏夹名称无效。";
            return;
        }

        var folder = CommunityFavoriteFolder.Create(normalizedName);
        CommunityFavoriteFolders.Add(folder);
        SelectedCommunityFavoriteFolder = folder;
        await SaveCommunityFavoritesAsync();
    }

    public async Task RenameSelectedCommunityFavoriteFolderAsync(string name)
    {
        if (SelectedCommunityFavoriteFolder is not { } selected)
        {
            return;
        }

        var normalizedName = name.Trim();
        if (string.IsNullOrWhiteSpace(normalizedName) ||
            normalizedName.Length > CommunityFavoriteFolder.MaximumNameLength ||
            normalizedName.Any(char.IsControl))
        {
            CommunityResourceSummary = "收藏夹名称无效。";
            return;
        }

        var index = CommunityFavoriteFolders.IndexOf(selected);
        if (index < 0)
        {
            return;
        }

        var replacement = selected with { Name = normalizedName };
        CommunityFavoriteFolders[index] = replacement;
        SelectedCommunityFavoriteFolder = replacement;
        await SaveCommunityFavoritesAsync();
    }

    public async Task<bool> DeleteSelectedCommunityFavoriteFolderAsync()
    {
        if (SelectedCommunityFavoriteFolder is not { } selected || CommunityFavoriteFolders.Count <= 1)
        {
            CommunityResourceSummary = "至少需要保留一个收藏夹。";
            return false;
        }

        CommunityFavoriteFolders.Remove(selected);
        SelectedCommunityFavoriteFolder = CommunityFavoriteFolders[0];
        await SaveCommunityFavoritesAsync();
        return true;
    }

    public string? ExportSelectedCommunityFavoriteFolder() =>
        SelectedCommunityFavoriteFolder is { } selected
            ? JsonSerializer.Serialize(selected, FavoriteTransferSerializerOptions)
            : null;

    public async Task<bool> ImportCommunityFavoriteFolderAsync(string json)
    {
        try
        {
            var imported = JsonSerializer.Deserialize<CommunityFavoriteFolder>(json, FavoriteTransferSerializerOptions);
            if (imported is null || !imported.IsValid)
            {
                CommunityResourceSummary = "导入的收藏数据无效。";
                return false;
            }

            var folder = CommunityFavoriteFolder.Create(imported.Name, imported.Projects);
            CommunityFavoriteFolders.Add(folder);
            SelectedCommunityFavoriteFolder = folder;
            await SaveCommunityFavoritesAsync();
            return true;
        }
        catch (JsonException)
        {
            CommunityResourceSummary = "导入的收藏数据不是有效 JSON。";
            return false;
        }
    }

    [RelayCommand]
    private Task SearchCommunityResourcesAsync() => LoadCommunityResourcesAsync(0);

    public Task LoadSelectedCommunityResourceVersionsAsync() => LoadCommunityResourceVersionsAsync();

    public void SelectCommunityGameVersionFilter(CommunityResourceVersionFilterOption option)
    {
        ArgumentNullException.ThrowIfNull(option);
        selectedCommunityGameVersionFilter = option.Value == PclCeCommunityResourceVersionOrganizer.AllFilter
            ? null
            : option.Value;
        UpdateCommunityVersionFilterSelection(CommunityGameVersionFilters, option);
        RebuildCommunityResourceVersionGroups();
    }

    public void SelectCommunityLoaderFilter(CommunityResourceVersionFilterOption option)
    {
        ArgumentNullException.ThrowIfNull(option);
        selectedCommunityLoaderFilter = option.Value == PclCeCommunityResourceVersionOrganizer.AllFilter
            ? null
            : option.Value;
        UpdateCommunityVersionFilterSelection(CommunityLoaderVersionFilters, option);
        RebuildCommunityResourceVersionGroups();
    }

    public async Task DownloadCommunityResourceVersionAsync(
        CommunityResourceVersion version,
        string destinationDirectory,
        IReadOnlyList<CommunityResourceVersion>? dependencies = null)
    {
        ArgumentNullException.ThrowIfNull(version);
        SelectedCommunityResourceVersion = version;
        await DownloadCommunityResourceAsync(destinationDirectory, dependencies ?? []);
    }

    public async Task ImportCommunityModpackVersionAsync(
        CommunityResourceVersion version,
        string destinationDirectory,
        string instanceName)
    {
        ArgumentNullException.ThrowIfNull(version);
        if (!CanDownloadCommunityResource ||
            SelectedCommunityResource?.Project is not { Type: CommunityResourceType.ModPack } project)
        {
            CommunityVersionSummary = "所选版本不是可导入的 Modrinth 整合包。";
            return;
        }

        SelectedCommunityResourceVersion = version;
        using var cancellation = new CancellationTokenSource();
        communityDownloadCancellation = cancellation;
        CanDownloadCommunityResource = false;
        CanLoadCommunityResourceVersions = false;
        CanCancelCommunityResourceOperation = true;
        try
        {
            var progress = new Progress<MinecraftDownloadProgress>(update =>
            {
                var size = update.TotalBytes is { } total
                    ? $"{FormatByteCount(update.DownloadedBytes)} / {FormatByteCount(total)}"
                    : FormatByteCount(update.DownloadedBytes);
                CommunityVersionSummary = $"正在导入 {update.CurrentDescription} · {size}";
            });
            var result = await modrinthModpackImportService.ImportAsync(
                project,
                version,
                destinationDirectory,
                instanceName,
                includeOptionalClientFiles: true,
                progress,
                cancellation.Token);
            if (!ReferenceEquals(communityDownloadCancellation, cancellation))
            {
                return;
            }

            var loader = result.LoaderKind is null
                ? string.Empty
                : $"，{result.LoaderKind} {result.LoaderVersion}";
            CommunityVersionSummary =
                $"已导入 {project.DisplayTitle}：Minecraft {result.MinecraftVersion}{loader}，" +
                $"{result.DownloadedFileCount} 个下载文件，{result.OverrideFileCount} 个覆盖文件。";
        }
        catch (OperationCanceledException)
        {
            if (ReferenceEquals(communityDownloadCancellation, cancellation))
            {
                CommunityVersionSummary = "整合包导入已取消。";
            }
        }
        catch (Exception exception)
        {
            if (ReferenceEquals(communityDownloadCancellation, cancellation))
            {
                CommunityVersionSummary = $"整合包导入失败：{exception.Message}";
            }
        }
        finally
        {
            if (ReferenceEquals(communityDownloadCancellation, cancellation))
            {
                communityDownloadCancellation = null;
                CanCancelCommunityResourceOperation = false;
                CanLoadCommunityResourceVersions = SelectedCommunityResource is not null;
                RefreshCommunityDownloadState();
            }
        }
    }

    public async Task ImportCommunityWorldVersionAsync(
        CommunityResourceVersion version,
        string destinationDirectory,
        string worldName)
    {
        ArgumentNullException.ThrowIfNull(version);
        if (!CanDownloadCommunityResource ||
            SelectedCommunityResource?.Project is not { Type: CommunityResourceType.World } project)
        {
            CommunityVersionSummary = "所选版本不是可导入的世界资源。";
            return;
        }

        SelectedCommunityResourceVersion = version;
        using var cancellation = new CancellationTokenSource();
        communityDownloadCancellation = cancellation;
        CanDownloadCommunityResource = false;
        CanLoadCommunityResourceVersions = false;
        CanCancelCommunityResourceOperation = true;
        try
        {
            var progress = new Progress<MinecraftDownloadProgress>(update =>
            {
                var size = update.TotalBytes is { } total
                    ? $"{FormatByteCount(update.DownloadedBytes)} / {FormatByteCount(total)}"
                    : FormatByteCount(update.DownloadedBytes);
                CommunityVersionSummary = $"正在导入 {update.CurrentDescription} · {size}";
            });
            var result = await communityWorldImportService.ImportAsync(
                project,
                version,
                destinationDirectory,
                worldName,
                progress,
                cancellation.Token);
            if (!ReferenceEquals(communityDownloadCancellation, cancellation))
            {
                return;
            }

            CommunityVersionSummary =
                $"已导入 {project.DisplayTitle}：{result.WorldDirectory}（{result.ExtractedFileCount} 个文件）。";
        }
        catch (OperationCanceledException)
        {
            if (ReferenceEquals(communityDownloadCancellation, cancellation))
            {
                CommunityVersionSummary = "世界导入已取消。";
            }
        }
        catch (Exception exception)
        {
            if (ReferenceEquals(communityDownloadCancellation, cancellation))
            {
                CommunityVersionSummary = $"世界导入失败：{exception.Message}";
            }
        }
        finally
        {
            if (ReferenceEquals(communityDownloadCancellation, cancellation))
            {
                communityDownloadCancellation = null;
                CanCancelCommunityResourceOperation = false;
                CanLoadCommunityResourceVersions = SelectedCommunityResource is not null;
                RefreshCommunityDownloadState();
            }
        }
    }

    public async Task<CommunityResourceDependencyPreparation?> PrepareCommunityResourceDependenciesAsync(
        CommunityResourceVersion version)
    {
        ArgumentNullException.ThrowIfNull(version);
        if (SelectedCommunityResource?.Project.Type != CommunityResourceType.Mod ||
            version.Dependencies.All(dependency => dependency.Type is not (
                CommunityResourceDependencyType.Required or CommunityResourceDependencyType.Optional)))
        {
            return new([], [], []);
        }

        using var cancellation = new CancellationTokenSource();
        communityDownloadCancellation = cancellation;
        CanDownloadCommunityResource = false;
        CanLoadCommunityResourceVersions = false;
        CanCancelCommunityResourceOperation = true;
        CommunityVersionSummary = $"正在解析 {version.VersionNumber} 的依赖…";
        try
        {
            var preparation = await communityResourceDependencyResolver.ResolveAsync(
                version,
                GetCommunityDependencyGameVersion(version),
                GetCommunityDependencyLoader(version),
                cancellation.Token);
            CommunityVersionSummary = preparation.Errors.Count == 0
                ? $"已解析 {preparation.RequiredVersions.Count} 项必要依赖和 {preparation.OptionalDependencies.Count} 项可选依赖。"
                : string.Join("；", preparation.Errors);
            return preparation;
        }
        catch (OperationCanceledException)
        {
            CommunityVersionSummary = "依赖解析已取消。";
            return null;
        }
        catch (Exception exception)
        {
            CommunityVersionSummary = $"依赖解析失败：{exception.Message}";
            return new([], [], [CommunityVersionSummary]);
        }
        finally
        {
            if (ReferenceEquals(communityDownloadCancellation, cancellation))
            {
                communityDownloadCancellation = null;
                CanCancelCommunityResourceOperation = false;
                CanLoadCommunityResourceVersions = SelectedCommunityResource is not null;
                RefreshCommunityDownloadState();
            }
        }
    }

    [RelayCommand]
    private Task PreviousCommunityPageAsync() =>
        CanGoToPreviousCommunityPage ? LoadCommunityResourcesAsync(CommunityPage - 1) : Task.CompletedTask;

    [RelayCommand]
    private Task NextCommunityPageAsync() =>
        CanGoToNextCommunityPage ? LoadCommunityResourcesAsync(CommunityPage + 1) : Task.CompletedTask;

    [RelayCommand]
    private void CancelCommunitySearch()
    {
        communitySearchCancellation?.Cancel();
    }

    [RelayCommand]
    private async Task OpenCommunityResourceAsync()
    {
        if (SelectedCommunityResource?.Project is not { } project)
        {
            CommunityResourceSummary = "请先选择一个社区项目。";
            return;
        }

        try
        {
            CommunityResourceSummary = $"正在打开 {project.DisplayTitle} 的 {project.SourceDisplay} 项目页…";
            await openPathService.OpenUriAsync(project.WebsiteUrl);
            CommunityResourceSummary = $"已打开 {project.DisplayTitle}。";
        }
        catch (Exception exception)
        {
            CommunityResourceSummary = $"无法打开项目页：{exception.Message}";
        }
    }

    [RelayCommand]
    private async Task TranslateCommunityDescriptionAsync()
    {
        if (SelectedCommunityResource?.Project is not { } project ||
            IsCommunityDescriptionTranslationRunning)
        {
            return;
        }

        communityDescriptionTranslationCancellation?.Cancel();
        using var cancellation = new CancellationTokenSource();
        communityDescriptionTranslationCancellation = cancellation;
        CanTranslateCommunityDescription = false;
        IsCommunityDescriptionTranslationRunning = true;
        IsCommunityDescriptionTranslationPanelVisible = true;
        CommunityDescriptionTranslation = string.Empty;
        HasCommunityDescriptionTranslation = false;
        CommunityDescriptionTranslationSummary = $"正在获取 {project.DisplayTitle} 的简介译文…";
        try
        {
            var result = await communityDescriptionTranslationService.TranslateAsync(project, cancellation.Token);
            if (!ReferenceEquals(communityDescriptionTranslationCancellation, cancellation) ||
                SelectedCommunityResource?.Project.Id != project.Id)
            {
                return;
            }

            CommunityDescriptionTranslation = result.Translation ?? string.Empty;
            HasCommunityDescriptionTranslation = result.HasTranslation;
            CommunityDescriptionTranslationSummary = result.HasTranslation
                ? "以下译文来自社区翻译，可与原文对照查看。"
                : result.Error ?? "当前资源的简介暂无译文。";
        }
        catch (OperationCanceledException)
        {
            if (ReferenceEquals(communityDescriptionTranslationCancellation, cancellation))
            {
                IsCommunityDescriptionTranslationPanelVisible = false;
            }
        }
        finally
        {
            if (ReferenceEquals(communityDescriptionTranslationCancellation, cancellation))
            {
                communityDescriptionTranslationCancellation = null;
                IsCommunityDescriptionTranslationRunning = false;
                CanTranslateCommunityDescription = SelectedCommunityResource is not null;
            }
        }
    }

    [RelayCommand]
    private async Task LoadCommunityResourceVersionsAsync()
    {
        if (SelectedCommunityResource?.Project is not { } project ||
            communityDownloadCancellation is not null)
        {
            return;
        }

        communityVersionCancellation?.Cancel();
        using var cancellation = new CancellationTokenSource();
        communityVersionCancellation = cancellation;
        CommunityResourceVersions.Clear();
        CommunityResourceVersionGroups.Clear();
        SelectedCommunityResourceVersion = null;
        HasCommunityResourceVersions = false;
        IsCommunityVersionLoading = true;
        CanLoadCommunityResourceVersions = false;
        CanCancelCommunityResourceOperation = true;
        CommunityVersionSummary = $"正在获取 {project.DisplayTitle} 的可用版本…";
        try
        {
            var catalog = await communityResourceVersionService.GetProjectVersionsAsync(
                project.Id,
                gameVersion: null,
                CommunityResourceLoader.Any,
                cancellation.Token);
            if (!ReferenceEquals(communityVersionCancellation, cancellation) ||
                SelectedCommunityResource?.Project.Id != project.Id)
            {
                return;
            }

            CommunityResourceVersions.Clear();
            foreach (var version in catalog.Versions.OrderByDescending(item => item.PublishedAt))
            {
                CommunityResourceVersions.Add(version);
            }

            InitializeCommunityVersionFilters(project.Type);
            RebuildCommunityResourceVersionGroups();
            CommunityVersionSummary = HasCommunityResourceVersions
                ? $"找到 {CommunityResourceVersions.Count} 个可用文件。"
                : catalog.Errors.Count > 0
                    ? $"版本列表不可用：{string.Join("；", catalog.Errors)}"
                    : "没有符合当前筛选条件的文件。";
        }
        catch (OperationCanceledException)
        {
            if (ReferenceEquals(communityVersionCancellation, cancellation))
            {
                CommunityVersionSummary = "已取消获取版本列表。";
            }
        }
        catch (Exception exception)
        {
            if (ReferenceEquals(communityVersionCancellation, cancellation))
            {
                CommunityVersionSummary = $"版本列表获取失败：{exception.Message}";
            }
        }
        finally
        {
            if (ReferenceEquals(communityVersionCancellation, cancellation))
            {
                communityVersionCancellation = null;
                IsCommunityVersionLoading = false;
                CanLoadCommunityResourceVersions = SelectedCommunityResource is not null;
                CanCancelCommunityResourceOperation = false;
                RefreshCommunityDownloadState();
            }
        }
    }

    private async Task DownloadCommunityResourceAsync(
        string destinationDirectory,
        IReadOnlyList<CommunityResourceVersion> dependencies)
    {
        if (!CanDownloadCommunityResource ||
            SelectedCommunityResource?.Project is not { } project ||
            SelectedCommunityResourceVersion is not { } version)
        {
            CommunityVersionSummary = "所选版本没有可下载文件。";
            return;
        }

        using var cancellation = new CancellationTokenSource();
        communityDownloadCancellation = cancellation;
        CanDownloadCommunityResource = false;
        CanLoadCommunityResourceVersions = false;
        CanCancelCommunityResourceOperation = true;
        try
        {
            var progress = new Progress<MinecraftDownloadProgress>(update =>
            {
                var size = update.TotalBytes is { } total
                    ? $"{FormatByteCount(update.DownloadedBytes)} / {FormatByteCount(total)}"
                    : FormatByteCount(update.DownloadedBytes);
                CommunityVersionSummary = $"正在下载 {update.CurrentDescription} · {size}";
            });
            var result = await communityResourceDownloadService.DownloadWithDependenciesAsync(
                project,
                version,
                dependencies,
                destinationDirectory,
                progress,
                cancellation.Token);
            if (!ReferenceEquals(communityDownloadCancellation, cancellation))
            {
                return;
            }

            CommunityVersionSummary = result.DependencyCount == 0
                ? $"已下载 {project.DisplayTitle}：{result.Paths[0]}"
                : $"已下载 {project.DisplayTitle} 与 {result.DependencyCount} 个依赖：{destinationDirectory}";
        }
        catch (OperationCanceledException)
        {
            if (ReferenceEquals(communityDownloadCancellation, cancellation))
            {
                CommunityVersionSummary = "社区资源下载已取消。";
            }
        }
        catch (Exception exception)
        {
            if (ReferenceEquals(communityDownloadCancellation, cancellation))
            {
                CommunityVersionSummary = $"下载失败：{exception.Message}";
            }
        }
        finally
        {
            if (ReferenceEquals(communityDownloadCancellation, cancellation))
            {
                communityDownloadCancellation = null;
                CanCancelCommunityResourceOperation = false;
                CanLoadCommunityResourceVersions = SelectedCommunityResource is not null;
                RefreshCommunityDownloadState();
            }
        }
    }

    [RelayCommand]
    private void CancelCommunityResourceOperation()
    {
        communityVersionCancellation?.Cancel();
        communityDownloadCancellation?.Cancel();
        CanCancelCommunityResourceOperation = false;
    }

    private void RefreshCommunityDownloadState()
    {
        CanDownloadCommunityResource =
            communityDownloadCancellation is null &&
            !IsCommunityVersionLoading &&
            SelectedCommunityResourceVersion?.PrimaryFile is not null;
    }

    private void RebuildCommunityResourceVersionGroups()
    {
        CommunityResourceVersionGroups.Clear();
        if (SelectedCommunityResource?.Project is not { } project)
        {
            HasCommunityResourceVersions = false;
            SelectedCommunityResourceVersion = null;
            return;
        }

        var groups = PclCeCommunityResourceVersionOrganizer.BuildGroups(
            CommunityResourceVersions,
            project.Type,
            communityVersionFilters,
            selectedCommunityGameVersionFilter,
            selectedCommunityLoaderFilter);
        for (var index = 0; index < groups.Count; index++)
        {
            var group = groups[index];
            CommunityResourceVersionGroups.Add(new(group.Title, group.Versions, index == 0));
        }

        HasCommunityResourceVersions = CommunityResourceVersionGroups.Count > 0;
        SelectedCommunityResourceVersion = CommunityResourceVersionGroups
            .SelectMany(group => group.Versions)
            .FirstOrDefault();
        CommunityVersionSummary = HasCommunityResourceVersions
            ? $"显示 {CommunityResourceVersionGroups.Sum(group => group.Count)} 个兼容文件。"
            : "没有符合当前筛选条件的文件。";
        RefreshCommunityDownloadState();
    }

    private void InitializeCommunityVersionFilters(CommunityResourceType type)
    {
        ClearCommunityVersionFilters();
        communityVersionFilters = PclCeCommunityResourceVersionOrganizer.BuildFilters(
            CommunityResourceVersions,
            type);

        var preferredGameVersion = string.IsNullOrWhiteSpace(CommunityGameVersion)
            ? GetMinecraftVersionForLoaders(SelectedInstance)
            : CommunityGameVersion.Trim();
        var preferredGameFilter = string.IsNullOrWhiteSpace(preferredGameVersion)
            ? null
            : PclCeCommunityResourceVersionOrganizer.GetFilterGroupName(
                preferredGameVersion,
                communityVersionFilters.GroupByMinorVersion,
                communityVersionFilters.FoldLegacyVersions);
        if (!communityVersionFilters.GameVersions.Contains(preferredGameFilter, StringComparer.OrdinalIgnoreCase))
        {
            preferredGameFilter = null;
        }

        selectedCommunityGameVersionFilter = preferredGameFilter;
        CommunityGameVersionFilters.Add(new(
            PclCeCommunityResourceVersionOrganizer.AllFilter,
            PclCeCommunityResourceVersionOrganizer.AllFilter,
            preferredGameFilter is null));
        foreach (var gameVersion in communityVersionFilters.GameVersions)
        {
            CommunityGameVersionFilters.Add(new(
                gameVersion,
                gameVersion,
                string.Equals(gameVersion, preferredGameFilter, StringComparison.OrdinalIgnoreCase)));
        }

        var preferredLoader = type == CommunityResourceType.Mod
            ? GetPreferredCommunityLoaderDisplayName()
            : null;
        if (!communityVersionFilters.Loaders.Contains(preferredLoader, StringComparer.OrdinalIgnoreCase))
        {
            preferredLoader = null;
        }

        selectedCommunityLoaderFilter = preferredLoader;
        CommunityLoaderVersionFilters.Add(new(
            PclCeCommunityResourceVersionOrganizer.AllFilter,
            PclCeCommunityResourceVersionOrganizer.AllFilter,
            preferredLoader is null));
        foreach (var loader in communityVersionFilters.Loaders.Where(loader =>
                     !IgnoreQuilt || !loader.Equals("Quilt", StringComparison.OrdinalIgnoreCase)))
        {
            CommunityLoaderVersionFilters.Add(new(
                loader,
                loader,
                string.Equals(loader, preferredLoader, StringComparison.OrdinalIgnoreCase)));
        }

        RaiseCommunityVersionFilterVisibilityChanged();
    }

    private string? GetPreferredCommunityLoaderDisplayName()
    {
        var loader = GetCommunityResourceLoaderForSelectedInstance();
        return loader switch
        {
            CommunityResourceLoader.Forge => "Forge",
            CommunityResourceLoader.NeoForge => "NeoForge",
            CommunityResourceLoader.Fabric => "Fabric",
            CommunityResourceLoader.Quilt when !IgnoreQuilt => "Quilt",
            _ => null,
        };
    }

    private void ClearCommunityVersionFilters()
    {
        communityVersionFilters = new([], [], false, false);
        selectedCommunityGameVersionFilter = null;
        selectedCommunityLoaderFilter = null;
        CommunityGameVersionFilters.Clear();
        CommunityLoaderVersionFilters.Clear();
        RaiseCommunityVersionFilterVisibilityChanged();
    }

    private void RaiseCommunityVersionFilterVisibilityChanged()
    {
        OnPropertyChanged(nameof(IsCommunityVersionFilterVisible));
        OnPropertyChanged(nameof(IsCommunityGameVersionFilterVisible));
        OnPropertyChanged(nameof(IsCommunityLoaderVersionFilterVisible));
    }

    private static void UpdateCommunityVersionFilterSelection(
        IEnumerable<CommunityResourceVersionFilterOption> options,
        CommunityResourceVersionFilterOption selected)
    {
        foreach (var option in options)
        {
            option.IsSelected = ReferenceEquals(option, selected);
        }
    }

    private IReadOnlyList<CommunityResourceLoaderOption> GetVisibleModLoaderOptions() =>
        IgnoreQuilt
            ? ModLoaderOptions.Where(option => option.Loader != CommunityResourceLoader.Quilt).ToArray()
            : ModLoaderOptions;

    private CommunityResourceLoader GetCommunityResourceLoaderForSelectedInstance() =>
        SelectedInstance?.InstalledLoader?.Kind switch
        {
            MinecraftLoaderKind.Forge => CommunityResourceLoader.Forge,
            MinecraftLoaderKind.NeoForge => CommunityResourceLoader.NeoForge,
            MinecraftLoaderKind.Fabric => CommunityResourceLoader.Fabric,
            _ => SelectedCommunityResourceLoader.Loader,
        };

    private string? GetCommunityDependencyGameVersion(CommunityResourceVersion version)
    {
        if (string.IsNullOrWhiteSpace(selectedCommunityGameVersionFilter))
        {
            return version.GameVersions.FirstOrDefault(IsStableCommunityGameVersion);
        }

        return version.GameVersions.FirstOrDefault(gameVersion =>
            string.Equals(
                PclCeCommunityResourceVersionOrganizer.GetFilterGroupName(
                    gameVersion,
                    communityVersionFilters.GroupByMinorVersion,
                    communityVersionFilters.FoldLegacyVersions),
                selectedCommunityGameVersionFilter,
                StringComparison.OrdinalIgnoreCase));
    }

    private CommunityResourceLoader GetCommunityDependencyLoader(CommunityResourceVersion version)
    {
        var value = selectedCommunityLoaderFilter ?? version.Loaders.FirstOrDefault(loader =>
            loader.Equals("forge", StringComparison.OrdinalIgnoreCase) ||
            loader.Equals("neoforge", StringComparison.OrdinalIgnoreCase) ||
            loader.Equals("fabric", StringComparison.OrdinalIgnoreCase) ||
            (!IgnoreQuilt && loader.Equals("quilt", StringComparison.OrdinalIgnoreCase)));
        return value?.ToLowerInvariant() switch
        {
            "forge" => CommunityResourceLoader.Forge,
            "neoforge" => CommunityResourceLoader.NeoForge,
            "fabric" => CommunityResourceLoader.Fabric,
            "quilt" => CommunityResourceLoader.Quilt,
            _ => CommunityResourceLoader.Any,
        };
    }

    private static bool IsStableCommunityGameVersion(string value)
    {
        var parts = value.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return parts.Length >= 2 && parts.All(part => part.All(char.IsAsciiDigit));
    }

    private async Task LoadCommunityResourcesAsync(int page)
    {
        if (communityResourceType is not { } type || !CanSearchCommunityResources || IsCommunitySearchRunning)
        {
            return;
        }

        using var cancellation = new CancellationTokenSource();
        communitySearchCancellation = cancellation;
        IsCommunitySearchRunning = true;
        CanCancelCommunitySearch = true;
        CanSearchCommunityResources = false;
        CommunityLoadingText = $"正在获取 {GetCommunityResourceTypeName(type)} 列表";
        CommunityResourceSummary = string.Empty;
        CommunityResourceItemViewModel[] pendingItems = [];
        var publishedItems = false;
        try
        {
            var result = await communityResourceSearchService.SearchAsync(
                new CommunityResourceSearchRequest(
                    type,
                    CommunitySearchText,
                    CommunityGameVersion,
                    SelectedCommunityResourceLoader.Loader,
                    SelectedCommunityResourceSort.Sort,
                    page,
                    Category: SelectedCommunityResourceCategory.Category),
                cancellation.Token);
            if (!ReferenceEquals(communitySearchCancellation, cancellation) || communityResourceType != type)
            {
                return;
            }

            if (!result.IsSuccess && result.Projects.Count == 0 && result.Limit == 0)
            {
                CommunityResourceSummary = "资源下载操作失败。请检查网络后重试。";
                return;
            }

            pendingItems = result.Projects
                .Select(project => new CommunityResourceItemViewModel(project))
                .ToArray();
            foreach (var item in pendingItems)
            {
                item.IsFavorite = IsCommunityResourceFavorite(item.Project.Id);
            }
            if (pendingItems.Length > 0)
            {
                CommunityLoadingText = $"正在加载 {GetCommunityResourceTypeName(type)} 图标";
                await LoadCommunityIconsAsync(pendingItems, cancellation.Token);
                cancellation.Token.ThrowIfCancellationRequested();
                if (!ReferenceEquals(communitySearchCancellation, cancellation) || communityResourceType != type)
                {
                    return;
                }
            }

            ClearCommunityResources();
            foreach (var item in pendingItems)
            {
                CommunityResources.Add(item);
            }
            publishedItems = true;

            CommunityPage = page;
            HasCommunityResources = CommunityResources.Count > 0;
            CanGoToPreviousCommunityPage = page > 0;
            CanGoToNextCommunityPage = result.HasNextPage;
            CommunityResourceSummary = result.Projects.Count == 0
                ? "没有符合条件的结果"
                : string.Empty;
        }
        catch (OperationCanceledException)
        {
            CommunityResourceSummary = "已取消";
        }
        catch (Exception)
        {
            CommunityResourceSummary = "资源下载操作失败。请检查网络后重试。";
        }
        finally
        {
            if (!publishedItems)
            {
                foreach (var item in pendingItems)
                {
                    item.Dispose();
                }
            }

            if (ReferenceEquals(communitySearchCancellation, cancellation))
            {
                communitySearchCancellation = null;
                IsCommunitySearchRunning = false;
                CanCancelCommunitySearch = false;
                CanSearchCommunityResources = IsCommunityCatalogAvailable;
            }
        }
    }

    private Task LoadCommunityIconsAsync(
        IReadOnlyList<CommunityResourceItemViewModel> items,
        CancellationToken cancellationToken) =>
        Task.WhenAll(items.Select(item => LoadCommunityIconAsync(item, cancellationToken)));

    private async Task LoadCommunityIconAsync(
        CommunityResourceItemViewModel item,
        CancellationToken cancellationToken)
    {
        if (item.Project.IconUrl is not { } iconUrl)
        {
            return;
        }

        try
        {
            var bytes = await communityResourceIconService.LoadAsync(iconUrl, cancellationToken);
            if (bytes is null || cancellationToken.IsCancellationRequested)
            {
                return;
            }

            item.SetIcon(bytes);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception)
        {
            // A malformed project icon must not hide an otherwise valid search result.
        }
    }

    private void ClearCommunityResources()
    {
        foreach (var item in CommunityResources)
        {
            item.Dispose();
        }

        CommunityResources.Clear();
        CommunityFavoriteGroups.Clear();
        CommunityResourceVersionGroups.Clear();
    }

    private async Task LoadCommunityFavoritesAsync()
    {
        var result = await communityFavoritesStore.LoadAsync();
        CommunityFavoriteFolders.Clear();
        foreach (var folder in result.Folders)
        {
            CommunityFavoriteFolders.Add(folder);
        }

        SelectedCommunityFavoriteFolder = CommunityFavoriteFolders.FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(result.Warning))
        {
            CommunityResourceSummary = result.Warning;
        }
    }

    private async Task SaveCommunityFavoritesAsync()
    {
        try
        {
            await communityFavoritesStore.SaveAsync(CommunityFavoriteFolders.ToArray());
        }
        catch (Exception exception)
        {
            CommunityResourceSummary = $"保存收藏夹失败：{exception.Message}";
        }
    }

    private async Task LoadFavoriteResourcesAsync()
    {
        if (SelectedCommunityFavoriteFolder is not { } folder)
        {
            return;
        }

        communitySearchCancellation?.Cancel();
        using var cancellation = new CancellationTokenSource();
        communitySearchCancellation = cancellation;
        IsCommunitySearchRunning = true;
        CanCancelCommunitySearch = true;
        CommunityLoadingText = "正在加载收藏内容";
        CommunityResourceSummary = string.Empty;
        CommunityResourceItemViewModel[] pendingItems = [];
        var publishedItems = false;
        try
        {
            pendingItems = folder.Projects
                .Select(project => new CommunityResourceItemViewModel(project) { IsFavorite = true })
                .ToArray();
            await LoadCommunityIconsAsync(pendingItems, cancellation.Token);
            cancellation.Token.ThrowIfCancellationRequested();
            if (!ReferenceEquals(communitySearchCancellation, cancellation) ||
                SelectedCommunityFavoriteFolder?.Id != folder.Id)
            {
                return;
            }

            ClearCommunityResources();
            foreach (var item in pendingItems)
            {
                CommunityResources.Add(item);
            }
            publishedItems = true;
            RebuildCommunityFavoriteGroups();
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            if (!publishedItems)
            {
                foreach (var item in pendingItems)
                {
                    item.Dispose();
                }
            }

            if (ReferenceEquals(communitySearchCancellation, cancellation))
            {
                communitySearchCancellation = null;
                IsCommunitySearchRunning = false;
                CanCancelCommunitySearch = false;
                OnPropertyChanged(nameof(IsCommunityFavoriteListVisible));
                OnPropertyChanged(nameof(IsCommunityFavoriteEmptyVisible));
            }
        }
    }

    private void RebuildCommunityFavoriteGroups()
    {
        var searchText = CommunityFavoriteSearchText.Trim();
        var matchingItems = CommunityResources
            .Where(item => string.IsNullOrWhiteSpace(searchText) ||
                           item.Project.DisplayTitle.Contains(searchText, StringComparison.CurrentCultureIgnoreCase) ||
                           item.Project.Title.Contains(searchText, StringComparison.OrdinalIgnoreCase) ||
                           item.Project.Description.Contains(searchText, StringComparison.CurrentCultureIgnoreCase) ||
                           item.Project.Author.Contains(searchText, StringComparison.CurrentCultureIgnoreCase))
            .ToArray();
        CommunityFavoriteGroups.Clear();
        foreach (var group in matchingItems
                     .GroupBy(item => item.Project.Type)
                     .OrderBy(group => group.Key))
        {
            CommunityFavoriteGroups.Add(new(
                group.Key,
                GetCommunityResourceTypeName(group.Key),
                group.ToArray()));
        }

        HasCommunityResources = matchingItems.Length > 0;
        CommunityFavoriteEmptyTitle = string.IsNullOrWhiteSpace(searchText)
            ? "还没有收藏内容"
            : "没有匹配的收藏内容";
        CommunityFavoriteEmptyDescription = string.IsNullOrWhiteSpace(searchText)
            ? "在资源详细信息界面中可以点击收藏按钮进行收藏"
            : "换一个关键词再试试";
        OnPropertyChanged(nameof(IsCommunityFavoriteListVisible));
        OnPropertyChanged(nameof(IsCommunityFavoriteEmptyVisible));
    }

    private void UpdateCommunityFavoriteFlags()
    {
        foreach (var item in CommunityResources)
        {
            item.IsFavorite = IsCommunityResourceFavorite(item.Project.Id);
        }

        if (SelectedCommunityResource is { } selected)
        {
            selected.IsFavorite = IsCommunityResourceFavorite(selected.Project.Id);
        }
    }

    private static string GetCommunityResourceTypeName(CommunityResourceType type) => type switch
    {
        CommunityResourceType.Mod => "模组",
        CommunityResourceType.ModPack => "整合包",
        CommunityResourceType.DataPack => "数据包",
        CommunityResourceType.ResourcePack => "资源包",
        CommunityResourceType.Shader => "光影包",
        CommunityResourceType.World => "世界资源",
        _ => "资源",
    };

    private static IReadOnlyList<CommunityResourceCategoryOption> GetCommunityResourceCategories(
        CommunityResourceType? type) => type switch
    {
        CommunityResourceType.Mod =>
        [
            AllCommunityResourceCategories,
            new("worldgen", "世界生成"),
            new("technology", "科技"),
            new("food", "食物与烹饪"),
            new("game-mechanics", "游戏机制"),
            new("transportation", "运输"),
            new("storage", "仓储"),
            new("magic", "魔法"),
            new("adventure", "冒险"),
            new("decoration", "装饰"),
            new("mobs", "生物"),
            new("utility", "辅助"),
            new("equipment", "装备与工具"),
            new("optimization", "性能优化"),
            new("social", "服务端"),
            new("library", "支持库"),
        ],
        CommunityResourceType.ModPack =>
        [
            AllCommunityResourceCategories,
            new("optimization", "性能优化"),
            new("challenging", "硬核"),
            new("combat", "战斗"),
            new("quests", "任务"),
            new("technology", "科技"),
            new("magic", "魔法"),
            new("adventure", "冒险"),
            new("kitchen-sink", "混合"),
            new("lightweight", "轻量"),
        ],
        CommunityResourceType.DataPack =>
        [
            AllCommunityResourceCategories,
            new("worldgen", "世界生成"),
            new("technology", "科技"),
            new("game-mechanics", "游戏机制"),
            new("transportation", "运输"),
            new("storage", "仓储"),
            new("magic", "魔法"),
            new("adventure", "冒险"),
            new("decoration", "装饰"),
            new("mobs", "生物"),
            new("utility", "辅助"),
            new("equipment", "装备与工具"),
            new("optimization", "性能优化"),
            new("social", "服务端"),
            new("library", "支持库"),
        ],
        CommunityResourceType.ResourcePack =>
        [
            AllCommunityResourceCategories,
            new("vanilla-like", "原版风格"),
            new("realistic", "写实风格"),
            new("themed", "主题风格"),
            new("simplistic", "简约风格"),
            new("decoration", "装饰"),
            new("combat", "战斗"),
            new("utility", "辅助"),
            new("tweaks", "微调"),
            new("cursed", "鬼畜"),
            new("entities", "含实体"),
            new("audio", "含声音"),
            new("fonts", "含字体"),
            new("models", "含模型"),
            new("locale", "含语言"),
            new("gui", "含 UI"),
            new("core-shaders", "核心着色器"),
            new("modded", "兼容模组"),
            new("8x-", "8x 及以下"),
            new("16x", "16x"),
            new("32x", "32x"),
            new("48x", "48x"),
            new("64x", "64x"),
            new("128x", "128x"),
            new("256x", "256x"),
            new("512x+", "512x 及以上"),
        ],
        CommunityResourceType.Shader =>
        [
            AllCommunityResourceCategories,
            new("vanilla-like", "原版风格"),
            new("fantasy", "幻想风"),
            new("realistic", "写实风格"),
            new("semi-realistic", "半写实风格"),
            new("cartoon", "卡通风"),
            new("colored-lighting", "彩色光照"),
            new("path-tracing", "路径追踪"),
            new("pbr", "PBR"),
            new("reflections", "反射"),
            new("potato", "极低性能需求"),
            new("low", "低性能需求"),
            new("medium", "中等性能需求"),
            new("high", "高性能需求"),
        ],
        CommunityResourceType.World =>
        [
            AllCommunityResourceCategories,
            new("248", "冒险"),
            new("249", "创造"),
            new("250", "小游戏"),
            new("251", "跑酷"),
            new("252", "解谜"),
            new("253", "生存"),
            new("4464", "模组世界"),
        ],
        _ => [AllCommunityResourceCategories],
    };

    partial void OnHasAcknowledgedAccountGuidanceChanged(bool value)
    {
        if (!isRefreshing)
        {
            _ = RefreshGameLaunchPreparationAsync();
        }
    }

    partial void OnHasAvailableInstancesChanged(bool value)
    {
        OnPropertyChanged(nameof(NeedsGameDownload));
    }

    private async Task SaveSelectedInstancePreferenceAsync(string? instanceName)
    {
        try
        {
            await preferencesService.SaveSelectedInstanceNameAsync(instanceName);
            currentPreferences = currentPreferences with { SelectedInstanceName = instanceName };
        }
        catch (Exception exception)
        {
            InstanceSummary = $"{InstanceSummary}{Environment.NewLine}无法保存实例选择：{exception.Message}";
        }
    }

    partial void OnSelectedThemeModeChanged(ThemeOption value)
    {
        if (isLoadingPreferences)
        {
            return;
        }

        themeService.Apply(value.Mode);
        _ = SaveThemePreferenceAsync(value);
    }

    private async Task SaveThemePreferenceAsync(ThemeOption value)
    {
        try
        {
            ThemeSummary = $"当前使用{value.DisplayName}主题；正在保存到本机…";
            await preferencesService.SaveThemeModeAsync(value.Mode);
            ThemeSummary = $"当前使用{value.DisplayName}主题；该偏好已保存到本机。";
        }
        catch (Exception exception)
        {
            ThemeSummary = $"当前使用{value.DisplayName}主题，但保存失败：{exception.Message}";
        }
    }

    private void ApplyInterfaceSettings(InterfaceSettings settings)
    {
        InterfaceWindowOpacity = settings.WindowOpacity;
        LightThemeColorIndex = (int)settings.LightColor;
        DarkThemeColorIndex = (int)settings.DarkColor;
        ShowStartupLogo = settings.ShowStartupLogo;
        LockWindowSize = settings.LockWindowSize;
        ShowLaunchingHint = settings.ShowLaunchingHint;
        EnableAdvancedMaterial = settings.EnableAdvancedMaterial;
        InterfaceBlurRadius = settings.BlurRadius;
        InterfaceBlurSamplingRate = settings.BlurSamplingRate;
        InterfaceBlurKernelIndex = (int)settings.BlurKernel;
        GlobalInterfaceFont = settings.GlobalFont;
        MotdInterfaceFont = settings.MotdFont;
        BackgroundSuitIndex = (int)settings.BackgroundSuit;
        InterfaceBackgroundOpacity = settings.BackgroundOpacity;
        InterfaceBackgroundBlurRadius = settings.BackgroundBlurRadius;
        AutoPauseBackgroundVideo = settings.AutoPauseVideo;
        UseColorfulBackground = settings.BackgroundColorful;
        InterfaceMusicVolume = settings.MusicVolume;
        ShuffleBackgroundMusic = settings.ShuffleMusic;
        AutoPlayBackgroundMusic = settings.AutoPlayMusic;
        StartBackgroundMusicInGame = settings.StartMusicInGame;
        StopBackgroundMusicInGame = settings.StopMusicInGame;
        EnableSystemMediaControls = settings.EnableSystemMediaControls;
        TitleContentTypeIndex = (int)settings.TitleType;
        TitleLeftAligned = settings.TitleLeftAligned;
        CustomTitleText = settings.CustomTitleText;
        HomepageTypeIndex = (int)settings.HomepageType;
        HomepagePresetIndex = settings.HomepagePreset;
        HomepageUrl = settings.HomepageUrl;

        var hidden = settings.EffectiveHidden;
        HidePageDownload = hidden.PageDownload;
        HidePageSettings = hidden.PageSettings;
        HidePageTools = hidden.PageTools;
        HideSetupLaunch = hidden.SetupLaunch;
        HideSetupJava = hidden.SetupJava;
        HideSetupManage = hidden.SetupManage;
        HideSetupLink = hidden.SetupLink;
        HideSetupInterface = hidden.SetupInterface;
        HideSetupLanguage = hidden.SetupLanguage;
        HideSetupMisc = hidden.SetupMisc;
        HideSetupUpdate = hidden.SetupUpdate;
        HideSetupAbout = hidden.SetupAbout;
        HideSetupFeedback = hidden.SetupFeedback;
        HideSetupLog = hidden.SetupLog;
        HideToolsLink = hidden.ToolsLink;
        HideToolsToolbox = hidden.ToolsToolbox;
        HideInstanceEdit = hidden.InstanceEdit;
        HideInstanceExport = hidden.InstanceExport;
        HideInstanceSave = hidden.InstanceSave;
        HideInstanceScreenshot = hidden.InstanceScreenshot;
        HideInstanceMod = hidden.InstanceMod;
        HideInstanceResourcePack = hidden.InstanceResourcePack;
        HideInstanceShader = hidden.InstanceShader;
        HideInstanceSchematic = hidden.InstanceSchematic;
        HideInstanceServer = hidden.InstanceServer;
        HideFunctionInstanceSelect = hidden.FunctionInstanceSelect;
        HideFunctionModUpdate = hidden.FunctionModUpdate;
        HideFunctionSettings = hidden.FunctionHideSettings;
    }

    partial void OnInterfaceWindowOpacityChanged(int value)
    {
        OnPropertyChanged(nameof(InterfaceWindowOpacityFraction));
        QueueInterfaceSettingsSave();
    }

    partial void OnEnableAdvancedMaterialChanged(bool value)
    {
        OnPropertyChanged(nameof(UsesAdvancedMaterialSettings));
        QueueInterfaceSettingsSave();
    }

    partial void OnTitleContentTypeIndexChanged(int value)
    {
        OnPropertyChanged(nameof(UsesCustomTitleText));
        OnPropertyChanged(nameof(UsesCustomTitleImage));
        OnPropertyChanged(nameof(ShowsTitleLeftAlignment));
        OnPropertyChanged(nameof(IsTitleNone));
        OnPropertyChanged(nameof(IsTitleDefault));
        OnPropertyChanged(nameof(IsTitleText));
        OnPropertyChanged(nameof(IsTitleImage));
        QueueInterfaceSettingsSave();
    }

    partial void OnHomepageTypeIndexChanged(int value)
    {
        OnPropertyChanged(nameof(UsesLocalHomepage));
        OnPropertyChanged(nameof(UsesOnlineHomepage));
        OnPropertyChanged(nameof(UsesPresetHomepage));
        OnPropertyChanged(nameof(IsHomepageBlank));
        OnPropertyChanged(nameof(IsHomepageLocal));
        OnPropertyChanged(nameof(IsHomepageOnline));
        OnPropertyChanged(nameof(IsHomepagePreset));
        QueueInterfaceSettingsSave();
    }

    partial void OnStartBackgroundMusicInGameChanged(bool value)
    {
        if (value)
        {
            StopBackgroundMusicInGame = false;
        }

        QueueInterfaceSettingsSave();
    }

    partial void OnStopBackgroundMusicInGameChanged(bool value)
    {
        if (value)
        {
            StartBackgroundMusicInGame = false;
        }

        QueueInterfaceSettingsSave();
    }

    partial void OnLightThemeColorIndexChanged(int value) => QueueInterfaceSettingsSave();
    partial void OnDarkThemeColorIndexChanged(int value) => QueueInterfaceSettingsSave();
    partial void OnShowStartupLogoChanged(bool value) => QueueInterfaceSettingsSave();
    partial void OnLockWindowSizeChanged(bool value) => QueueInterfaceSettingsSave();
    partial void OnShowLaunchingHintChanged(bool value) => QueueInterfaceSettingsSave();
    partial void OnInterfaceBlurRadiusChanged(int value) => QueueInterfaceSettingsSave();
    partial void OnInterfaceBlurSamplingRateChanged(int value) => QueueInterfaceSettingsSave();
    partial void OnInterfaceBlurKernelIndexChanged(int value) => QueueInterfaceSettingsSave();
    partial void OnGlobalInterfaceFontChanged(string value) => QueueInterfaceSettingsSave();
    partial void OnMotdInterfaceFontChanged(string value) => QueueInterfaceSettingsSave();
    partial void OnBackgroundSuitIndexChanged(int value) => QueueInterfaceSettingsSave();
    partial void OnInterfaceBackgroundOpacityChanged(int value) => QueueInterfaceSettingsSave();
    partial void OnInterfaceBackgroundBlurRadiusChanged(int value) => QueueInterfaceSettingsSave();
    partial void OnAutoPauseBackgroundVideoChanged(bool value) => QueueInterfaceSettingsSave();
    partial void OnUseColorfulBackgroundChanged(bool value) => QueueInterfaceSettingsSave();
    partial void OnInterfaceMusicVolumeChanged(int value) => QueueInterfaceSettingsSave();
    partial void OnShuffleBackgroundMusicChanged(bool value) => QueueInterfaceSettingsSave();
    partial void OnAutoPlayBackgroundMusicChanged(bool value) => QueueInterfaceSettingsSave();
    partial void OnEnableSystemMediaControlsChanged(bool value) => QueueInterfaceSettingsSave();
    partial void OnTitleLeftAlignedChanged(bool value) => QueueInterfaceSettingsSave();
    partial void OnCustomTitleTextChanged(string value) => QueueInterfaceSettingsSave();
    partial void OnHomepagePresetIndexChanged(int value) => QueueInterfaceSettingsSave();
    partial void OnHomepageUrlChanged(string value) => QueueInterfaceSettingsSave();

    partial void OnHidePageDownloadChanged(bool value) => QueueInterfaceSettingsSave();
    partial void OnHidePageSettingsChanged(bool value) => QueueInterfaceSettingsSave();
    partial void OnHidePageToolsChanged(bool value) => QueueInterfaceSettingsSave();
    partial void OnHideSetupLaunchChanged(bool value) => QueueInterfaceSettingsSave();
    partial void OnHideSetupJavaChanged(bool value) => QueueInterfaceSettingsSave();
    partial void OnHideSetupManageChanged(bool value) => QueueInterfaceSettingsSave();
    partial void OnHideSetupLinkChanged(bool value) => QueueInterfaceSettingsSave();
    partial void OnHideSetupInterfaceChanged(bool value) => QueueInterfaceSettingsSave();
    partial void OnHideSetupLanguageChanged(bool value) => QueueInterfaceSettingsSave();
    partial void OnHideSetupMiscChanged(bool value) => QueueInterfaceSettingsSave();
    partial void OnHideSetupUpdateChanged(bool value) => QueueInterfaceSettingsSave();
    partial void OnHideSetupAboutChanged(bool value) => QueueInterfaceSettingsSave();
    partial void OnHideSetupFeedbackChanged(bool value) => QueueInterfaceSettingsSave();
    partial void OnHideSetupLogChanged(bool value) => QueueInterfaceSettingsSave();
    partial void OnHideToolsLinkChanged(bool value) => QueueInterfaceSettingsSave();
    partial void OnHideToolsToolboxChanged(bool value) => QueueInterfaceSettingsSave();
    partial void OnHideInstanceEditChanged(bool value) => QueueInterfaceSettingsSave();
    partial void OnHideInstanceExportChanged(bool value) => QueueInterfaceSettingsSave();
    partial void OnHideInstanceSaveChanged(bool value) => QueueInterfaceSettingsSave();
    partial void OnHideInstanceScreenshotChanged(bool value) => QueueInterfaceSettingsSave();
    partial void OnHideInstanceModChanged(bool value) => QueueInterfaceSettingsSave();
    partial void OnHideInstanceResourcePackChanged(bool value) => QueueInterfaceSettingsSave();
    partial void OnHideInstanceShaderChanged(bool value) => QueueInterfaceSettingsSave();
    partial void OnHideInstanceSchematicChanged(bool value) => QueueInterfaceSettingsSave();
    partial void OnHideInstanceServerChanged(bool value) => QueueInterfaceSettingsSave();
    partial void OnHideFunctionInstanceSelectChanged(bool value) => QueueInterfaceSettingsSave();
    partial void OnHideFunctionModUpdateChanged(bool value) => QueueInterfaceSettingsSave();
    partial void OnHideFunctionSettingsChanged(bool value) => QueueInterfaceSettingsSave();

    private void QueueInterfaceSettingsSave()
    {
        if (isLoadingPreferences)
        {
            return;
        }

        interfaceSettingsSaveCancellation?.Cancel();
        interfaceSettingsSaveCancellation?.Dispose();
        interfaceSettingsSaveCancellation = new CancellationTokenSource();
        _ = SaveInterfaceSettingsAsync(interfaceSettingsSaveCancellation.Token);
    }

    private async Task SaveInterfaceSettingsAsync(CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(250, cancellationToken);
            var hidden = new InterfaceFeatureVisibility(
                HidePageDownload, HidePageSettings, HidePageTools,
                HideSetupLaunch, HideSetupJava, HideSetupManage, HideSetupLink,
                HideSetupInterface, HideSetupLanguage, HideSetupMisc, HideSetupUpdate,
                HideSetupAbout, HideSetupFeedback, HideSetupLog,
                HideToolsLink, HideToolsToolbox,
                HideInstanceEdit, HideInstanceExport, HideInstanceSave, HideInstanceScreenshot,
                HideInstanceMod, HideInstanceResourcePack, HideInstanceShader,
                HideInstanceSchematic, HideInstanceServer,
                HideFunctionInstanceSelect, HideFunctionModUpdate, HideFunctionSettings);
            var settings = new InterfaceSettings(
                InterfaceWindowOpacity,
                (LauncherColorTheme)LightThemeColorIndex,
                (LauncherColorTheme)DarkThemeColorIndex,
                ShowStartupLogo,
                LockWindowSize,
                ShowLaunchingHint,
                EnableAdvancedMaterial,
                InterfaceBlurRadius,
                InterfaceBlurSamplingRate,
                (LauncherBlurKernel)InterfaceBlurKernelIndex,
                GlobalInterfaceFont,
                MotdInterfaceFont,
                (LauncherBackgroundSuitMode)BackgroundSuitIndex,
                InterfaceBackgroundOpacity,
                InterfaceBackgroundBlurRadius,
                AutoPauseBackgroundVideo,
                UseColorfulBackground,
                InterfaceMusicVolume,
                ShuffleBackgroundMusic,
                AutoPlayBackgroundMusic,
                StartBackgroundMusicInGame,
                StopBackgroundMusicInGame,
                EnableSystemMediaControls,
                (LauncherTitleContentType)TitleContentTypeIndex,
                TitleLeftAligned,
                CustomTitleText,
                (LauncherHomepageType)HomepageTypeIndex,
                HomepagePresetIndex,
                HomepageUrl,
                hidden);
            if (!settings.IsValid)
            {
                return;
            }

            await preferencesService.SaveInterfaceSettingsAsync(settings, cancellationToken);
            currentPreferences = currentPreferences with { InterfaceSettings = settings };
        }
        catch (OperationCanceledException)
        {
        }
        catch
        {
            // The next setting change retries the atomic local preference write.
        }
    }

    private void ApplyLocalizationSettings(LauncherLocalizationSettings settings)
    {
        SelectedLauncherLanguage = LauncherLanguages.First(option =>
            string.Equals(option.Code, settings.Language, StringComparison.OrdinalIgnoreCase));
        SelectedLauncherFormatCulture = LauncherFormatCultures.FirstOrDefault(option =>
            string.Equals(option.Code, settings.FormatCulture, StringComparison.OrdinalIgnoreCase))
            ?? LauncherFormatCultures[0];
        ApplyCultures(settings);
    }

    private async Task ApplyMiscSettingsAsync(LauncherMiscSettings settings)
    {
        AnnouncementModeIndex = (int)settings.AnnouncementMode;
        AnimationFpsLimitStep = settings.AnimationFpsLimitStep;
        MaximumGameLogLinesStep = settings.MaximumGameLogLinesStep;
        DisableHardwareAcceleration = settings.DisableHardwareAcceleration;
        TelemetryEnabled = settings.Telemetry;
        EnableDoh = settings.EnableDoh;
        ProxyModeIndex = (int)settings.ProxyMode;
        CustomProxyAddress = settings.CustomProxyAddress;
        CustomProxyUsername = settings.CustomProxyUsername;
        DebugAnimationSpeedStep = settings.DebugAnimationSpeedStep;
        DebugSkipCopy = settings.DebugSkipCopy;
        DebugMode = settings.DebugMode;
        DebugDelay = settings.DebugDelay;
        CustomProxyPassword = await secretStore.GetAsync(ProxySecretService, ProxySecretAccount) ?? string.Empty;
        networkSettingsService.Apply(settings, CustomProxyPassword);
        PclMotionSettings.Configure(settings.AnimationFramesPerSecond, settings.AnimationSpeedMultiplier);
        TrimGameLogs(settings.MaximumGameLogLines);
    }

    partial void OnSelectedLauncherLanguageChanged(LauncherLanguageOption value)
    {
        if (isLoadingPreferences)
        {
            return;
        }

        ApplyCultures(CreateLocalizationSettings());
        QueueLocalizationSettingsSave();
    }

    partial void OnSelectedLauncherFormatCultureChanged(LauncherFormatCultureOption value)
    {
        if (isLoadingPreferences)
        {
            return;
        }

        ApplyCultures(CreateLocalizationSettings());
        QueueLocalizationSettingsSave();
    }

    partial void OnAnnouncementModeIndexChanged(int value) => QueueMiscSettingsSave();

    partial void OnAnimationFpsLimitStepChanged(int value)
    {
        OnPropertyChanged(nameof(AnimationFpsLimitDisplay));
        PclMotionSettings.Configure(value + 1, CreateMiscSettings().AnimationSpeedMultiplier);
        QueueMiscSettingsSave();
    }

    partial void OnMaximumGameLogLinesStepChanged(int value)
    {
        OnPropertyChanged(nameof(MaximumGameLogLinesDisplay));
        TrimGameLogs(CreateMiscSettings().MaximumGameLogLines);
        QueueMiscSettingsSave();
    }

    partial void OnDisableHardwareAccelerationChanged(bool value)
    {
        MiscSettingsStatus = "硬件加速设置将在下次启动 PCL Aurora 时生效。";
        QueueMiscSettingsSave();
    }

    partial void OnTelemetryEnabledChanged(bool value) => QueueMiscSettingsSave();

    partial void OnEnableDohChanged(bool value)
    {
        ApplyNetworkSettings();
        QueueMiscSettingsSave();
    }

    partial void OnProxyModeIndexChanged(int value)
    {
        OnPropertyChanged(nameof(UsesCustomProxy));
        OnPropertyChanged(nameof(IsProxyDisabled));
        OnPropertyChanged(nameof(IsProxySystem));
        OnPropertyChanged(nameof(IsProxyCustom));
        ApplyNetworkSettings();
        QueueMiscSettingsSave();
    }

    partial void OnDebugAnimationSpeedStepChanged(int value)
    {
        OnPropertyChanged(nameof(DebugAnimationSpeedDisplay));
        PclMotionSettings.Configure(CreateMiscSettings().AnimationFramesPerSecond,
            value > 29 ? 0 : value / 10d + 0.1d);
        QueueMiscSettingsSave();
    }

    partial void OnDebugSkipCopyChanged(bool value) => QueueMiscSettingsSave();
    partial void OnDebugModeChanged(bool value) => QueueMiscSettingsSave();
    partial void OnDebugDelayChanged(bool value) => QueueMiscSettingsSave();

    partial void OnSelectedUpdateChannelChanged(GameManagementOption<LauncherUpdateChannel> value)
    {
        if (isLoadingPreferences)
        {
            return;
        }

        QueueUpdateSettingsSave();
        _ = CheckForUpdatesAsync();
    }

    partial void OnSelectedAutoUpdateBehaviorChanged(GameManagementOption<LauncherAutoUpdateBehavior> value) =>
        QueueUpdateSettingsSave();

    private LauncherLocalizationSettings CreateLocalizationSettings() =>
        new(SelectedLauncherLanguage.Code, SelectedLauncherFormatCulture.Code);

    private LauncherMiscSettings CreateMiscSettings() => new(
        (LauncherAnnouncementMode)AnnouncementModeIndex,
        AnimationFpsLimitStep,
        MaximumGameLogLinesStep,
        DisableHardwareAcceleration,
        TelemetryEnabled,
        EnableDoh,
        (LauncherProxyMode)ProxyModeIndex,
        CustomProxyAddress.Trim(),
        CustomProxyUsername.Trim(),
        DebugAnimationSpeedStep,
        DebugSkipCopy,
        DebugMode,
        DebugDelay);

    private LauncherUpdateSettings CreateUpdateSettings() =>
        new(SelectedUpdateChannel.Value, SelectedAutoUpdateBehavior.Value);

    private void QueueLocalizationSettingsSave()
    {
        if (isLoadingPreferences)
        {
            return;
        }

        localizationSettingsSaveCancellation?.Cancel();
        localizationSettingsSaveCancellation?.Dispose();
        localizationSettingsSaveCancellation = new CancellationTokenSource();
        _ = SaveLocalizationSettingsAsync(localizationSettingsSaveCancellation.Token);
    }

    private async Task SaveLocalizationSettingsAsync(CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(250, cancellationToken);
            await preferencesService.SaveLocalizationSettingsAsync(CreateLocalizationSettings(), cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
    }

    private void QueueMiscSettingsSave()
    {
        if (isLoadingPreferences)
        {
            return;
        }

        miscSettingsSaveCancellation?.Cancel();
        miscSettingsSaveCancellation?.Dispose();
        miscSettingsSaveCancellation = new CancellationTokenSource();
        _ = SaveMiscSettingsAsync(miscSettingsSaveCancellation.Token);
    }

    private async Task SaveMiscSettingsAsync(CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(250, cancellationToken);
            await preferencesService.SaveMiscSettingsAsync(CreateMiscSettings(), cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            MiscSettingsStatus = $"保存杂项设置失败：{exception.Message}";
        }
    }

    private void QueueUpdateSettingsSave()
    {
        if (isLoadingPreferences)
        {
            return;
        }

        updateSettingsSaveCancellation?.Cancel();
        updateSettingsSaveCancellation?.Dispose();
        updateSettingsSaveCancellation = new CancellationTokenSource();
        _ = SaveUpdateSettingsAsync(updateSettingsSaveCancellation.Token);
    }

    private async Task SaveUpdateSettingsAsync(CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(250, cancellationToken);
            var settings = CreateUpdateSettings();
            await preferencesService.SaveUpdateSettingsAsync(settings, cancellationToken);
            currentPreferences = currentPreferences with { UpdateSettings = settings };
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
    }

    private void ApplyCultures(LauncherLocalizationSettings settings)
    {
        var uiCulture = ResolveUiCulture(settings.Language);
        var formatCulture = settings.FormatCulture switch
        {
            LauncherLocalizationSettings.Auto => SystemFormatCulture,
            LauncherLocalizationSettings.FollowInterfaceLanguage => uiCulture,
            _ => CultureInfo.GetCultureInfo(settings.FormatCulture),
        };
        CultureInfo.DefaultThreadCurrentUICulture = uiCulture;
        CultureInfo.DefaultThreadCurrentCulture = formatCulture;
        CultureInfo.CurrentUICulture = uiCulture;
        CultureInfo.CurrentCulture = formatCulture;
    }

    private static CultureInfo ResolveUiCulture(string code)
    {
        if (!string.Equals(code, LauncherLocalizationSettings.Auto, StringComparison.OrdinalIgnoreCase))
        {
            return CultureInfo.GetCultureInfo(code);
        }

        var current = SystemUiCulture;
        var exact = LauncherLocalizationSettings.SupportedLanguageCodes.FirstOrDefault(candidate =>
            string.Equals(candidate, current.Name, StringComparison.OrdinalIgnoreCase));
        var neutral = exact ?? LauncherLocalizationSettings.SupportedLanguageCodes.FirstOrDefault(candidate =>
            candidate.StartsWith(current.TwoLetterISOLanguageName + "-", StringComparison.OrdinalIgnoreCase));
        return CultureInfo.GetCultureInfo(neutral ?? LauncherLocalizationSettings.DefaultLanguageCode);
    }

    private void ApplyNetworkSettings() =>
        networkSettingsService.Apply(CreateMiscSettings(), CustomProxyPassword);

    public async Task ApplyProxySettingsAsync()
    {
        var settings = CreateMiscSettings();
        if (settings.ProxyMode == LauncherProxyMode.Custom &&
            (!Uri.TryCreate(settings.CustomProxyAddress, UriKind.Absolute, out var proxy) ||
             proxy.Scheme is not ("http" or "https")))
        {
            MiscSettingsStatus = "代理地址无效，请填写完整的 http:// 或 https:// 地址。";
            return;
        }

        if (string.IsNullOrEmpty(CustomProxyPassword))
        {
            await secretStore.DeleteAsync(ProxySecretService, ProxySecretAccount);
        }
        else
        {
            await secretStore.SetAsync(ProxySecretService, ProxySecretAccount, CustomProxyPassword);
        }

        await preferencesService.SaveMiscSettingsAsync(settings);
        networkSettingsService.Apply(settings, CustomProxyPassword);
        MiscSettingsStatus = "代理设置已应用。";
    }

    public async Task ExportSettingsAsync(Stream destination)
    {
        ArgumentNullException.ThrowIfNull(destination);
        var snapshot = preferencesService.Current with
        {
            LocalizationSettings = CreateLocalizationSettings(),
            MiscSettings = CreateMiscSettings(),
            UpdateSettings = CreateUpdateSettings(),
        };
        await JsonSerializer.SerializeAsync(destination, snapshot,
            PreferencesTransferSerializerOptions);
        MiscSettingsStatus = "配置导出成功。代理密码等安全凭据未包含在导出文件中。";
    }

    public async Task ImportSettingsAsync(Stream source)
    {
        ArgumentNullException.ThrowIfNull(source);
        CancelEnvironmentSettingsSaves();
        var imported = await JsonSerializer.DeserializeAsync<LauncherPreferences>(source,
            PreferencesTransferSerializerOptions)
            ?? throw new InvalidDataException("配置文件为空。 ");
        if (!imported.IsValid)
        {
            throw new InvalidDataException("配置文件包含无效或不支持的设置。 ");
        }

        await preferencesService.ReplaceAsync(imported);
        await LoadPreferencesAsync();
        MiscSettingsStatus = "配置导入成功；硬件加速设置将在重启后生效。";
    }

    public async Task ResetLocalizationSettingsAsync()
    {
        CancelLocalizationSettingsSave();
        isLoadingPreferences = true;
        try
        {
            ApplyLocalizationSettings(LauncherLocalizationSettings.Default);
            await preferencesService.SaveLocalizationSettingsAsync(LauncherLocalizationSettings.Default);
        }
        finally
        {
            isLoadingPreferences = false;
        }
    }

    public async Task ResetMiscSettingsAsync()
    {
        CancelMiscSettingsSave();
        isLoadingPreferences = true;
        try
        {
            await secretStore.DeleteAsync(ProxySecretService, ProxySecretAccount);
            await ApplyMiscSettingsAsync(LauncherMiscSettings.Default);
            await preferencesService.SaveMiscSettingsAsync(LauncherMiscSettings.Default);
            MiscSettingsStatus = "已初始化杂项页设置。";
        }
        finally
        {
            isLoadingPreferences = false;
        }
    }

    public async Task StopUsingAuroraAsync()
    {
        CancelEnvironmentSettingsSaves();
        if (preferencesService.Current.MicrosoftAccount is { } profile)
        {
            await microsoftAccountSessionService.RemoveAsync(profile);
        }

        await secretStore.DeleteAsync(ProxySecretService, ProxySecretAccount);
        await preferencesService.ReplaceAsync(LauncherPreferences.Default);
        currentPreferences = LauncherPreferences.Default;
        networkSettingsService.Apply(LauncherMiscSettings.Default, null);
    }

    private void CancelEnvironmentSettingsSaves()
    {
        CancelLocalizationSettingsSave();
        CancelMiscSettingsSave();
        CancelUpdateSettingsSave();
    }

    private void CancelLocalizationSettingsSave()
    {
        localizationSettingsSaveCancellation?.Cancel();
        localizationSettingsSaveCancellation?.Dispose();
        localizationSettingsSaveCancellation = null;
    }

    private void CancelMiscSettingsSave()
    {
        miscSettingsSaveCancellation?.Cancel();
        miscSettingsSaveCancellation?.Dispose();
        miscSettingsSaveCancellation = null;
    }

    private void CancelUpdateSettingsSave()
    {
        updateSettingsSaveCancellation?.Cancel();
        updateSettingsSaveCancellation?.Dispose();
        updateSettingsSaveCancellation = null;
    }

    private void TrimGameLogs(int maximumLines)
    {
        if (maximumLines == int.MaxValue)
        {
            return;
        }

        while (GameLogLines.Count > maximumLines)
        {
            GameLogLines.RemoveAt(0);
        }
    }

    partial void OnSelectedDownloadConcurrencyChanged(int value)
    {
        OnPropertyChanged(nameof(DownloadConcurrencyDisplay));
        if (!isLoadingPreferences)
        {
            _ = SaveDownloadConcurrencyPreferenceAsync(value);
        }
    }

    partial void OnSelectedDownloadSpeedLimitStepChanged(int value)
    {
        OnPropertyChanged(nameof(DownloadSpeedLimitDisplay));
        if (!isLoadingPreferences)
        {
            _ = SaveDownloadSpeedLimitPreferenceAsync(value);
        }
    }

    private async Task SaveDownloadConcurrencyPreferenceAsync(int value)
    {
        try
        {
            await preferencesService.SaveDownloadConcurrencyAsync(value);
            currentPreferences = currentPreferences with { DownloadConcurrency = value };
        }
        catch
        {
            SelectedDownloadConcurrency = currentPreferences.DownloadConcurrency;
        }
    }

    private async Task SaveDownloadSpeedLimitPreferenceAsync(int value)
    {
        try
        {
            await preferencesService.SaveDownloadSpeedLimitStepAsync(value);
            currentPreferences = currentPreferences with { DownloadSpeedLimitStep = value };
        }
        catch
        {
            SelectedDownloadSpeedLimitStep = currentPreferences.DownloadSpeedLimitStep;
        }
    }

    partial void OnSelectedFileSourceChanged(GameManagementOption<DownloadSourcePreference> value) => QueueGameManagementOptionsSave();
    partial void OnSelectedVersionListSourceChanged(GameManagementOption<DownloadSourcePreference> value) => QueueGameManagementOptionsSave();
    partial void OnAutoSelectNewInstanceChanged(bool value) => QueueGameManagementOptionsSave();
    partial void OnFixAuthlibChanged(bool value) => QueueGameManagementOptionsSave();
    partial void OnSelectedCommunitySourceChanged(GameManagementOption<DownloadSourcePreference> value) => QueueGameManagementOptionsSave();
    partial void OnSelectedCommunityFileNameFormatChanged(GameManagementOption<CommunityFileNameFormat> value) => QueueGameManagementOptionsSave();
    partial void OnSelectedCommunityModNameStyleChanged(GameManagementOption<CommunityModNameStyle> value) => QueueGameManagementOptionsSave();
    partial void OnSelectedCommunityQuickDownloadBehaviorChanged(GameManagementOption<CommunityQuickDownloadBehavior> value) => QueueGameManagementOptionsSave();
    partial void OnIgnoreQuiltChanged(bool value)
    {
        if (communityResourceType is not null && communityResourceType != CommunityResourceType.Shader)
        {
            var selectedLoader = SelectedCommunityResourceLoader.Loader;
            CommunityResourceLoaderOptions = GetVisibleModLoaderOptions();
            SelectedCommunityResourceLoader = CommunityResourceLoaderOptions.FirstOrDefault(option =>
                                                  option.Loader == selectedLoader)
                                              ?? CommunityResourceLoaderOptions[0];
        }

        if (SelectedCommunityResource?.Project.Type is { } selectedType && CommunityResourceVersions.Count > 0)
        {
            InitializeCommunityVersionFilters(selectedType);
            RebuildCommunityResourceVersionGroups();
        }

        QueueGameManagementOptionsSave();
    }

    partial void OnAutoInstallDependenciesChanged(bool value) => QueueGameManagementOptionsSave();
    partial void OnReleaseNotificationsChanged(bool value) => QueueGameManagementOptionsSave();
    partial void OnSnapshotNotificationsChanged(bool value) => QueueGameManagementOptionsSave();
    partial void OnAutoChangeGameLanguageChanged(bool value) => QueueGameManagementOptionsSave();
    partial void OnReadClipboardChanged(bool value) => QueueGameManagementOptionsSave();

    private void QueueGameManagementOptionsSave()
    {
        if (isLoadingPreferences)
        {
            return;
        }

        gameManagementOptionsSaveCancellation?.Cancel();
        gameManagementOptionsSaveCancellation?.Dispose();
        gameManagementOptionsSaveCancellation = new CancellationTokenSource();
        _ = SaveGameManagementOptionsAsync(gameManagementOptionsSaveCancellation.Token);
    }

    private async Task SaveGameManagementOptionsAsync(CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(250, cancellationToken);
            var options = new GameManagementOptions(
                SelectedFileSource.Value,
                SelectedVersionListSource.Value,
                AutoSelectNewInstance,
                FixAuthlib,
                SelectedCommunitySource.Value,
                SelectedCommunityFileNameFormat.Value,
                SelectedCommunityModNameStyle.Value,
                SelectedCommunityQuickDownloadBehavior.Value,
                IgnoreQuilt,
                AutoInstallDependencies,
                ReleaseNotifications,
                SnapshotNotifications,
                AutoChangeGameLanguage,
                ReadClipboard);
            await preferencesService.SaveGameManagementOptionsAsync(options, cancellationToken);
            currentPreferences = currentPreferences with { GameManagementOptions = options };
        }
        catch (OperationCanceledException)
        {
        }
        catch
        {
            // Keep the current UI responsive; the next setting change retries the atomic preference write.
        }
    }

    partial void OnSelectedGameWindowModeChanged(MinecraftGameWindowModeOption value)
    {
        UsesCustomGameWindowSize = value.Mode == MinecraftGameWindowMode.Custom;
        QueueLaunchOptionsSave();
    }

    partial void OnSelectedMemoryAllocationModeChanged(MinecraftMemoryAllocationModeOption value)
    {
        UsesCustomMemoryAllocation = value.Mode == MinecraftMemoryAllocationMode.Custom;
        UsesAutomaticMemoryAllocation = value.Mode == MinecraftMemoryAllocationMode.Automatic;
        RefreshMemoryDisplay();
        QueueLaunchOptionsSave();
    }

    partial void OnCustomMemorySliderValueChanged(double value)
    {
        CustomMemoryMiB = MemorySliderStepToMiB((int)Math.Round(value))
            .ToString(System.Globalization.CultureInfo.InvariantCulture);
        RefreshMemoryDisplay();
        QueueLaunchOptionsSave();
    }

    partial void OnAdditionalJvmArgumentsChanged(string value) => QueueLaunchOptionsSave();
    partial void OnAdditionalGameArgumentsChanged(string value) => QueueLaunchOptionsSave();
    partial void OnCustomGameWindowWidthChanged(string value) => QueueLaunchOptionsSave();
    partial void OnCustomGameWindowHeightChanged(string value) => QueueLaunchOptionsSave();
    partial void OnSelectedInstanceIsolationModeChanged(MinecraftInstanceIsolationModeOption value) => QueueLaunchOptionsSave();
    partial void OnWindowTitleChanged(string value) => QueueLaunchOptionsSave();
    partial void OnCustomInfoChanged(string value) => QueueLaunchOptionsSave();
    partial void OnSelectedLauncherVisibilityChanged(MinecraftLauncherVisibilityOption value) => QueueLaunchOptionsSave();
    partial void OnSelectedGameProcessPriorityChanged(MinecraftGameProcessPriorityOption value) => QueueLaunchOptionsSave();
    partial void OnSelectedPreferredIpStackChanged(MinecraftPreferredIpStackOption value) => QueueLaunchOptionsSave();
    partial void OnSelectedRendererModeChanged(MinecraftRendererModeOption value) => QueueLaunchOptionsSave();
    partial void OnPreLaunchCommandChanged(string value) => QueueLaunchOptionsSave();
    partial void OnWaitForPreLaunchCommandChanged(bool value) => QueueLaunchOptionsSave();
    partial void OnDisableJavaLaunchWrapperChanged(bool value) => QueueLaunchOptionsSave();
    partial void OnDisableLegacyFixChanged(bool value) => QueueLaunchOptionsSave();
    partial void OnPreferDedicatedGpuChanged(bool value) => QueueLaunchOptionsSave();
    partial void OnUseJavaExecutableChanged(bool value) => QueueLaunchOptionsSave();
    partial void OnDisableLwjglUnsafeAgentChanged(bool value) => QueueLaunchOptionsSave();
    partial void OnDisableCrashAnalysisChanged(bool value) => QueueLaunchOptionsSave();
    partial void OnLockMemoryChanged(bool value) => QueueLaunchOptionsSave();

    public void SetMemoryAllocationMode(MinecraftMemoryAllocationMode mode) =>
        SelectedMemoryAllocationMode = MemoryAllocationModes.Single(option => option.Mode == mode);

    [RelayCommand]
    private void ResetJvmArguments() => AdditionalJvmArguments = MinecraftLaunchOptions.DefaultAdditionalJvmArguments;

    private void QueueLaunchOptionsSave()
    {
        if (isLoadingPreferences)
        {
            return;
        }

        launchOptionsSaveCancellation?.Cancel();
        launchOptionsSaveCancellation?.Dispose();
        launchOptionsSaveCancellation = new CancellationTokenSource();
        _ = SaveLaunchOptionsAfterDelayAsync(launchOptionsSaveCancellation.Token);
    }

    private async Task SaveLaunchOptionsAfterDelayAsync(CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(350, cancellationToken);
            await SaveLaunchOptionsAsync();
        }
        catch (OperationCanceledException)
        {
        }
    }

    [RelayCommand]
    private async Task SaveLaunchOptionsAsync()
    {
        if (!int.TryParse(
                CustomGameWindowWidth,
                System.Globalization.NumberStyles.None,
                System.Globalization.CultureInfo.InvariantCulture,
                out var width) ||
            !int.TryParse(
                CustomGameWindowHeight,
                System.Globalization.NumberStyles.None,
                System.Globalization.CultureInfo.InvariantCulture,
                out var height))
        {
            LaunchOptionsSummary = "窗口宽度和高度必须是整数；未保存也不会影响当前启动配置。";
            return;
        }

        if (!int.TryParse(
                CustomMemoryMiB,
                System.Globalization.NumberStyles.None,
                System.Globalization.CultureInfo.InvariantCulture,
                out var customMemory))
        {
            LaunchOptionsSummary = "自定义内存必须是 MiB 整数；未保存也不会影响当前启动配置。";
            return;
        }

        var options = new MinecraftLaunchOptions(
            AdditionalJvmArguments,
            AdditionalGameArguments,
            SelectedGameWindowMode.Mode,
            width,
            height,
            SelectedMemoryAllocationMode.Mode,
            customMemory,
            SelectedInstanceIsolationMode.Mode,
            WindowTitle,
            CustomInfo,
            SelectedLauncherVisibility.Mode,
            SelectedGameProcessPriority.Priority,
            SelectedPreferredIpStack.Stack,
            SelectedRendererMode.Mode,
            PreLaunchCommand,
            WaitForPreLaunchCommand,
            DisableJavaLaunchWrapper,
            DisableLegacyFix,
            PreferDedicatedGpu,
            UseJavaExecutable,
            DisableLwjglUnsafeAgent,
            DisableCrashAnalysis,
            LockMemory);
        if (!options.IsValid)
        {
            LaunchOptionsSummary = $"启动选项无效：自定义参数最多 {MinecraftLaunchOptions.MaximumArgumentTextLength} 个字符，窗口尺寸范围为 {MinecraftLaunchOptions.MinimumWindowDimension}–{MinecraftLaunchOptions.MaximumWindowDimension}，内存范围为 {MinecraftLaunchOptions.MinimumCustomMemoryMiB}–{MinecraftLaunchOptions.MaximumCustomMemoryMiB} MiB。";
            return;
        }

        try
        {
            LaunchOptionsSummary = "正在保存启动选项…";
            await preferencesService.SaveLaunchOptionsAsync(options);
            currentPreferences = currentPreferences with { LaunchOptions = options };
            LaunchOptionsSummary = GetLaunchOptionsSummary(options);
            await RefreshSelectedInstanceStateAsync();
        }
        catch (Exception exception)
        {
            LaunchOptionsSummary = $"启动选项保存失败：{exception.Message}";
        }
    }

    /// <summary>
    /// 将“游戏-启动”设置页的全部字段重置为安全默认值并持久化。
    /// 迁移自 PCL-CE PageSetupLeft.Reset 对 PageSetupLaunch 的初始化语义。
    /// </summary>
    public async Task ResetLaunchSettingsAsync()
    {
        var defaults = MinecraftLaunchOptions.Default;
        isLoadingPreferences = true;
        try
        {
            AdditionalJvmArguments = defaults.AdditionalJvmArguments ?? string.Empty;
            AdditionalGameArguments = defaults.AdditionalGameArguments ?? string.Empty;
            SelectedInstanceIsolationMode = InstanceIsolationModes.Single(option => option.Mode == defaults.InstanceIsolationMode);
            WindowTitle = defaults.WindowTitle ?? string.Empty;
            CustomInfo = defaults.CustomInfo ?? string.Empty;
            SelectedLauncherVisibility = LauncherVisibilityModes.Single(option => option.Mode == defaults.LauncherVisibility);
            SelectedGameProcessPriority = GameProcessPriorities.Single(option => option.Priority == defaults.ProcessPriority);
            SelectedPreferredIpStack = PreferredIpStacks.Single(option => option.Stack == defaults.PreferredIpStack);
            SelectedRendererMode = RendererModes.Single(option => option.Mode == defaults.Renderer);
            PreLaunchCommand = defaults.PreLaunchCommand ?? string.Empty;
            WaitForPreLaunchCommand = defaults.WaitForPreLaunchCommand;
            DisableJavaLaunchWrapper = defaults.DisableJavaLaunchWrapper;
            DisableLegacyFix = defaults.DisableLegacyFix;
            PreferDedicatedGpu = defaults.PreferDedicatedGpu;
            UseJavaExecutable = defaults.UseJavaExecutable;
            DisableLwjglUnsafeAgent = defaults.DisableLwjglUnsafeAgent;
            DisableCrashAnalysis = defaults.DisableCrashAnalysis;
            LockMemory = defaults.LockMemory;
            SelectedGameWindowMode = GameWindowModes.Single(option => option.Mode == defaults.WindowMode);
            CustomGameWindowWidth = defaults.WindowWidth.ToString(System.Globalization.CultureInfo.InvariantCulture);
            CustomGameWindowHeight = defaults.WindowHeight.ToString(System.Globalization.CultureInfo.InvariantCulture);
            UsesCustomGameWindowSize = defaults.WindowMode == MinecraftGameWindowMode.Custom;
            SelectedMemoryAllocationMode = MemoryAllocationModes.Single(option => option.Mode == defaults.MemoryAllocationMode);
            CustomMemoryMiB = defaults.CustomMemoryMiB.ToString(System.Globalization.CultureInfo.InvariantCulture);
            CustomMemorySliderValue = MemoryMiBToSliderStep(defaults.CustomMemoryMiB);
            UsesCustomMemoryAllocation = defaults.MemoryAllocationMode == MinecraftMemoryAllocationMode.Custom;
            RefreshMemoryDisplay();
        }
        finally
        {
            isLoadingPreferences = false;
        }

        await SaveLaunchOptionsAsync();
    }

    /// <summary>
    /// 将“游戏-管理”设置页的全部字段重置为安全默认值并持久化。
    /// 迁移自 PCL-CE PageSetupLeft.Reset 对 PageSetupGameManage 的初始化语义。
    /// </summary>
    public async Task ResetGameManagementSettingsAsync()
    {
        var defaults = GameManagementOptions.Default;
        isLoadingPreferences = true;
        try
        {
            SelectedFileSource = FileSourceOptions.Single(option => option.Value == defaults.FileSource);
            SelectedVersionListSource = VersionListSourceOptions.Single(option => option.Value == defaults.VersionListSource);
            AutoSelectNewInstance = defaults.AutoSelectNewInstance;
            FixAuthlib = defaults.FixAuthlib;
            SelectedCommunitySource = CommunitySourceOptions.Single(option => option.Value == defaults.CommunitySource);
            SelectedCommunityFileNameFormat = CommunityFileNameFormatOptions.Single(option => option.Value == defaults.CommunityFileNameFormat);
            SelectedCommunityModNameStyle = CommunityModNameStyleOptions.Single(option => option.Value == defaults.CommunityModNameStyle);
            SelectedCommunityQuickDownloadBehavior = CommunityQuickDownloadBehaviorOptions.Single(option => option.Value == defaults.QuickDownloadBehavior);
            IgnoreQuilt = defaults.IgnoreQuilt;
            AutoInstallDependencies = defaults.AutoInstallDependencies;
            ReleaseNotifications = defaults.ReleaseNotifications;
            SnapshotNotifications = defaults.SnapshotNotifications;
            AutoChangeGameLanguage = defaults.AutoChangeGameLanguage;
            ReadClipboard = defaults.ReadClipboard;
        }
        finally
        {
            isLoadingPreferences = false;
        }

        await SaveGameManagementOptionsAsync(CancellationToken.None);
    }

    /// <summary>
    /// 将“启动器-个性化”全部字段恢复为 PCL-CE 默认值并持久化。
    /// </summary>
    public async Task ResetInterfaceSettingsAsync()
    {
        var option = ThemeModes.Single(item => item.Mode == LauncherThemeMode.System);
        var defaults = InterfaceSettings.Default;
        isLoadingPreferences = true;
        try
        {
            SelectedThemeMode = option;
            themeService.Apply(option.Mode);
            ApplyInterfaceSettings(defaults);
        }
        finally
        {
            isLoadingPreferences = false;
        }

        await preferencesService.SaveThemeModeAsync(option.Mode);
        await preferencesService.SaveInterfaceSettingsAsync(defaults);
        currentPreferences = currentPreferences with
        {
            ThemeMode = option.Mode,
            InterfaceSettings = defaults,
        };
        ThemeSummary = "个性化设置已初始化。";
    }

    private static string GetLaunchOptionsSummary(MinecraftLaunchOptions options)
    {
        var windowDescription = options.WindowMode switch
        {
            MinecraftGameWindowMode.Default => "默认窗口 854 × 480",
            MinecraftGameWindowMode.Fullscreen => "全屏",
            MinecraftGameWindowMode.Custom => $"自定义窗口 {options.WindowWidth} × {options.WindowHeight}",
            MinecraftGameWindowMode.Launcher => "与启动器尺寸一致",
            MinecraftGameWindowMode.Maximized => "最大化",
            _ => "未知窗口模式",
        };
        var jvmDescription = string.IsNullOrWhiteSpace(options.AdditionalJvmArguments) ? "未设置额外 JVM 参数" : "已设置额外 JVM 参数";
        var gameDescription = string.IsNullOrWhiteSpace(options.AdditionalGameArguments) ? "未设置额外游戏参数" : "已设置额外游戏参数";
        var memoryDescription = options.MemoryAllocationMode == MinecraftMemoryAllocationMode.Automatic
            ? "自动内存分配"
            : $"自定义内存 {options.CustomMemoryMiB} MiB";
        return $"{windowDescription}；{memoryDescription}；{jvmDescription}；{gameDescription}。保存后立即用于下一次启动准备。";
    }

    private void RefreshMemoryDisplay(int? preparedMemoryMiB = null)
    {
        try
        {
            var memory = systemMemoryInfo.Get();
            if (memory.TotalBytes is not { } totalBytes || totalBytes <= 0 ||
                memory.AvailableBytes is not { } availableBytes || availableBytes <= 0)
            {
                MemoryUsedWidth = new GridLength(1, GridUnitType.Star);
                MemoryGameWidth = new GridLength(1, GridUnitType.Star);
                MemoryEmptyWidth = new GridLength(1, GridUnitType.Star);
                return;
            }

            const double bytesPerGiB = 1024d * 1024d * 1024d;
            var totalGiB = Math.Round(totalBytes / bytesPerGiB, 1);
            var availableGiB = Math.Round(Math.Min(totalBytes, availableBytes) / bytesPerGiB, 1);
            var usedGiB = Math.Max(0.1, Math.Round(totalGiB - availableGiB, 1));
            var configuredMiB = int.TryParse(CustomMemoryMiB, out var parsedMemory) ? parsedMemory : MinecraftLaunchOptions.DefaultCustomMemoryMiB;
            var gameGiB = Math.Max(0.1, Math.Round(
                (preparedMemoryMiB ?? (SelectedMemoryAllocationMode.Mode == MinecraftMemoryAllocationMode.Custom
                    ? configuredMiB
                    : Math.Min(configuredMiB, (int)(availableGiB * 1024d)))) / 1024d,
                1));
            var actualGameGiB = Math.Min(gameGiB, availableGiB);
            var emptyGiB = Math.Max(0.1, Math.Round(totalGiB - usedGiB - actualGameGiB, 1));

            MemoryUsedDisplay = $"{usedGiB:N1} GiB";
            MemoryTotalDisplay = $" / {totalGiB:N1} GiB";
            MemoryGameDisplay = gameGiB > availableGiB
                ? $"{gameGiB:N1} GiB（可用 {availableGiB:N1} GiB）"
                : $"{gameGiB:N1} GiB";
            MemoryUsedWidth = new GridLength(usedGiB, GridUnitType.Star);
            MemoryGameWidth = new GridLength(actualGameGiB, GridUnitType.Star);
            MemoryEmptyWidth = new GridLength(emptyGiB, GridUnitType.Star);
            CustomMemorySliderMaximum = Math.Max(
                CustomMemorySliderValue,
                GetMaximumMemorySliderStep(totalGiB));
        }
        catch
        {
            MemoryUsedWidth = new GridLength(1, GridUnitType.Star);
            MemoryGameWidth = new GridLength(1, GridUnitType.Star);
            MemoryEmptyWidth = new GridLength(1, GridUnitType.Star);
        }
    }

    private static int GetMaximumMemorySliderStep(double totalGiB) => totalGiB switch
    {
        <= 1.5 => Math.Max((int)Math.Floor((totalGiB - 0.3) / 0.1), 1),
        <= 8 => (int)Math.Floor((totalGiB - 1.5) / 0.5) + 12,
        <= 16 => (int)Math.Floor(totalGiB - 8) + 25,
        _ => (int)Math.Floor((totalGiB - 16) / 2) + 33,
    };

    private static int MemorySliderStepToMiB(int step)
    {
        var giB = step switch
        {
            <= 12 => step * 0.1 + 0.3,
            <= 25 => (step - 12) * 0.5 + 1.5,
            <= 33 => step - 25 + 8,
            _ => (step - 33) * 2 + 16,
        };
        return (int)Math.Round(giB * 1024d, MidpointRounding.AwayFromZero);
    }

    private static double MemoryMiBToSliderStep(int memoryMiB)
    {
        var giB = memoryMiB / 1024d;
        return giB switch
        {
            <= 1.5 => Math.Clamp(Math.Round((giB - 0.3) / 0.1), 0, 12),
            <= 8 => Math.Clamp(Math.Round((giB - 1.5) / 0.5 + 12), 13, 25),
            <= 16 => Math.Clamp(Math.Round(giB - 8 + 25), 26, 33),
            _ => Math.Max(34, Math.Round((giB - 16) / 2 + 33)),
        };
    }

    private static string GetJavaRequirementSummary(MinecraftJavaRequirement? requirement, JavaInstallation? java)
    {
        if (requirement is null)
        {
            return "版本元数据未提供可验证的 Java 版本要求。";
        }

        var rangeDescriptions = new List<string>();
        if (requirement.MinimumVersion is { } minimumVersion)
        {
            rangeDescriptions.Add($"至少 Java {FormatJavaVersion(minimumVersion)}");
        }
        else if (requirement.MinimumMajorVersion is { } minimum)
        {
            rangeDescriptions.Add($"至少 Java {minimum}");
        }

        if (requirement.MaximumVersion is { } maximumVersion)
        {
            rangeDescriptions.Add($"最高 Java {FormatJavaVersion(maximumVersion)}");
        }
        else if (requirement.MaximumMajorVersion is { } maximum)
        {
            rangeDescriptions.Add($"最高 Java {maximum}");
        }

        var requirementText = rangeDescriptions.Count == 0
            ? "未设定 Java 主版本上下限"
            : string.Join("；", rangeDescriptions);
        var status = java is null
            ? "尚未选择 Java。"
            : requirement.GetBlockingReasons(java) is { Count: > 0 } reasons
                ? string.Join("；", reasons)
                : $"所选 Java {java.MajorVersion?.ToString() ?? "未知"} 满足要求。";
        return $"Java 要求：{requirementText}（{requirement.Source}）。{status}";
    }

    private static string FormatJavaVersion(Version version) =>
        version.Minor == 0 && version.Build > 0
            ? $"{version.Major}u{version.Build}"
            : version.ToString();

    private static string GetMemoryAllocationSummary(MinecraftLaunchPreparation preparation)
    {
        if (preparation.MemoryAllocation is { BlockingReasons.Count: > 0 } blocked)
        {
            return string.Join(Environment.NewLine, blocked.BlockingReasons);
        }

        if (preparation.MemoryAllocation is not { IsReady: true, Allocation: { } allocation })
        {
            return "当前平台未提供内存信息；未额外注入 -Xmx。";
        }

        var expectedArgument = $"-Xmx{allocation.MaximumMemoryMiB}M";
        var effectiveArgument = preparation.ArgumentPreparation.Arguments?.JvmArguments
            .FirstOrDefault(argument => argument.StartsWith("-Xmx", StringComparison.OrdinalIgnoreCase));
        if (effectiveArgument is not null &&
            !string.Equals(effectiveArgument, expectedArgument, StringComparison.OrdinalIgnoreCase))
        {
            return $"高级 JVM 参数将使用 {effectiveArgument}；内存设置计算出的 {allocation.MaximumMemoryMiB} MiB 未重复注入。";
        }

        return $"当前堆内存为 {allocation.MaximumMemoryMiB} MiB（{(allocation.IsAutomatic ? "自动计算" : "自定义")}{(allocation.IsLimitedFor32BitJava ? "；32 位 Java 上限" : string.Empty)}）。";
    }

    [RelayCommand]
    private async Task InstallGameAsync() => await InstallGameCoreAsync(refreshDefaultInstanceCatalog: true);

    private async Task<bool> InstallGameCoreAsync(
        bool refreshDefaultInstanceCatalog,
        MinecraftInstance? targetInstance = null)
    {
        var instance = targetInstance ?? SelectedInstance;
        if (instance is null || (targetInstance is null && !CanInstallGame))
        {
            InstallationSummary = "安装条件尚未满足，未发起下载。";
            return false;
        }

        using var cancellation = new CancellationTokenSource();
        installationCancellation = cancellation;
        IsInstallationRunning = true;
        CanCancelInstallation = true;
        try
        {
            CanInstallGame = false;
            var progress = new Progress<MinecraftInstallationProgress>(update =>
                InstallationSummary = FormatInstallationProgress(update));
            await installationService.InstallAsync(instance, progress, cancellation.Token);
            InstallationSummary = "安装下载完成。资源映射将在下一次显式启动时准备。";
            if (refreshDefaultInstanceCatalog)
            {
                await RefreshAsync();
            }
            else
            {
                await RefreshSelectedInstanceStateAsync();
            }
            return true;
        }
        catch (OperationCanceledException)
        {
            InstallationSummary = "安装已取消。";
            CanInstallGame = true;
            return false;
        }
        catch (Exception exception)
        {
            InstallationSummary = $"安装失败：{exception.Message}";
            CanInstallGame = true;
            return false;
        }
        finally
        {
            if (ReferenceEquals(installationCancellation, cancellation))
            {
                installationCancellation = null;
                CanCancelInstallation = false;
                IsInstallationRunning = false;
            }
        }
    }

    [RelayCommand]
    private void CancelInstallation()
    {
        if (installationCancellation is not { IsCancellationRequested: false })
        {
            return;
        }

        CanCancelInstallation = false;
        InstallationSummary = "正在请求取消下载；已写入的临时文件会被清理。";
        installationCancellation.Cancel();
    }

    private static string FormatInstallationProgress(MinecraftInstallationProgress update)
    {
        var stage = $"[{update.CompletedStages}/{update.TotalStages}] {update.Description}";
        if (update.TotalArtifacts == 0)
        {
            return stage;
        }

        var bytes = update.TotalBytes is { } totalBytes
            ? $"{FormatByteCount(update.DownloadedBytes)} / {FormatByteCount(totalBytes)}"
            : $"已接收 {FormatByteCount(update.DownloadedBytes)}";
        return $"{stage} · {update.CompletedArtifacts}/{update.TotalArtifacts} 个文件已校验 · {bytes} · {update.ActiveArtifacts} 个文件下载中";
    }

    private static string FormatByteCount(long value) => value switch
    {
        < 1024 => $"{value} B",
        < 1024 * 1024 => $"{value / 1024d:0.#} KiB",
        < 1024L * 1024 * 1024 => $"{value / 1024d / 1024d:0.#} MiB",
        _ => $"{value / 1024d / 1024d / 1024d:0.#} GiB",
    };

    [RelayCommand]
    private async Task StartMicrosoftLoginAsync()
    {
        IsMicrosoftAccountMode = true;
        if (!microsoftAuthenticationService.IsConfigured)
        {
            MicrosoftLoginSummary = "正版登录暂不可用，请使用已配置的 PCL Aurora 正式发行版。";
            return;
        }

        using var cancellation = new CancellationTokenSource();
        microsoftLoginCancellation = cancellation;
        IsMicrosoftLoginRunning = true;
        HasMicrosoftDeviceCode = false;
        CanOpenMicrosoftVerificationPage = false;
        microsoftVerificationUri = null;
        CanStartMicrosoftLogin = false;
        CanRestoreMicrosoftLogin = false;
        CanCancelMicrosoftLogin = true;
        try
        {
            MicrosoftLoginSummary = "正在请求 Microsoft 设备代码…";
            var session = await microsoftAuthenticationService.BeginDeviceCodeLoginAsync(cancellation.Token);
            MicrosoftDeviceCode = session.Prompt.UserCode;
            microsoftVerificationUri = session.Prompt.OpenUri;
            HasMicrosoftDeviceCode = true;
            CanOpenMicrosoftVerificationPage = true;
            MicrosoftLoginSummary = "请在打开的网页中输入下方代码。";
            MicrosoftDeviceCodeAvailable?.Invoke(this, session.Prompt.UserCode);
            await openPathService.OpenUriAsync(session.Prompt.OpenUri, cancellation.Token);
            var progress = new Progress<MicrosoftAuthenticationProgress>(update => MicrosoftLoginSummary = update.Description);
            var result = await microsoftAuthenticationService.CompleteDeviceCodeLoginAsync(session, progress, cancellation.Token);
            await microsoftAccountSessionService.PersistAsync(result, cancellation.Token);
            var profile = MicrosoftAccountProfile.FromAuthenticatedAccount(result.Account);
            await preferencesService.SaveMicrosoftAccountAsync(profile, cancellation.Token);
            currentPreferences = currentPreferences with { MicrosoftAccount = profile };
            selectedAccount = result.Account;
            HasAcknowledgedAccountGuidance = false;
            AccountSummary = $"已登录：{result.Account.DisplayName}";
            MicrosoftLoginSummary = "登录成功。";
            MicrosoftAccountDisplayName = result.Account.DisplayName;
            HasMicrosoftAccountProfile = true;
            MicrosoftDeviceCode = "—";
            CanClearMicrosoftLogin = true;
            UpdateLaunchPreflight();
            await RefreshLaunchArgumentPreparationAsync();
            await RefreshGameLaunchPreparationAsync();
        }
        catch (OperationCanceledException)
        {
            MicrosoftLoginSummary = "Microsoft 登录已取消。";
        }
        catch (Exception exception)
        {
            MicrosoftLoginSummary = $"Microsoft 登录失败：{exception.Message}";
        }
        finally
        {
            if (ReferenceEquals(microsoftLoginCancellation, cancellation))
            {
                microsoftLoginCancellation = null;
                IsMicrosoftLoginRunning = false;
                HasMicrosoftDeviceCode = false;
                CanOpenMicrosoftVerificationPage = false;
                microsoftVerificationUri = null;
                CanCancelMicrosoftLogin = false;
                UpdateMicrosoftLoginAvailability(currentPreferences.MicrosoftAccount);
            }
        }
    }

    [RelayCommand]
    private void ShowMicrosoftAccount()
    {
        IsMicrosoftAccountMode = true;
        UpdateMicrosoftLoginAvailability(currentPreferences.MicrosoftAccount);
        AccountSummary = selectedAccount?.Kind == MinecraftAccountKind.Microsoft
            ? $"已登录：{selectedAccount.DisplayName}"
            : currentPreferences.MicrosoftAccount is { } profile
                ? $"已保存账户：{profile.DisplayName}"
                : "尚未登录 Microsoft 账户。";
    }

    [RelayCommand]
    private void ShowOfflineAccount()
    {
        IsMicrosoftAccountMode = false;
        AccountSummary = selectedAccount?.Kind == MinecraftAccountKind.Offline
            ? $"当前账户：{selectedAccount.DisplayName}"
            : "输入游戏用户名后即可使用离线账户。";
    }

    [RelayCommand]
    private async Task OpenMicrosoftVerificationPageAsync()
    {
        if (microsoftVerificationUri is not { } uri)
        {
            return;
        }

        await openPathService.OpenUriAsync(uri);
    }

    [RelayCommand]
    private async Task RestoreMicrosoftLoginAsync()
    {
        IsMicrosoftAccountMode = true;
        if (currentPreferences.MicrosoftAccount is not { } profile)
        {
            MicrosoftLoginSummary = "没有可恢复的 Microsoft 账户。";
            return;
        }

        CanStartMicrosoftLogin = false;
        CanRestoreMicrosoftLogin = false;
        try
        {
            var progress = new Progress<MicrosoftAuthenticationProgress>(update => MicrosoftLoginSummary = update.Description);
            MicrosoftLoginSummary = "正在从系统钥匙串恢复 Microsoft 登录…";
            var restored = await microsoftAccountSessionService.RestoreAsync(profile, progress);
            if (restored.Account is null)
            {
                MicrosoftLoginSummary = restored.Warning ?? "无法恢复 Microsoft 登录。";
                return;
            }

            selectedAccount = restored.Account;
            HasAcknowledgedAccountGuidance = false;
            AccountSummary = $"已登录：{restored.Account.DisplayName}";
            MicrosoftAccountDisplayName = restored.Account.DisplayName;
            MicrosoftLoginSummary = "登录已恢复。";
            UpdateLaunchPreflight();
            await RefreshLaunchArgumentPreparationAsync();
            await RefreshGameLaunchPreparationAsync();
        }
        catch (Exception exception)
        {
            MicrosoftLoginSummary = $"恢复 Microsoft 登录失败：{exception.Message}";
        }
        finally
        {
            UpdateMicrosoftLoginAvailability(currentPreferences.MicrosoftAccount);
        }
    }

    [RelayCommand]
    private void CancelMicrosoftLogin()
    {
        if (microsoftLoginCancellation is not { IsCancellationRequested: false })
        {
            return;
        }

        CanCancelMicrosoftLogin = false;
        MicrosoftLoginSummary = "正在取消 Microsoft 登录…";
        microsoftLoginCancellation.Cancel();
    }

    [RelayCommand]
    private async Task ClearMicrosoftLoginAsync()
    {
        if (currentPreferences.MicrosoftAccount is not { } profile)
        {
            return;
        }

        try
        {
            await microsoftAccountSessionService.RemoveAsync(profile);
            await preferencesService.SaveMicrosoftAccountAsync(null);
            currentPreferences = currentPreferences with { MicrosoftAccount = null };
            if (selectedAccount?.Kind == MinecraftAccountKind.Microsoft)
            {
                selectedAccount = null;
                HasAcknowledgedAccountGuidance = false;
                UpdateLaunchPreflight();
                await RefreshLaunchArgumentPreparationAsync();
                await RefreshGameLaunchPreparationAsync();
            }

            AccountSummary = "已清除 Microsoft 账户及系统钥匙串中的刷新令牌。";
            MicrosoftLoginSummary = "已退出 Microsoft 账户。";
            MicrosoftAccountDisplayName = "尚未登录";
            HasMicrosoftAccountProfile = false;
            MicrosoftDeviceCode = "—";
        }
        catch (Exception exception)
        {
            MicrosoftLoginSummary = $"清除 Microsoft 登录失败：{exception.Message}";
        }
        finally
        {
            UpdateMicrosoftLoginAvailability(currentPreferences.MicrosoftAccount);
        }
    }

    private void UpdateMicrosoftLoginAvailability(MicrosoftAccountProfile? profile)
    {
        HasMicrosoftAccountProfile = profile is not null;
        MicrosoftAccountDisplayName = profile?.DisplayName ?? "尚未登录";
        CanStartMicrosoftLogin = microsoftAuthenticationService.IsConfigured && microsoftLoginCancellation is null;
        CanRestoreMicrosoftLogin = microsoftAuthenticationService.IsConfigured && profile is not null && microsoftLoginCancellation is null;
        CanClearMicrosoftLogin = profile is not null;
        if (!microsoftAuthenticationService.IsConfigured)
        {
            MicrosoftLoginSummary = "正版登录暂不可用，请使用已配置的 PCL Aurora 正式发行版。";
        }
        else if (profile is not null && !CanCancelMicrosoftLogin && selectedAccount?.Kind != MinecraftAccountKind.Microsoft)
        {
            MicrosoftLoginSummary = $"已保存账户 {profile.DisplayName}，点击“恢复登录”继续。";
        }
        else if (profile is null && !CanCancelMicrosoftLogin)
        {
            MicrosoftLoginSummary = "使用 Microsoft 账户登录 Minecraft。";
        }
    }

    [RelayCommand]
    private async Task UseOfflineAccount()
    {
        IsMicrosoftAccountMode = false;
        if (!OfflineAccount.TryCreate(OfflinePlayerName, out var account) || account is null)
        {
            AccountSummary = "离线用户名需为 3–16 位英文字母、数字或下划线。";
            UpdateLaunchPreflight();
            await RefreshLaunchArgumentPreparationAsync();
            await RefreshGameLaunchPreparationAsync();
            return;
        }

        selectedAccount = account;
        HasAcknowledgedAccountGuidance = false;
        try
        {
            await preferencesService.SaveOfflinePlayerNameAsync(account.DisplayName);
            currentPreferences = currentPreferences with { OfflinePlayerName = account.DisplayName };
            AccountSummary = $"已恢复离线账户：{account.DisplayName}。本机仅保存用户名，不保存密码或令牌。";
        }
        catch (Exception exception)
        {
            AccountSummary = $"本次会话使用离线账户：{account.DisplayName}。用户名保存失败：{exception.Message}";
        }

        UpdateLaunchPreflight();
        await RefreshLaunchArgumentPreparationAsync();
        await RefreshGameLaunchPreparationAsync();
    }

    [RelayCommand]
    private async Task ClearOfflineAccountAsync()
    {
        selectedAccount = null;
        HasAcknowledgedAccountGuidance = false;
        OfflinePlayerName = string.Empty;
        try
        {
            await preferencesService.SaveOfflinePlayerNameAsync(null);
            currentPreferences = currentPreferences with { OfflinePlayerName = null };
            AccountSummary = "已清除离线账户；本机不再保存该用户名。";
        }
        catch (Exception exception)
        {
            AccountSummary = $"已清除当前会话账户，但删除本地用户名失败：{exception.Message}";
        }

        UpdateLaunchPreflight();
        await RefreshLaunchArgumentPreparationAsync();
        await RefreshGameLaunchPreparationAsync();
    }

    private void RestoreOfflineAccount(string? playerName)
    {
        if (string.IsNullOrEmpty(playerName))
        {
            return;
        }

        if (!OfflineAccount.TryCreate(playerName, out var account) || account is null)
        {
            AccountSummary = "本地离线用户名无效，未恢复账户。";
            return;
        }

        OfflinePlayerName = account.DisplayName;
        selectedAccount = account;
        HasAcknowledgedAccountGuidance = false;
        AccountSummary = $"已恢复离线账户：{account.DisplayName}。本机仅保存用户名，不保存密码或令牌。";
    }

    [RelayCommand]
    private async Task LaunchGameAsync()
    {
        if (gameLaunchPreparation is null || !gameLaunchPreparation.CanLaunch)
        {
            GameLaunchSummary = "启动条件尚未满足，未启动游戏进程。";
            await TryAppendLauncherLogAsync("Launch", GameLaunchSummary);
            return;
        }

        try
        {
            await TryAppendLauncherLogAsync("Launch", $"准备启动实例：{SelectedInstance?.Name ?? "未知实例"}。");
            var launchPreparation = gameLaunchPreparation;
            if (launchPreparation.RequestPreparation.Request is { } request)
            {
                var adjustedRequest = request.WithLauncherWindowSize(launcherClientWidth, launcherClientHeight);
                launchPreparation = launchPreparation with
                {
                    RequestPreparation = launchPreparation.RequestPreparation with { Request = adjustedRequest },
                };
            }

            var session = await gameLaunchService.LaunchAsync(launchPreparation);
            ToolboxLaunchCount++;
            currentPreferences = currentPreferences with { LaunchCount = ToolboxLaunchCount };
            try
            {
                await preferencesService.ReplaceAsync(currentPreferences);
            }
            catch
            {
                ToolboxStatusText = "游戏已启动，但启动次数保存失败。";
            }
            var visibility = SelectedLauncherVisibility.Mode;
            GameLaunchSummary = $"已启动游戏进程（PID {session.ProcessId}）。输出将用于后续日志页。";
            GameLogLines.Clear();
            HasGameLogLines = false;
            GameLogSummary = $"正在捕获游戏进程 {session.ProcessId} 的输出；日志仅保留在本次会话内。";
            GameProcessStarted?.Invoke(this, visibility);
            await TryAppendLauncherLogAsync("Launch", $"游戏进程已启动，PID {session.ProcessId}。");
            _ = ObserveGameProcessAsync(
                session,
                visibility,
                !currentPreferences.EffectiveLaunchOptions.DisableCrashAnalysis);
        }
        catch (Exception exception)
        {
            GameLaunchSummary = $"启动游戏失败：{exception.Message}";
            CanLaunchGame = false;
            await TryAppendLauncherLogAsync("Launch", GameLaunchSummary);
        }
    }

    public void UpdateLauncherWindowSize(double width, double height)
    {
        if (double.IsNaN(width) || double.IsNaN(height) || width <= 0 || height <= 0)
        {
            return;
        }

        launcherClientWidth = Math.Clamp(
            (int)Math.Round(width),
            MinecraftLaunchOptions.MinimumWindowDimension,
            MinecraftLaunchOptions.MaximumWindowDimension);
        launcherClientHeight = Math.Clamp(
            (int)Math.Round(height),
            MinecraftLaunchOptions.MinimumWindowDimension,
            MinecraftLaunchOptions.MaximumWindowDimension);
    }

    [RelayCommand]
    private async Task OpenGameDirectoryAsync()
    {
        try
        {
            GameDirectorySummary = "正在打开游戏目录…";
            await minecraftDirectoryService.OpenRootDirectoryAsync();
            GameDirectorySummary = "已请求系统文件管理器打开游戏目录。";
        }
        catch (DirectoryNotFoundException)
        {
            GameDirectorySummary = "游戏目录尚不存在。请先创建本地实例或安装游戏；本操作不会创建目录。";
        }
        catch (Exception exception)
        {
            GameDirectorySummary = $"无法打开游戏目录：{exception.Message}";
        }
    }

    [RelayCommand]
    private async Task ClearToolboxCacheAsync()
    {
        if (IsToolboxCacheClearing)
        {
            return;
        }

        IsToolboxCacheClearing = true;
        try
        {
            if (!HasToolboxCacheDirectory)
            {
                ToolboxStatusText = "缓存目录尚未创建，无需清理。";
                return;
            }

            var deleted = 0;
            foreach (var file in Directory.EnumerateFiles(CacheDirectory))
            {
                try
                {
                    File.Delete(file);
                    deleted++;
                }
                catch (IOException)
                {
                    // Cache cleanup is best effort; one locked entry must not hide the result.
                }
                catch (UnauthorizedAccessException)
                {
                    // Cache cleanup is best effort; report the count of entries we could remove.
                }
            }

            foreach (var directory in Directory.EnumerateDirectories(CacheDirectory))
            {
                try
                {
                    Directory.Delete(directory, recursive: true);
                    deleted++;
                }
                catch (IOException)
                {
                }
                catch (UnauthorizedAccessException)
                {
                }
            }

            ToolboxStatusText = deleted == 0
                ? "缓存目录为空，未删除文件。"
                : $"已清理缓存目录中的 {deleted} 项。";
        }
        catch (Exception exception)
        {
            ToolboxStatusText = $"清理缓存失败：{exception.Message}";
        }
        finally
        {
            IsToolboxCacheClearing = false;
            OnPropertyChanged(nameof(HasToolboxCacheDirectory));
        }
    }

    [RelayCommand]
    private Task OpenToolboxCacheDirectoryAsync() =>
        openPathService.OpenFolderAsync(CacheDirectory);

    public Task OpenToolboxFolderAsync(string path) =>
        openPathService.OpenFolderAsync(path);

    [RelayCommand]
    private void ShowToolboxLuck()
    {
        var seed = DateOnly.FromDateTime(DateTime.Now).DayNumber;
        var luck = new Random(seed).Next(0, 101);
        var rating = luck switch
        {
            >= 95 => "今天手气爆棚",
            >= 75 => "今天状态不错",
            >= 50 => "今天平稳发挥",
            >= 25 => "今天适合稳扎稳打",
            _ => "今天先休息一下",
        };
        ToolboxStatusText = $"今日人品：{luck} · {rating}。结果仅供娱乐。";
    }

    [RelayCommand]
    private void ShowToolboxLaunchCount() =>
        ToolboxStatusText = $"PCL Aurora 已启动 {ToolboxLaunchCount} 次。";

    [RelayCommand]
    private void ShowToolboxMemoryOptimization()
    {
        ToolboxStatusText = ToolboxMemoryOptimizationSummary;
    }

    [RelayCommand]
    private void ShowToolboxDontClick()
    {
        ToolboxStatusText = "你还是点了。PCL Aurora 不会在这里执行崩溃、删文件或其他破坏性操作。";
    }

    [RelayCommand]
    private async Task OpenAuroraReleasesAsync()
    {
        try
        {
            AuroraReleaseSummary = "正在打开 PCL Aurora 的发行页…";
            await openPathService.OpenUriAsync(AuroraReleasesUri);
            AuroraReleaseSummary = "已请求系统浏览器打开 PCL Aurora 的发行页。";
        }
        catch (Exception exception)
        {
            AuroraReleaseSummary = $"无法打开 PCL Aurora 的发行页：{exception.Message}";
        }
    }

    [RelayCommand(AllowConcurrentExecutions = false)]
    private async Task CheckForUpdatesAsync()
    {
        try
        {
            IsCheckingForUpdates = true;
            HasAvailableUpdate = false;
            UpdateStatusText = "正在检查更新";
            var result = await launcherUpdateService.CheckAsync(
                LauncherVersionName,
                SelectedUpdateChannel.Value);
            latestUpdateReleaseUri = result.LatestRelease.ReleaseUri;
            UpdateChangelog = result.LatestRelease.Changelog;
            AvailableUpdateVersionDisplay = result.LatestRelease.DisplayName;
            AvailableUpdateSummary = result.LatestRelease.Summary;
            HasAvailableUpdate = result.IsUpdateAvailable;
            UpdateStatusText = result.IsUpdateAvailable ? "发现新版本" : "已是最新版本";
        }
        catch (Exception exception)
        {
            HasAvailableUpdate = false;
            latestUpdateReleaseUri = null;
            UpdateStatusText = "检查更新失败";
            UpdateChangelog = $"暂时无法获取更新日志。\n\n{exception.Message}";
        }
        finally
        {
            IsCheckingForUpdates = false;
        }
    }

    [RelayCommand]
    private async Task InstallAvailableUpdateAsync()
    {
        await openPathService.OpenUriAsync(latestUpdateReleaseUri ?? AuroraReleasesUri);
    }

    [RelayCommand(AllowConcurrentExecutions = false)]
    public async Task RefreshFeedbackAsync()
    {
        feedbackLoadCancellation?.Cancel();
        feedbackLoadCancellation?.Dispose();
        feedbackLoadCancellation = new CancellationTokenSource();
        var cancellationToken = feedbackLoadCancellation.Token;

        IsLoadingFeedback = true;
        HasFeedbackLoadError = false;
        FeedbackStatusText = "正在获取反馈列表";
        try
        {
            var issues = await gitHubIssueService.GetIssuesAsync(cancellationToken);
            foreach (var group in FeedbackGroups)
            {
                group.ReplaceIssues(issues.Where(issue => issue.Status == group.Status));
            }

            HasFeedbackGroups = FeedbackGroups.Any(group => group.HasItems);
            FeedbackStatusText = HasFeedbackGroups
                ? $"已获取 {issues.Count} 条反馈"
                : "暂时没有公开反馈";
            await TryAppendLauncherLogAsync("Feedback", $"反馈列表刷新完成，共 {issues.Count} 条。");
        }
        catch (OperationCanceledException)
        {
            FeedbackStatusText = "反馈列表加载已取消";
        }
        catch (Exception exception)
        {
            foreach (var group in FeedbackGroups)
            {
                group.ReplaceIssues([]);
            }

            HasFeedbackGroups = false;
            HasFeedbackLoadError = true;
            FeedbackStatusText = "暂时无法获取反馈列表，点击重试";
            await TryAppendLauncherLogAsync("Feedback", $"反馈列表刷新失败：{exception.Message}");
        }
        finally
        {
            IsLoadingFeedback = false;
        }
    }

    public Task OpenNewFeedbackAsync() => openPathService.OpenUriAsync(AuroraNewIssueUri);

    public Task OpenFeedbackIssueAsync(GitHubIssue issue)
    {
        ArgumentNullException.ThrowIfNull(issue);
        return openPathService.OpenUriAsync(issue.IssueUri);
    }

    [RelayCommand(AllowConcurrentExecutions = false)]
    public async Task RefreshLauncherLogsAsync()
    {
        IsLoadingLauncherLogs = true;
        LauncherLogStatusText = "正在读取日志列表";
        try
        {
            var files = await launcherLogService.GetFilesAsync();
            LauncherLogFiles.Clear();
            foreach (var file in files)
            {
                LauncherLogFiles.Add(new LauncherLogFileItemViewModel(file));
            }

            HasLauncherLogFiles = LauncherLogFiles.Count > 0;
            LauncherLogStatusText = HasLauncherLogFiles
                ? $"共 {LauncherLogFiles.Count} 个日志文件"
                : "暂无日志";
        }
        catch (Exception exception)
        {
            LauncherLogFiles.Clear();
            HasLauncherLogFiles = false;
            LauncherLogStatusText = $"无法读取日志：{exception.Message}";
        }
        finally
        {
            IsLoadingLauncherLogs = false;
        }
    }

    public async Task ExportLauncherLogsAsync(string destinationPath, bool exportAll)
    {
        var files = await launcherLogService.GetFilesAsync();
        var selected = exportAll ? files : files.Where(file => file.IsCurrent).ToArray();
        await launcherLogService.ExportAsync(selected, destinationPath);
        await TryAppendLauncherLogAsync("Log", exportAll ? "已导出全部日志。" : "已导出当前日志。");
        await RefreshLauncherLogsAsync();
    }

    public Task OpenLauncherLogDirectoryAsync() => openPathService.OpenFolderAsync(launcherLogService.LogDirectory);

    public Task OpenLauncherLogFileAsync(LauncherLogFile file)
    {
        ArgumentNullException.ThrowIfNull(file);
        return openPathService.OpenFileAsync(file.FullPath);
    }

    public async Task<int> ClearLauncherLogHistoryAsync()
    {
        var deleted = await launcherLogService.ClearHistoryAsync();
        await launcherLogService.AppendAsync("Log", $"已清理 {deleted} 个历史日志文件。");
        await RefreshLauncherLogsAsync();
        return deleted;
    }

    [RelayCommand(AllowConcurrentExecutions = false)]
    private async Task LoadContributorsAsync()
    {
        if (contributorsLoadAttempted)
        {
            return;
        }

        contributorsLoadAttempted = true;
        IsLoadingContributors = true;
        ContributorSummary = "正在读取 GitHub 贡献者…";

        try
        {
            var contributors = await gitHubContributorService.GetContributorsAsync();
            foreach (var contributor in Contributors)
            {
                contributor.Dispose();
            }

            Contributors.Clear();
            foreach (var contributor in contributors)
            {
                Contributors.Add(new GitHubContributorItemViewModel(contributor));
            }

            HasContributors = Contributors.Count > 0;
            ContributorSummary = HasContributors
                ? $"已加载 {Contributors.Count} 位贡献者。"
                : "该仓库暂时还没有可显示的代码贡献者。";
        }
        catch (OperationCanceledException)
        {
            contributorsLoadAttempted = false;
            ContributorSummary = "贡献者列表加载已取消。";
        }
        catch (Exception)
        {
            contributorsLoadAttempted = false;
            ContributorSummary = "暂时无法读取 GitHub 贡献者。";
        }
        finally
        {
            IsLoadingContributors = false;
        }
    }

    [RelayCommand]
    private Task OpenContributorPageAsync(Uri? profileUri) =>
        profileUri is null ? Task.CompletedTask : openPathService.OpenUriAsync(profileUri);

    [RelayCommand]
    private async Task OpenProjectPageAsync(string target)
    {
        var uri = target switch
        {
            "source" => AuroraRepositoryUri,
            "issues" => AuroraIssuesUri,
            "license" => AuroraLicenseUri,
            "notice" => new Uri("https://github.com/Micro-ATP/PCL-Aurora/blob/main/NOTICE"),
            "pcl" => PclRepositoryUri,
            "pcl-snapshot" => PclOfficialSnapshotUri,
            "pcl-license" => new Uri("https://github.com/Meloong-Git/PCL/blob/main/LICENCE"),
            "pcl-terms" => new Uri("https://shimo.im/docs/rGrd8pY8xWkt6ryW"),
            "pcl-ce" => PclCeRepositoryUri,
            "pcl-ce-license" => new Uri("https://github.com/PCL-Community/PCL-CE/blob/dev/Plain%20Craft%20Launcher%202/LICENCE"),
            "pcl-core" => new Uri("https://github.com/PCL-Community/PCL-CE/tree/dev/PCL.Core"),
            "apache-license" => new Uri("https://github.com/Micro-ATP/PCL-Aurora/blob/main/LICENSES/Apache-2.0.txt"),
            "mit-license" => new Uri("https://github.com/Micro-ATP/PCL-Aurora/blob/main/LICENSES/MIT.txt"),
            "lucide-license" => new Uri("https://github.com/Micro-ATP/PCL-Aurora/blob/main/LICENSES/Lucide-Icons.txt"),
            "harmony-license" => new Uri("https://github.com/Micro-ATP/PCL-Aurora/blob/main/Fonts/HarmonyOS_Sans_SC/LICENSE.txt"),
            "angle-license" => new Uri("https://github.com/Micro-ATP/PCL-Aurora/blob/main/LICENSES/ANGLE-BSD-3-Clause.txt"),
            "avalonia" => new Uri("https://github.com/AvaloniaUI/Avalonia"),
            "community-toolkit" => new Uri("https://github.com/CommunityToolkit/dotnet"),
            "dotnet" => new Uri("https://github.com/dotnet/dotnet"),
            "protobuf-net" => new Uri("https://github.com/protobuf-net/protobuf-net"),
            "lucide" => new Uri("https://github.com/lucide-icons/lucide"),
            "harmony-font" => new Uri("https://developer.huawei.com/consumer/cn/design/resource/"),
            "skiasharp" => new Uri("https://github.com/mono/SkiaSharp"),
            "runtime-components" => new Uri("https://github.com/AvaloniaUI/Avalonia"),
            "angle" => new Uri("https://github.com/AvaloniaUI/angle"),
            "pcl-sponsor" => new Uri("https://ifdian.net/a/LTCat"),
            "pcl-community" => new Uri("https://github.com/PCL-Community"),
            "bmclapi-sponsor" => new Uri("https://afdian.com/a/bangbang93"),
            "mcmod" => new Uri("https://www.mcmod.cn"),
            "modrinth" => new Uri("https://modrinth.com"),
            "curseforge" => new Uri("https://www.curseforge.com/minecraft"),
            "contributors" => new Uri("https://github.com/Micro-ATP/PCL-Aurora/graphs/contributors"),
            "author" => new Uri("https://github.com/Micro-ATP"),
            _ => throw new ArgumentOutOfRangeException(nameof(target), target, "未知的项目页面。"),
        };

        await openPathService.OpenUriAsync(uri);
    }

    [RelayCommand]
    private Task OpenApplicationDataDirectoryAsync() =>
        openPathService.OpenFolderAsync(ApplicationDataDirectory);

    public Task OpenExternalUriAsync(Uri uri) => openPathService.OpenUriAsync(uri);

    public string GetInterfaceContentDirectory(string contentType)
    {
        var folderName = contentType switch
        {
            "background" => "Pictures",
            "music" => "Musics",
            "title" => "Title",
            _ => throw new ArgumentOutOfRangeException(nameof(contentType), contentType, null),
        };
        var directory = Path.Combine(ApplicationDataDirectory, folderName);
        Directory.CreateDirectory(directory);
        return directory;
    }

    public Task OpenInterfaceContentDirectoryAsync(string contentType) =>
        openPathService.OpenFolderAsync(GetInterfaceContentDirectory(contentType));

    private async Task ObserveGameProcessAsync(
        GameProcessSession session,
        MinecraftLauncherVisibility visibility,
        bool enableCrashAnalysis)
    {
        var outputCount = 0;
        var capturedOutput = new List<string>();
        await foreach (var output in session.Output.ReadAllAsync())
        {
            outputCount++;
            if (capturedOutput.Count >= 4000)
            {
                capturedOutput.RemoveAt(0);
            }
            capturedOutput.Add(output.Text);
            var maximumLines = CreateMiscSettings().MaximumGameLogLines;
            if (maximumLines != int.MaxValue && GameLogLines.Count >= maximumLines)
            {
                GameLogLines.RemoveAt(0);
            }

            GameLogLines.Add(GameLogLine.FromOutput(output));
            HasGameLogLines = true;
            await TryAppendLauncherLogAsync(output.IsError ? "Game/Stderr" : "Game/Stdout", output.Text);
        }

        var exitCode = await session.ExitCode;
        if (exitCode != 0 && enableCrashAnalysis)
        {
            var analysis = PclCeMinecraftCrashAnalyzer.Analyze(exitCode, capturedOutput);
            GameLaunchSummary = $"游戏进程异常退出（代码 {exitCode}）：{analysis.Summary}";
            GameLogSummary = $"自动崩溃分析：{analysis.Summary} 本次会话保留 {GameLogLines.Count} 行输出。";
            await TryAppendLauncherLogAsync("CrashAnalysis", analysis.Summary);
            foreach (var evidence in analysis.Evidence)
            {
                await TryAppendLauncherLogAsync("CrashAnalysis/Evidence", evidence);
            }
        }
        else
        {
            GameLaunchSummary = $"游戏进程已退出（代码 {exitCode}，捕获 {outputCount} 行输出）。";
            GameLogSummary = $"游戏进程已退出（代码 {exitCode}）。本次会话保留 {GameLogLines.Count} 行输出。";
        }
        CanLaunchGame = false;
        await TryAppendLauncherLogAsync("Launch", GameLaunchSummary);
        GameProcessExited?.Invoke(this, visibility);
    }

    private async Task TryAppendLauncherLogAsync(string category, string message)
    {
        try
        {
            await launcherLogService.AppendAsync(category, message);
        }
        catch
        {
            // A file-system logging failure must not interrupt user operations.
        }
    }

    [RelayCommand]
    private void ClearGameLogs()
    {
        GameLogLines.Clear();
        HasGameLogLines = false;
        GameLogSummary = "已清除本次会话中的游戏输出；不会影响游戏进程或本地文件。";
    }

    private void UpdateLaunchPreflight()
    {
        var readiness = launchReadinessService.Evaluate(SelectedInstance, selectedAccount, SelectedJava);
        LaunchPreflightSummary = readiness.CanLaunch
            ? "启动前检查已通过。进程启动条件将继续检查类路径与版本参数。"
            : string.Join(Environment.NewLine, readiness.BlockingReasons);
    }

    private static IReadOnlyList<LauncherLanguageOption> CreateLanguageOptions()
    {
        var autoCulture = ResolveUiCulture(LauncherLocalizationSettings.Auto);
        var names = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["zh-CN"] = "简体中文（中国大陆）",
            ["zh-TW"] = "繁體中文（台灣）",
            ["en-US"] = "English (US)",
            ["en-GB"] = "English (United Kingdom)",
            ["ja-JP"] = "日本語（日本）",
            ["fr-FR"] = "Français (France)",
            ["es-ES"] = "Español (España)",
        };
        var fonts = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["zh-CN"] = "PingFang SC, Microsoft YaHei UI, sans-serif",
            ["zh-TW"] = "PingFang TC, Microsoft JhengHei UI, sans-serif",
            ["en-US"] = "Segoe UI, Arial, sans-serif",
            ["en-GB"] = "Segoe UI, Arial, sans-serif",
            ["ja-JP"] = "Hiragino Sans, Yu Gothic UI, sans-serif",
            ["fr-FR"] = "Segoe UI, Arial, sans-serif",
            ["es-ES"] = "Segoe UI, Arial, sans-serif",
        };

        var result = new List<LauncherLanguageOption>
        {
            new(LauncherLocalizationSettings.Auto, $"跟随系统（{names[autoCulture.Name]}）",
                autoCulture.Name, fonts[autoCulture.Name]),
        };
        result.AddRange(LauncherLocalizationSettings.SupportedLanguageCodes.Select(code =>
            new LauncherLanguageOption(code, names[code], code, fonts[code])));
        return result;
    }

    private static IReadOnlyList<LauncherFormatCultureOption> CreateFormatCultureOptions()
    {
        var result = new List<LauncherFormatCultureOption>
        {
            new(LauncherLocalizationSettings.Auto, "跟随系统区域格式"),
            new(LauncherLocalizationSettings.FollowInterfaceLanguage, "同步界面语言"),
        };
        result.AddRange(LauncherLocalizationSettings.SupportedLanguageCodes.Select(code =>
            new LauncherFormatCultureOption(code, CultureInfo.GetCultureInfo(code).NativeName)));
        return result;
    }
}

public sealed record MinecraftGameWindowModeOption(MinecraftGameWindowMode Mode, string DisplayName);

public sealed record MinecraftMemoryAllocationModeOption(MinecraftMemoryAllocationMode Mode, string DisplayName);

public sealed record MinecraftInstanceIsolationModeOption(MinecraftInstanceIsolationMode Mode, string DisplayName);

public sealed record MinecraftLauncherVisibilityOption(MinecraftLauncherVisibility Mode, string DisplayName);

public sealed record MinecraftGameProcessPriorityOption(MinecraftGameProcessPriority Priority, string DisplayName);

public sealed record MinecraftPreferredIpStackOption(MinecraftPreferredIpStack Stack, string DisplayName);

public sealed record MinecraftRendererModeOption(MinecraftRendererMode Mode, string DisplayName);

public sealed record LauncherLanguageOption(string Code, string DisplayName, string CultureName, string FontFamily);

public sealed record LauncherFormatCultureOption(string Code, string DisplayName);

public sealed record CommunityResourceSortOption(CommunityResourceSort Sort, string DisplayName);

public sealed record CommunityResourceLoaderOption(CommunityResourceLoader Loader, string DisplayName);

public sealed record CommunityResourceCategoryOption(string? Category, string DisplayName);
