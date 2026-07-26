using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using PCL.Aurora.Application;
using PCL.Aurora.Desktop.Services;
using PCL.Aurora.Domain;

namespace PCL.Aurora.Desktop.ViewModels;

public partial class MainViewModel(
    ISystemDiagnosticsService diagnosticsService,
    IInstanceCatalogService instanceCatalogService,
    ILaunchReadinessService launchReadinessService,
    IMinecraftVersionPreparationService versionPreparationService,
    IMinecraftLaunchPreparationService launchPreparationService,
    IMinecraftInstanceInstallationService installationService,
    IMinecraftVersionCatalogService versionCatalogService,
    IMinecraftLoaderCatalogService loaderCatalogService,
    IMinecraftOfficialLoaderCatalogService officialLoaderCatalogService,
    IMinecraftLoaderInstallerService loaderInstallerService,
    IMinecraftVersionProvisioningService versionProvisioningService,
    IMinecraftDirectoryService minecraftDirectoryService,
    IMinecraftGameLaunchService gameLaunchService,
    ILauncherPreferencesService preferencesService,
    IThemeService themeService) : ViewModelBase
{
    private const int MaximumGameLogLines = 500;

    private readonly List<MinecraftVersionCatalogEntry> allCatalogVersions = [];
    private MinecraftAccount? selectedAccount;
    private MinecraftLoaderCatalog? loaderCatalog;
    private MinecraftGameLaunchPreparation? gameLaunchPreparation;
    private LauncherPreferences currentPreferences = LauncherPreferences.Default;
    private bool isRefreshing;
    private bool isLoadingPreferences;

    public ObservableCollection<MinecraftVersionCatalogEntry> AvailableVersions { get; } = [];

    public ObservableCollection<MinecraftLoaderCatalogEntry> AvailableLoaders { get; } = [];

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
    private string gameLogSummary = "尚未启动游戏，本次会话没有可查看的进程输出。";

    [ObservableProperty]
    private bool hasGameLogLines;

    [ObservableProperty]
    private bool canLaunchGame;

    [ObservableProperty]
    private bool canInstallGame;

    [ObservableProperty]
    private string installationSummary = "选择本地实例后可查看安装计划；不会自动下载。";

    [ObservableProperty]
    private MinecraftVersionCatalogEntry? selectedCatalogVersion;

    [ObservableProperty]
    private string versionSearchText = string.Empty;

    [ObservableProperty]
    private bool includeReleaseVersions = true;

    [ObservableProperty]
    private bool includeSnapshotVersions = true;

    [ObservableProperty]
    private bool includeLegacyVersions;

    [ObservableProperty]
    private bool canProvisionSelectedVersion;

    [ObservableProperty]
    private string versionCatalogSummary = "点击“刷新官方版本”后加载可选版本；不会自动访问网络。";

    [ObservableProperty]
    private string loaderCatalogPath = string.Empty;

    [ObservableProperty]
    private string loaderCatalogSummary = "可导入用户指定的本地加载器目录 JSON；不会自动访问网络。";

    [ObservableProperty]
    private string loaderSelectionSummary = "请先导入目录并选择一个 Minecraft 版本；不会下载或执行安装器。";

    [ObservableProperty]
    private bool hasAvailableLoaders;

    [ObservableProperty]
    private MinecraftLoaderCatalogEntry? selectedLoader;

    [ObservableProperty]
    private bool canInstallSelectedLoader;

    [ObservableProperty]
    private string offlinePlayerName = string.Empty;

    [ObservableProperty]
    private string accountSummary = "未选择账户。可创建只在本次会话使用的离线账户。";

    [ObservableProperty]
    private string accountLicenseGuidance = "选择账户后会显示正版购买与上游赞助提示。";

    [ObservableProperty]
    private bool requiresAccountGuidance;

    [ObservableProperty]
    private bool hasAcknowledgedAccountGuidance;

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
            RestoreOfflineAccount(result.Preferences.OfflinePlayerName);
        }
        catch (Exception exception)
        {
            ThemeSummary = $"无法读取本地主题偏好：{exception.Message}；当前跟随系统主题。";
            DownloadSettingsSummary = "无法读取本地下载设置；已使用安全默认值。";
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
            VersionMetadataSummary = string.Join(Environment.NewLine, preparation.Inspection.Errors);
            DownloadPreparationSummary = "版本元数据无效，未生成下载计划。";
            InstallationSummary = "版本元数据无效，无法开始下载。";
            CanInstallGame = false;
            return;
        }

        var metadata = preparation.Inspection.EffectiveMetadata;
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

        var preparation = await launchPreparationService.PrepareAsync(SelectedInstance, selectedAccount);
        ClasspathSummary = preparation.ClasspathInspection.IsReady
            ? $"已发现 {preparation.ClasspathInspection.Entries.Count} 个本地类路径条目。"
            : string.Join(
                Environment.NewLine,
                preparation.ClasspathInspection.BlockingReasons
                    .Concat(preparation.ClasspathInspection.MissingFiles.Select(file => $"缺少文件：{file}")));
        LaunchArgumentSummary = preparation.ArgumentPreparation.IsReady
            ? $"已准备 {preparation.ArgumentPreparation.Arguments!.JvmArguments.Count} 个 JVM 参数与 {preparation.ArgumentPreparation.Arguments.GameArguments.Count} 个游戏参数；等待进程启动条件检查。"
            : string.Join(Environment.NewLine, preparation.ArgumentPreparation.BlockingReasons);
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
        try
        {
            VersionCatalogSummary = "正在获取官方版本清单…";
            var result = await versionCatalogService.FetchAsync();
            if (result.Catalog is null)
            {
                allCatalogVersions.Clear();
                AvailableVersions.Clear();
                SelectedCatalogVersion = null;
                VersionCatalogSummary = string.Join(Environment.NewLine, result.Errors);
                return;
            }

            allCatalogVersions.Clear();
            allCatalogVersions.AddRange(result.Catalog.Versions);
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

    partial void OnSelectedCatalogVersionChanged(MinecraftVersionCatalogEntry? value)
    {
        CanProvisionSelectedVersion = value is not null;
        RefreshLoaderEntries();
    }

    partial void OnVersionSearchTextChanged(string value) => ApplyVersionFilters();

    partial void OnIncludeReleaseVersionsChanged(bool value) => ApplyVersionFilters();

    partial void OnIncludeSnapshotVersionsChanged(bool value) => ApplyVersionFilters();

    partial void OnIncludeLegacyVersionsChanged(bool value) => ApplyVersionFilters();

    private void ApplyVersionFilters(string? preferredVersionId = null)
    {
        if (allCatalogVersions.Count == 0)
        {
            return;
        }

        var selectedId = SelectedCatalogVersion?.Id ?? preferredVersionId;
        var filtered = MinecraftVersionCatalogFilter.Filter(
            allCatalogVersions,
            VersionSearchText,
            IncludeReleaseVersions,
            IncludeSnapshotVersions,
            IncludeLegacyVersions);

        AvailableVersions.Clear();
        foreach (var version in filtered)
        {
            AvailableVersions.Add(version);
        }

        SelectedCatalogVersion = AvailableVersions.FirstOrDefault(version => version.Id == selectedId)
            ?? AvailableVersions.FirstOrDefault();
        VersionCatalogSummary = AvailableVersions.Count == 0
            ? "没有符合当前筛选条件的官方版本；不会创建实例。"
            : $"已加载 {allCatalogVersions.Count} 个官方版本；当前筛选显示 {AvailableVersions.Count} 个。";
    }

    partial void OnSelectedInstanceChanged(MinecraftInstance? value)
    {
        RefreshLoaderEntries();
        CanInstallSelectedLoader = CanInstallLoaderForSelectedInstance(SelectedLoader);
        if (!isRefreshing)
        {
            _ = RefreshSelectedInstanceStateAsync();
            _ = SaveSelectedInstancePreferenceAsync(value?.Name);
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
        var minecraftVersion = SelectedCatalogVersion?.Id ?? GetMinecraftVersionForLoaders(SelectedInstance);
        if (string.IsNullOrWhiteSpace(minecraftVersion))
        {
            LoaderCatalogSummary = "请先选择下载页中的官方版本，或选择一个本地实例；未访问网络。";
            return;
        }

        try
        {
            LoaderCatalogSummary = $"正在读取 Minecraft {minecraftVersion} 的官方加载器目录…";
            var result = await officialLoaderCatalogService.FetchAsync(minecraftVersion);
            if (!result.IsSuccess || result.Catalog is null)
            {
                loaderCatalog = null;
                AvailableLoaders.Clear();
                HasAvailableLoaders = false;
                SelectedLoader = null;
                LoaderCatalogSummary = string.Join(Environment.NewLine, result.Errors);
                LoaderSelectionSummary = "官方目录未通过检查；未选择加载器，也不会发起安装。";
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
            LoaderSelectionSummary = "请先导入本地加载器目录；不会自动访问网络。";
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

        foreach (var loader in MinecraftLoaderCatalogFilter.ForMinecraftVersion(loaderCatalog, minecraftVersion))
        {
            AvailableLoaders.Add(loader);
        }

        HasAvailableLoaders = AvailableLoaders.Count > 0;
        SelectedLoader = AvailableLoaders.FirstOrDefault(loader =>
                string.Equals($"{loader.Kind}:{loader.MinecraftVersion}:{loader.Version}", selectedKey, StringComparison.OrdinalIgnoreCase))
            ?? AvailableLoaders.FirstOrDefault();
        CanInstallSelectedLoader = CanInstallLoaderForSelectedInstance(SelectedLoader);
        LoaderSelectionSummary = HasAvailableLoaders
            ? $"Minecraft {minecraftVersion} 可选 {AvailableLoaders.Count} 个加载器版本。一次只能选择一个主模组加载器。"
            : $"本地目录中没有兼容 Minecraft {minecraftVersion} 的 Forge、NeoForge 或 Fabric 版本。";
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
            ? $"已选择 {value.Kind} {value.Version}（Minecraft {value.MinecraftVersion}，{value.Channel}）。选择本地实例与 Java 后，可由“下载并安装”明确触发。"
            : string.Join(Environment.NewLine, compatibility.Reasons);
    }

    partial void OnSelectedJavaChanged(JavaInstallation? value)
    {
        CanInstallSelectedLoader = CanInstallLoaderForSelectedInstance(SelectedLoader);
        if (!isRefreshing)
        {
            _ = RefreshSelectedInstanceStateAsync();
        }
    }

    [RelayCommand]
    private async Task InstallSelectedLoaderAsync()
    {
        if (SelectedLoader is not { } loader ||
            SelectedInstance is not { } instance ||
            SelectedJava is not { } java ||
            !string.Equals(loader.MinecraftVersion, GetMinecraftVersionForLoaders(instance), StringComparison.OrdinalIgnoreCase))
        {
            LoaderSelectionSummary = "请先选择兼容的加载器、本地实例与 Java；未下载或执行安装器。";
            return;
        }

        try
        {
            CanInstallSelectedLoader = false;
            LoaderSelectionSummary = $"正在准备 {loader.Kind} {loader.Version} 官方安装器…";
            var plan = await loaderInstallerService.PrepareAsync(loader, MinecraftRootDirectory, java);
            if (!plan.CanInstall)
            {
                LoaderSelectionSummary = string.Join(Environment.NewLine, plan.BlockingReasons);
                return;
            }

            LoaderSelectionSummary = $"正在下载并执行 {loader.Kind} {loader.Version} 安装器…";
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
        SelectedJava is not null &&
        string.Equals(loader.MinecraftVersion, GetMinecraftVersionForLoaders(SelectedInstance), StringComparison.OrdinalIgnoreCase);

    private static string? GetMinecraftVersionForLoaders(MinecraftInstance? instance) =>
        instance?.BaseVersionId ?? instance?.VersionId;

    partial void OnHasAcknowledgedAccountGuidanceChanged(bool value)
    {
        if (!isRefreshing)
        {
            _ = RefreshGameLaunchPreparationAsync();
        }
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

    [RelayCommand]
    private async Task InstallGameAsync()
    {
        if (SelectedInstance is null || !CanInstallGame)
        {
            InstallationSummary = "安装条件尚未满足，未发起下载。";
            return;
        }

        try
        {
            CanInstallGame = false;
            var progress = new Progress<MinecraftInstallationProgress>(update =>
                InstallationSummary = $"[{update.CompletedStages}/{update.TotalStages}] {update.Description}");
            await installationService.InstallAsync(SelectedInstance, progress);
            InstallationSummary = "安装下载完成。资源映射将在下一次显式启动时准备。";
            await RefreshAsync();
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
    }

    [RelayCommand]
    private async Task UseOfflineAccount()
    {
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
