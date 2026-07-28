using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
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
    ICommunityResourceIconService communityResourceIconService,
    ICommunityResourceVersionService communityResourceVersionService,
    ICommunityResourceInstallationService communityResourceInstallationService,
    IMinecraftLoaderCatalogService loaderCatalogService,
    IMinecraftOfficialLoaderCatalogService officialLoaderCatalogService,
    IMinecraftLoaderInstallerService loaderInstallerService,
    IMinecraftVersionProvisioningService versionProvisioningService,
    IMinecraftDirectoryService minecraftDirectoryService,
    IMinecraftGameLaunchService gameLaunchService,
    ILauncherPreferencesService preferencesService,
    IMicrosoftAccountAuthenticationService microsoftAuthenticationService,
    IMicrosoftAccountSessionService microsoftAccountSessionService,
    IOpenPathService openPathService,
    IThemeService themeService) : ViewModelBase
{
    public event EventHandler<string>? MicrosoftDeviceCodeAvailable;

    private const int MaximumGameLogLines = 500;
    private static readonly Uri AuroraReleasesUri = new("https://github.com/Micro-ATP/PCL-Aurora/releases");
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

    private readonly List<MinecraftVersionCatalogEntry> allCatalogVersions = [];
    private MinecraftAccount? selectedAccount;
    private MinecraftLoaderCatalog? loaderCatalog;
    private MinecraftLoaderKind? loaderKindFilter;
    private CommunityResourceType? communityResourceType;
    private MinecraftGameLaunchPreparation? gameLaunchPreparation;
    private LauncherPreferences currentPreferences = LauncherPreferences.Default;
    private bool isRefreshing;
    private bool isLoadingPreferences;
    private bool isSelectingJavaForRequirement;
    private MinecraftJavaRequirement? currentJavaRequirement;
    private CancellationTokenSource? installationCancellation;
    private CancellationTokenSource? microsoftLoginCancellation;
    private Uri? microsoftVerificationUri;
    private CancellationTokenSource? communitySearchCancellation;
    private CancellationTokenSource? communityIconCancellation;
    private CancellationTokenSource? communityVersionCancellation;
    private CancellationTokenSource? communityInstallationCancellation;

    public ObservableCollection<MinecraftVersionCatalogEntry> AvailableVersions { get; } = [];

    public ObservableCollection<MinecraftLoaderCatalogEntry> AvailableLoaders { get; } = [];

    public ObservableCollection<CommunityResourceItemViewModel> CommunityResources { get; } = [];

    public ObservableCollection<CommunityResourceVersion> CommunityResourceVersions { get; } = [];

    public ObservableCollection<MinecraftInstance> AvailableInstances { get; } = [];

    public ObservableCollection<JavaInstallation> AvailableJavaInstallations { get; } = [];

    public ObservableCollection<GameLogLine> GameLogLines { get; } = [];

    public string MinecraftRootDirectory { get; } = minecraftDirectoryService.GetRootDirectory();

    public IReadOnlyList<ThemeOption> ThemeModes { get; } =
    [
        new(LauncherThemeMode.System, "跟随系统"),
        new(LauncherThemeMode.Light, "浅色"),
        new(LauncherThemeMode.Dark, "深色"),
    ];

    public IReadOnlyList<int> DownloadConcurrencyOptions { get; } =
        Enumerable.Range(
            LauncherDownloadSettings.MinimumConcurrency,
            LauncherDownloadSettings.MaximumConcurrency - LauncherDownloadSettings.MinimumConcurrency + 1)
        .ToArray();

    public IReadOnlyList<DownloadSpeedOption> DownloadSpeedOptions { get; } = DownloadSpeedOption.CreateAll();

    public IReadOnlyList<MinecraftGameWindowModeOption> GameWindowModes { get; } =
    [
        new(MinecraftGameWindowMode.Default, "默认窗口（854 × 480）"),
        new(MinecraftGameWindowMode.Fullscreen, "全屏"),
        new(MinecraftGameWindowMode.Custom, "自定义尺寸"),
    ];

    public IReadOnlyList<MinecraftMemoryAllocationModeOption> MemoryAllocationModes { get; } =
    [
        new(MinecraftMemoryAllocationMode.Automatic, "自动分配"),
        new(MinecraftMemoryAllocationMode.Custom, "自定义 MiB"),
    ];

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
        LauncherThemeMode.Light => "浅色",
        LauncherThemeMode.Dark => "深色",
        _ => throw new ArgumentOutOfRangeException(nameof(themeService.CurrentMode)),
    });

    [ObservableProperty]
    private string themeSummary = "正在读取本地主题偏好…";

    [ObservableProperty]
    private int selectedDownloadConcurrency = LauncherDownloadSettings.DefaultConcurrency;

    [ObservableProperty]
    private DownloadSpeedOption selectedDownloadSpeedLimit = new(
        LauncherDownloadSettings.UnlimitedSpeedLimitStep,
        LauncherDownloadSettings.GetSpeedLimitDisplayName(LauncherDownloadSettings.UnlimitedSpeedLimitStep));

    [ObservableProperty]
    private string downloadSettingsSummary = "正在读取本地下载设置…";

    [ObservableProperty]
    private string additionalJvmArguments = string.Empty;

    [ObservableProperty]
    private string additionalGameArguments = string.Empty;

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
    private bool hasGameLogLines;

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
    private bool isVersionDetailLoading;

    [ObservableProperty]
    private string versionDetailSummary = "选择版本后将检查可添加的组件。";

    [ObservableProperty]
    private string selectedVersionForgeAvailability = "正在检查";

    [ObservableProperty]
    private string selectedVersionNeoForgeAvailability = "正在检查";

    [ObservableProperty]
    private string selectedVersionFabricAvailability = "正在检查";

    [ObservableProperty]
    private string selectedVersionOptiFineAvailability = "正在检查";

    [ObservableProperty]
    private string loaderCatalogPath = string.Empty;

    [ObservableProperty]
    private string loaderCatalogSummary = "可导入本地加载器目录 JSON。";

    [ObservableProperty]
    private bool isOfficialLoaderCatalogLoading;

    [ObservableProperty]
    private string loaderSelectionSummary = "请先导入目录并选择一个 Minecraft 版本；不会下载或执行安装器。";

    [ObservableProperty]
    private bool hasAvailableLoaders;

    [ObservableProperty]
    private MinecraftLoaderCatalogEntry? selectedLoader;

    [ObservableProperty]
    private bool canInstallSelectedLoader;

    [ObservableProperty]
    private string communitySearchText = string.Empty;

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
    private bool canInstallCommunityResource;

    [ObservableProperty]
    private bool canCancelCommunityResourceOperation;

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

    public bool IsCommunityResultListVisible => HasCommunityResources && !IsCommunitySearchRunning;

    public bool IsCommunityFooterVisible => IsCommunityCatalogAvailable && !IsCommunitySearchRunning;

    public bool IsCommunityStatusVisible =>
        !IsCommunitySearchRunning && !HasCommunityResources && !string.IsNullOrWhiteSpace(CommunityResourceSummary);

    public bool IsCommunityVersionCardVisible => SelectedCommunityResource is not null && !IsCommunitySearchRunning;

    public bool IsLoaderPageLoading => IsVersionCatalogLoading || IsOfficialLoaderCatalogLoading;

    public Task LoadVersionCatalogPageAsync() => RefreshVersionCatalogAsync();

    public Task LoadOfficialLoaderCatalogPageAsync() => RefreshOfficialLoaderCatalogAsync();

    public async Task LoadSelectedVersionDetailAsync()
    {
        if (SelectedCatalogVersion is null || IsVersionDetailLoading)
        {
            return;
        }

        IsVersionDetailLoading = true;
        SelectedVersionForgeAvailability = "正在检查";
        SelectedVersionNeoForgeAvailability = "正在检查";
        SelectedVersionFabricAvailability = "正在检查";
        SelectedVersionOptiFineAvailability = "正在检查";
        try
        {
            var result = await officialLoaderCatalogService.FetchAsync(SelectedCatalogVersion.Id);
            var entries = result.Catalog?.Entries ?? [];
            SelectedVersionForgeAvailability = FormatAvailability(entries, MinecraftLoaderKind.Forge, result.Errors);
            SelectedVersionNeoForgeAvailability = FormatAvailability(entries, MinecraftLoaderKind.NeoForge, result.Errors);
            SelectedVersionFabricAvailability = FormatAvailability(entries, MinecraftLoaderKind.Fabric, result.Errors);
            SelectedVersionOptiFineAvailability = FormatAvailability(entries, MinecraftLoaderKind.OptiFine, result.Errors);
            VersionDetailSummary = result.Errors.Count == 0
                ? $"已检查 Minecraft {SelectedCatalogVersion.Id} 的可选组件。"
                : $"已读取可用组件，部分目录暂时不可用：{string.Join("；", result.Errors)}";
        }
        catch (OperationCanceledException)
        {
            VersionDetailSummary = "组件目录检查已取消。";
        }
        catch (Exception exception)
        {
            SelectedVersionForgeAvailability = "获取失败";
            SelectedVersionNeoForgeAvailability = "获取失败";
            SelectedVersionFabricAvailability = "获取失败";
            SelectedVersionOptiFineAvailability = "获取失败";
            VersionDetailSummary = $"组件目录检查失败：{exception.Message}";
        }
        finally
        {
            IsVersionDetailLoading = false;
        }
    }

    public async Task SaveSelectedVersionClientCoreAsync(string destinationDirectory)
    {
        if (SelectedCatalogVersion is null)
        {
            return;
        }

        try
        {
            VersionCatalogSummary = $"正在另存 {SelectedCatalogVersion.Id} 的客户端核心…";
            var target = await versionArchiveService.SaveClientCoreAsync(SelectedCatalogVersion, destinationDirectory);
            VersionCatalogSummary = $"客户端核心已保存到 {target}。";
        }
        catch (OperationCanceledException)
        {
            VersionCatalogSummary = "另存为已取消。";
        }
        catch (Exception exception)
        {
            VersionCatalogSummary = $"另存为失败：{exception.Message}";
        }
    }

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

    public Task LoadCommunityResourcePageAsync() => LoadCommunityResourcesAsync(0);

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
            AvailableJavaInstallations.Clear();
            foreach (var java in diagnostics.JavaInstallations.Where(java => java.IsCompatible))
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
            JavaSummary = diagnostics.JavaInstallations.Count == 0
                ? "未发现可用 Java。"
                : $"发现 {diagnostics.JavaInstallations.Count} 个 Java，其中 {AvailableJavaInstallations.Count} 个与当前架构兼容。";
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
        await LoadPreferencesAsync();
        await RefreshAsync();
    }

    private async Task LoadPreferencesAsync()
    {
        try
        {
            isLoadingPreferences = true;
            var result = await preferencesService.LoadAsync();
            currentPreferences = result.Preferences;
            var option = ThemeModes.Single(item => item.Mode == result.Preferences.ThemeMode);
            SelectedThemeMode = option;
            themeService.Apply(option.Mode);
            ThemeSummary = result.Warning ?? $"当前使用{option.DisplayName}主题；该偏好已保存到本机。";
            SelectedDownloadConcurrency = result.Preferences.DownloadConcurrency;
            SelectedDownloadSpeedLimit = DownloadSpeedOptions.Single(option => option.Step == result.Preferences.DownloadSpeedLimitStep);
            DownloadSettingsSummary = result.Warning ?? GetDownloadSettingsSummary(result.Preferences);
            var launchOptions = result.Preferences.EffectiveLaunchOptions;
            AdditionalJvmArguments = launchOptions.AdditionalJvmArguments ?? string.Empty;
            AdditionalGameArguments = launchOptions.AdditionalGameArguments ?? string.Empty;
            SelectedGameWindowMode = GameWindowModes.Single(option => option.Mode == launchOptions.WindowMode);
            CustomGameWindowWidth = launchOptions.WindowWidth.ToString(System.Globalization.CultureInfo.InvariantCulture);
            CustomGameWindowHeight = launchOptions.WindowHeight.ToString(System.Globalization.CultureInfo.InvariantCulture);
            UsesCustomGameWindowSize = launchOptions.WindowMode == MinecraftGameWindowMode.Custom;
            SelectedMemoryAllocationMode = MemoryAllocationModes.Single(option => option.Mode == launchOptions.MemoryAllocationMode);
            CustomMemoryMiB = launchOptions.CustomMemoryMiB.ToString(System.Globalization.CultureInfo.InvariantCulture);
            UsesCustomMemoryAllocation = launchOptions.MemoryAllocationMode == MinecraftMemoryAllocationMode.Custom;
            LaunchOptionsSummary = result.Warning ?? GetLaunchOptionsSummary(launchOptions);
            RestoreOfflineAccount(result.Preferences.OfflinePlayerName);
            UpdateMicrosoftLoginAvailability(result.Preferences.MicrosoftAccount);
        }
        catch (Exception exception)
        {
            ThemeSummary = $"无法读取本地主题偏好：{exception.Message}；当前跟随系统主题。";
            DownloadSettingsSummary = "无法读取本地下载设置；已使用安全默认值。";
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
                SelectedCatalogVersion = null;
                LatestReleaseVersion = null;
                LatestSnapshotVersion = null;
                VersionCatalogSummary = string.Join(Environment.NewLine, result.Errors);
                return;
            }

            allCatalogVersions.Clear();
            allCatalogVersions.AddRange(result.Catalog.Versions);
            LatestReleaseVersion = allCatalogVersions.FirstOrDefault(version =>
                string.Equals(version.Type, "release", StringComparison.OrdinalIgnoreCase));
            LatestSnapshotVersion = allCatalogVersions.FirstOrDefault(version =>
                string.Equals(version.Type, "snapshot", StringComparison.OrdinalIgnoreCase));
            ApplyVersionFilters(result.Catalog.LatestRelease);
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
            VersionCatalogSummary = $"正在 {rootDirectory} 创建 {version.Id}…";
            var instance = await versionProvisioningService.ProvisionAsync(version, rootDirectory);
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
            if (!CanInstallGame)
            {
                InstallationSummary = "当前实例的下载计划尚未准备完成。";
                return;
            }

            await InstallGameCoreAsync(refreshDefaultInstanceCatalog: false);
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
        var filtered = MinecraftVersionCatalogFilter.FilterByCategory(
            allCatalogVersions,
            VersionSearchText,
            SelectedVersionCategory);

        AvailableVersions.Clear();
        foreach (var version in filtered)
        {
            AvailableVersions.Add(version);
        }

        SelectedCatalogVersion = AvailableVersions.FirstOrDefault(version => version.Id == selectedId)
            ?? AvailableVersions.FirstOrDefault();
        VersionCatalogSummary = AvailableVersions.Count == 0
            ? "没有符合当前筛选条件的官方版本；不会创建实例。"
            : $"已加载 {allCatalogVersions.Count} 个官方版本；当前分类显示 {AvailableVersions.Count} 个。";
    }

    private static string FormatAvailability(
        IReadOnlyList<MinecraftLoaderCatalogEntry> entries,
        MinecraftLoaderKind kind,
        IReadOnlyList<string> errors) =>
        entries.Any(entry => entry.Kind == kind)
            ? "可以添加"
            : errors.Any(error => IsLoaderSourceError(error, kind))
                ? "获取失败"
                : "无可用版本";

    private static bool IsLoaderSourceError(string error, MinecraftLoaderKind kind)
    {
        if (error.StartsWith("无法获取加载器目录", StringComparison.OrdinalIgnoreCase) ||
            error.StartsWith("官方加载器目录格式无效", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var source = kind switch
        {
            MinecraftLoaderKind.Forge => "无法获取 Forge 目录",
            MinecraftLoaderKind.NeoForge => "无法获取 NeoForge",
            MinecraftLoaderKind.Fabric => "无法获取 Fabric 目录",
            MinecraftLoaderKind.OptiFine => "无法获取 OptiFine 公开目录",
            _ => string.Empty,
        };
        return source.Length > 0 && error.StartsWith(source, StringComparison.OrdinalIgnoreCase);
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
        RefreshCommunityInstallState();
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
        communityVersionCancellation?.Cancel();
        communityVersionCancellation = null;
        IsCommunityVersionLoading = false;
        if (communityInstallationCancellation is null)
        {
            CanCancelCommunityResourceOperation = false;
        }
        CommunityResourceVersions.Clear();
        SelectedCommunityResourceVersion = null;
        HasCommunityResourceVersions = false;
        CanOpenCommunityResource = value is not null;
        CanLoadCommunityResourceVersions = value is not null;
        CommunityVersionSummary = value is null
            ? "选择项目后可查看适合当前实例的版本。"
            : "正在准备版本列表…";
        OnPropertyChanged(nameof(IsCommunityVersionCardVisible));
        RefreshCommunityInstallState();
        if (value is not null)
        {
            _ = LoadCommunityResourceVersionsAsync();
        }
    }

    partial void OnSelectedCommunityResourceVersionChanged(CommunityResourceVersion? value)
    {
        RefreshCommunityInstallState();
        if (value is not null)
        {
            var type = SelectedCommunityResource?.Project.Type;
            var versionCount = CommunityResourceVersions.Count;
            var summaryPrefix = versionCount > 0 ? $"共 {versionCount} 个版本；" : string.Empty;
            CommunityVersionSummary = type switch
            {
                CommunityResourceType.DataPack or CommunityResourceType.ModPack =>
                    $"{summaryPrefix}{value.FileSummary}；{value.DependencySummary}。{ProjectTypeInstallHint(type)}",
                CommunityResourceType.Mod when SelectedInstance?.InstalledLoader?.Kind is not
                    (MinecraftLoaderKind.Forge or MinecraftLoaderKind.NeoForge or MinecraftLoaderKind.Fabric) =>
                    $"{summaryPrefix}{value.FileSummary}；{value.DependencySummary}。目标实例需要兼容的模组加载器。",
                _ => $"{summaryPrefix}{value.FileSummary}；{value.DependencySummary}。",
            };
        }
    }

    partial void OnCommunityPageChanged(int value) => OnPropertyChanged(nameof(CommunityPageNumber));

    partial void OnHasCommunityResourcesChanged(bool value)
    {
        OnPropertyChanged(nameof(IsCommunityResultListVisible));
        OnPropertyChanged(nameof(IsCommunityStatusVisible));
    }

    partial void OnCommunityResourceSummaryChanged(string value) =>
        OnPropertyChanged(nameof(IsCommunityStatusVisible));

    partial void OnIsCommunityCatalogAvailableChanged(bool value) =>
        OnPropertyChanged(nameof(IsCommunityFooterVisible));

    partial void OnIsCommunitySearchRunningChanged(bool value)
    {
        OnPropertyChanged(nameof(IsCommunityResultListVisible));
        OnPropertyChanged(nameof(IsCommunityFooterVisible));
        OnPropertyChanged(nameof(IsCommunityStatusVisible));
        OnPropertyChanged(nameof(IsCommunityVersionCardVisible));
    }

    partial void OnIsVersionCatalogLoadingChanged(bool value) =>
        OnPropertyChanged(nameof(IsLoaderPageLoading));

    partial void OnIsOfficialLoaderCatalogLoadingChanged(bool value) =>
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
        communityInstallationCancellation?.Cancel();
        communityInstallationCancellation = null;
        CancelCommunityIconLoading();
        IsCommunitySearchRunning = false;
        CanCancelCommunitySearch = false;
        IsCommunityVersionLoading = false;
        CanCancelCommunityResourceOperation = false;
        CommunityResourceVersions.Clear();
        SelectedCommunityResourceVersion = null;
        HasCommunityResourceVersions = false;
        CanLoadCommunityResourceVersions = false;
        CanInstallCommunityResource = false;
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
            : ModLoaderOptions;
        SelectedCommunityResourceLoader = CommunityResourceLoaderOptions[0];
        SelectedCommunityResourceSort = CommunityResourceSortOptions[0];
        IsCommunityLoaderFilterVisible = communityResourceType is
            CommunityResourceType.Mod or CommunityResourceType.ModPack or CommunityResourceType.Shader;
        IsCommunityCatalogAvailable = communityResourceType is not null and not CommunityResourceType.World;
        CanSearchCommunityResources = IsCommunityCatalogAvailable;
        CommunityResourceSummary = section switch
        {
            "favorites" => "暂无收藏",
            "world" => "世界资源暂不可用",
            _ => string.Empty,
        };
    }

    [RelayCommand]
    private Task SearchCommunityResourcesAsync() => LoadCommunityResourcesAsync(0);

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
            CommunityResourceSummary = $"正在打开 {project.Title} 的 Modrinth 项目页…";
            await openPathService.OpenUriAsync(project.WebsiteUrl);
            CommunityResourceSummary = $"已打开 {project.Title}。";
        }
        catch (Exception exception)
        {
            CommunityResourceSummary = $"无法打开项目页：{exception.Message}";
        }
    }

    [RelayCommand]
    private async Task LoadCommunityResourceVersionsAsync()
    {
        if (SelectedCommunityResource?.Project is not { } project ||
            communityInstallationCancellation is not null)
        {
            return;
        }

        communityVersionCancellation?.Cancel();
        using var cancellation = new CancellationTokenSource();
        communityVersionCancellation = cancellation;
        CommunityResourceVersions.Clear();
        SelectedCommunityResourceVersion = null;
        HasCommunityResourceVersions = false;
        IsCommunityVersionLoading = true;
        CanLoadCommunityResourceVersions = false;
        CanCancelCommunityResourceOperation = true;
        CommunityVersionSummary = $"正在获取 {project.Title} 的可用版本…";
        try
        {
            var gameVersion = GetMinecraftVersionForLoaders(SelectedInstance);
            if (string.IsNullOrWhiteSpace(gameVersion))
            {
                gameVersion = string.IsNullOrWhiteSpace(CommunityGameVersion) ? null : CommunityGameVersion.Trim();
            }

            var loader = project.Type is CommunityResourceType.Mod or CommunityResourceType.ModPack
                ? GetCommunityResourceLoaderForSelectedInstance()
                : CommunityResourceLoader.Any;
            var catalog = await communityResourceVersionService.GetProjectVersionsAsync(
                project.Id,
                gameVersion,
                loader,
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

            HasCommunityResourceVersions = CommunityResourceVersions.Count > 0;
            CommunityVersionSummary = HasCommunityResourceVersions
                ? $"找到 {CommunityResourceVersions.Count} 个兼容版本。"
                : catalog.Errors.Count > 0
                    ? $"版本列表不可用：{string.Join("；", catalog.Errors)}"
                    : "没有适合当前 Minecraft 版本与加载器的文件。";
            SelectedCommunityResourceVersion = CommunityResourceVersions.FirstOrDefault();
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
                RefreshCommunityInstallState();
            }
        }
    }

    [RelayCommand]
    private async Task InstallCommunityResourceAsync()
    {
        if (!CanInstallCommunityResource ||
            SelectedCommunityResource?.Project is not { } project ||
            SelectedCommunityResourceVersion is not { } version ||
            SelectedInstance is not { } instance)
        {
            CommunityVersionSummary = ProjectTypeInstallHint(SelectedCommunityResource?.Project.Type);
            return;
        }

        using var cancellation = new CancellationTokenSource();
        communityInstallationCancellation = cancellation;
        CanInstallCommunityResource = false;
        CanLoadCommunityResourceVersions = false;
        CanCancelCommunityResourceOperation = true;
        try
        {
            var progress = new Progress<MinecraftDownloadProgress>(update =>
            {
                var size = update.TotalBytes is { } total
                    ? $"{FormatByteCount(update.DownloadedBytes)} / {FormatByteCount(total)}"
                    : FormatByteCount(update.DownloadedBytes);
                CommunityVersionSummary = $"正在安装 {update.CurrentDescription} · {update.CompletedArtifacts}/{update.TotalArtifacts} · {size}";
            });
            var result = await communityResourceInstallationService.InstallAsync(
                project,
                version,
                instance,
                progress,
                cancellation.Token);
            if (!ReferenceEquals(communityInstallationCancellation, cancellation))
            {
                return;
            }

            CommunityVersionSummary = result.InstalledDependencyCount > 0
                ? $"已安装 {project.Title} 和 {result.InstalledDependencyCount} 项必要依赖。"
                : $"已安装 {project.Title}。";
        }
        catch (OperationCanceledException)
        {
            if (ReferenceEquals(communityInstallationCancellation, cancellation))
            {
                CommunityVersionSummary = "社区资源安装已取消。";
            }
        }
        catch (Exception exception)
        {
            if (ReferenceEquals(communityInstallationCancellation, cancellation))
            {
                CommunityVersionSummary = $"安装失败：{exception.Message}";
            }
        }
        finally
        {
            if (ReferenceEquals(communityInstallationCancellation, cancellation))
            {
                communityInstallationCancellation = null;
                CanCancelCommunityResourceOperation = false;
                CanLoadCommunityResourceVersions = SelectedCommunityResource is not null;
                RefreshCommunityInstallState();
            }
        }
    }

    [RelayCommand]
    private void CancelCommunityResourceOperation()
    {
        communityVersionCancellation?.Cancel();
        communityInstallationCancellation?.Cancel();
        CanCancelCommunityResourceOperation = false;
    }

    private void RefreshCommunityInstallState()
    {
        var type = SelectedCommunityResource?.Project.Type;
        var hasCompatibleTarget = type is CommunityResourceType.ResourcePack or CommunityResourceType.Shader ||
                                  type == CommunityResourceType.Mod &&
                                  SelectedInstance?.InstalledLoader?.Kind is
                                      MinecraftLoaderKind.Forge or MinecraftLoaderKind.NeoForge or MinecraftLoaderKind.Fabric;
        CanInstallCommunityResource =
            communityInstallationCancellation is null &&
            !IsCommunityVersionLoading &&
            SelectedInstance?.Status == MinecraftInstanceStatus.Valid &&
            SelectedCommunityResourceVersion is not null &&
            hasCompatibleTarget;
    }

    private CommunityResourceLoader GetCommunityResourceLoaderForSelectedInstance() =>
        SelectedInstance?.InstalledLoader?.Kind switch
        {
            MinecraftLoaderKind.Forge => CommunityResourceLoader.Forge,
            MinecraftLoaderKind.NeoForge => CommunityResourceLoader.NeoForge,
            MinecraftLoaderKind.Fabric => CommunityResourceLoader.Fabric,
            _ => SelectedCommunityResourceLoader.Loader,
        };

    private static string ProjectTypeInstallHint(CommunityResourceType? type) => type switch
    {
        CommunityResourceType.DataPack => "数据包需要先选择存档世界，不能直接装入实例。",
        CommunityResourceType.ModPack => "整合包需要创建或导入独立实例，不能作为普通文件安装。",
        _ => "请先选择可用实例和资源版本。",
    };

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

            CancelCommunityIconLoading();
            ClearCommunityResources();
            var items = result.Projects.Select(project => new CommunityResourceItemViewModel(project)).ToArray();
            foreach (var item in items)
            {
                CommunityResources.Add(item);
            }

            CommunityPage = page;
            HasCommunityResources = CommunityResources.Count > 0;
            StartCommunityIconLoading(items);
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
            if (ReferenceEquals(communitySearchCancellation, cancellation))
            {
                communitySearchCancellation = null;
                IsCommunitySearchRunning = false;
                CanCancelCommunitySearch = false;
                CanSearchCommunityResources = IsCommunityCatalogAvailable;
            }
        }
    }

    private void StartCommunityIconLoading(IReadOnlyList<CommunityResourceItemViewModel> items)
    {
        CancelCommunityIconLoading();
        var cancellation = new CancellationTokenSource();
        communityIconCancellation = cancellation;
        _ = LoadCommunityIconsAsync(items, cancellation);
    }

    private async Task LoadCommunityIconsAsync(
        IReadOnlyList<CommunityResourceItemViewModel> items,
        CancellationTokenSource cancellation)
    {
        try
        {
            await Task.WhenAll(items.Select(item => LoadCommunityIconAsync(item, cancellation.Token)));
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            if (ReferenceEquals(communityIconCancellation, cancellation))
            {
                communityIconCancellation = null;
            }

            cancellation.Dispose();
        }
    }

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
            if (bytes is null || cancellationToken.IsCancellationRequested || !CommunityResources.Contains(item))
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

    private void CancelCommunityIconLoading()
    {
        communityIconCancellation?.Cancel();
        communityIconCancellation = null;
    }

    private void ClearCommunityResources()
    {
        foreach (var item in CommunityResources)
        {
            item.Dispose();
        }

        CommunityResources.Clear();
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

    partial void OnSelectedDownloadConcurrencyChanged(int value)
    {
        if (!isLoadingPreferences)
        {
            _ = SaveDownloadConcurrencyPreferenceAsync(value);
        }
    }

    partial void OnSelectedDownloadSpeedLimitChanged(DownloadSpeedOption value)
    {
        if (!isLoadingPreferences)
        {
            _ = SaveDownloadSpeedLimitPreferenceAsync(value.Step);
        }
    }

    private async Task SaveDownloadConcurrencyPreferenceAsync(int value)
    {
        try
        {
            DownloadSettingsSummary = "正在保存下载并发设置…";
            await preferencesService.SaveDownloadConcurrencyAsync(value);
            currentPreferences = currentPreferences with { DownloadConcurrency = value };
            DownloadSettingsSummary = GetDownloadSettingsSummary(currentPreferences);
        }
        catch (Exception exception)
        {
            DownloadSettingsSummary = $"下载并发设置保存失败：{exception.Message}";
        }
    }

    private async Task SaveDownloadSpeedLimitPreferenceAsync(int value)
    {
        try
        {
            DownloadSettingsSummary = "正在保存下载限速设置…";
            await preferencesService.SaveDownloadSpeedLimitStepAsync(value);
            currentPreferences = currentPreferences with { DownloadSpeedLimitStep = value };
            DownloadSettingsSummary = GetDownloadSettingsSummary(currentPreferences);
        }
        catch (Exception exception)
        {
            DownloadSettingsSummary = $"下载限速设置保存失败：{exception.Message}";
        }
    }

    private static string GetDownloadSettingsSummary(LauncherPreferences preferences) =>
        $"最多 {preferences.DownloadConcurrency} 个总下载连接；速度上限：{LauncherDownloadSettings.GetSpeedLimitDisplayName(preferences.DownloadSpeedLimitStep)}。设置将在下一次安装任务开始时生效。";

    partial void OnSelectedGameWindowModeChanged(MinecraftGameWindowModeOption value) =>
        UsesCustomGameWindowSize = value.Mode == MinecraftGameWindowMode.Custom;

    partial void OnSelectedMemoryAllocationModeChanged(MinecraftMemoryAllocationModeOption value) =>
        UsesCustomMemoryAllocation = value.Mode == MinecraftMemoryAllocationMode.Custom;

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
            customMemory);
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

    private static string GetLaunchOptionsSummary(MinecraftLaunchOptions options)
    {
        var windowDescription = options.WindowMode switch
        {
            MinecraftGameWindowMode.Default => "默认窗口 854 × 480",
            MinecraftGameWindowMode.Fullscreen => "全屏",
            MinecraftGameWindowMode.Custom => $"自定义窗口 {options.WindowWidth} × {options.WindowHeight}",
            _ => "未知窗口模式",
        };
        var jvmDescription = string.IsNullOrWhiteSpace(options.AdditionalJvmArguments) ? "未设置额外 JVM 参数" : "已设置额外 JVM 参数";
        var gameDescription = string.IsNullOrWhiteSpace(options.AdditionalGameArguments) ? "未设置额外游戏参数" : "已设置额外游戏参数";
        var memoryDescription = options.MemoryAllocationMode == MinecraftMemoryAllocationMode.Automatic
            ? "自动内存分配"
            : $"自定义内存 {options.CustomMemoryMiB} MiB";
        return $"{windowDescription}；{memoryDescription}；{jvmDescription}；{gameDescription}。保存后立即用于下一次启动准备。";
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
    private Task InstallGameAsync() => InstallGameCoreAsync(refreshDefaultInstanceCatalog: true);

    private async Task InstallGameCoreAsync(bool refreshDefaultInstanceCatalog)
    {
        if (SelectedInstance is null || !CanInstallGame)
        {
            InstallationSummary = "安装条件尚未满足，未发起下载。";
            return;
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
            await installationService.InstallAsync(SelectedInstance, progress, cancellation.Token);
            InstallationSummary = "安装下载完成。资源映射将在下一次显式启动时准备。";
            if (refreshDefaultInstanceCatalog)
            {
                await RefreshAsync();
            }
            else
            {
                await RefreshSelectedInstanceStateAsync();
            }
        }
        catch (OperationCanceledException)
        {
            InstallationSummary = "安装已取消。";
            CanInstallGame = true;
        }
        catch (Exception exception)
        {
            InstallationSummary = $"安装失败：{exception.Message}";
            CanInstallGame = true;
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
            return;
        }

        try
        {
            var session = await gameLaunchService.LaunchAsync(gameLaunchPreparation);
            GameLaunchSummary = $"已启动游戏进程（PID {session.ProcessId}）。输出将用于后续日志页。";
            GameLogLines.Clear();
            HasGameLogLines = false;
            GameLogSummary = $"正在捕获游戏进程 {session.ProcessId} 的输出；日志仅保留在本次会话内。";
            _ = ObserveGameProcessAsync(session);
        }
        catch (Exception exception)
        {
            GameLaunchSummary = $"启动游戏失败：{exception.Message}";
            CanLaunchGame = false;
        }
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

    private async Task ObserveGameProcessAsync(GameProcessSession session)
    {
        var outputCount = 0;
        await foreach (var output in session.Output.ReadAllAsync())
        {
            outputCount++;
            if (GameLogLines.Count == MaximumGameLogLines)
            {
                GameLogLines.RemoveAt(0);
            }

            GameLogLines.Add(GameLogLine.FromOutput(output));
            HasGameLogLines = true;
        }

        var exitCode = await session.ExitCode;
        GameLaunchSummary = $"游戏进程已退出（代码 {exitCode}，捕获 {outputCount} 行输出）。";
        GameLogSummary = $"游戏进程已退出（代码 {exitCode}）。本次会话保留 {GameLogLines.Count} 行输出。";
        CanLaunchGame = false;
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
}

public sealed record MinecraftGameWindowModeOption(MinecraftGameWindowMode Mode, string DisplayName);

public sealed record MinecraftMemoryAllocationModeOption(MinecraftMemoryAllocationMode Mode, string DisplayName);

public sealed record CommunityResourceSortOption(CommunityResourceSort Sort, string DisplayName);

public sealed record CommunityResourceLoaderOption(CommunityResourceLoader Loader, string DisplayName);

public sealed record CommunityResourceCategoryOption(string? Category, string DisplayName);
