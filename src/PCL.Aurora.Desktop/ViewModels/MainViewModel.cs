using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PCL.Aurora.Application;
using PCL.Aurora.Domain;

namespace PCL.Aurora.Desktop.ViewModels;

public partial class MainViewModel(
    ISystemDiagnosticsService diagnosticsService,
    IInstanceCatalogService instanceCatalogService,
    ILaunchReadinessService launchReadinessService,
    IMinecraftVersionPreparationService versionPreparationService,
    IMinecraftLaunchPreparationService launchPreparationService,
    IMinecraftGameLaunchService gameLaunchService) : ViewModelBase
{
    private MinecraftAccount? selectedAccount;
    private MinecraftInstance? selectedInstance;
    private JavaInstallation? selectedJava;
    private MinecraftGameLaunchPreparation? gameLaunchPreparation;

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
    private string instanceSummary = "正在扫描本地实例…";

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
    private bool canLaunchGame;

    [ObservableProperty]
    private string offlinePlayerName = string.Empty;

    [ObservableProperty]
    private string accountSummary = "未选择账户。可创建只在本次会话使用的离线账户。";

    [ObservableProperty]
    private string launchPreflightSummary = "正在检查启动条件…";

    [RelayCommand]
    private async Task RefreshAsync()
    {
        try
        {
            var diagnosticsTask = diagnosticsService.GetAsync();
            var instancesTask = instanceCatalogService.GetAllAsync();
            await Task.WhenAll(diagnosticsTask, instancesTask);

            var diagnostics = await diagnosticsTask;
            var instances = await instancesTask;
            selectedInstance = instances.FirstOrDefault(instance => instance.Status == MinecraftInstanceStatus.Valid);
            selectedJava = diagnostics.JavaInstallations.FirstOrDefault(java => java.IsCompatible);
            OperatingSystem = $"{diagnostics.Platform.OperatingSystem} ({diagnostics.Platform.Version})";
            Architecture = diagnostics.Platform.Architecture.ToString();
            Runtime = diagnostics.Platform.RuntimeVersion;
            ApplicationDataDirectory = diagnostics.Paths.ApplicationDataDirectory;
            CacheDirectory = diagnostics.Paths.CacheDirectory;
            JavaSummary = diagnostics.JavaInstallations.Count == 0
                ? "未发现可用 Java。"
                : $"发现 {diagnostics.JavaInstallations.Count} 个 Java，其中 {diagnostics.JavaInstallations.Count(java => java.IsCompatible)} 个与当前架构兼容。";
            InstanceSummary = instances.Count == 0
                ? "未在 macOS 默认 Minecraft 目录中发现实例。"
                : $"发现 {instances.Count} 个本地实例，其中 {instances.Count(instance => instance.Status == MinecraftInstanceStatus.Valid)} 个可读取版本元数据。";
            await RefreshVersionPreparationAsync();
            await RefreshLaunchArgumentPreparationAsync();
            UpdateLaunchPreflight();
            await RefreshGameLaunchPreparationAsync();
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
        }
    }

    private async Task RefreshVersionPreparationAsync()
    {
        if (selectedInstance is null)
        {
            VersionMetadataSummary = "未选择可读取版本元数据的本地实例。";
            DownloadPreparationSummary = "需先发现有效本地实例；不会创建目录或下载文件。";
            return;
        }

        var preparation = await versionPreparationService.PrepareAsync(selectedInstance);
        if (!preparation.Inspection.IsSuccess || preparation.Inspection.EffectiveMetadata is null)
        {
            VersionMetadataSummary = string.Join(Environment.NewLine, preparation.Inspection.Errors);
            DownloadPreparationSummary = "版本元数据无效，未生成下载计划。";
            return;
        }

        var metadata = preparation.Inspection.EffectiveMetadata;
        VersionMetadataSummary = $"{metadata.Id} · {metadata.Type ?? "未知类型"} · 继承链：{string.Join(" → ", preparation.Inspection.InheritanceChain.Select(item => item.Id))}";
        DownloadPreparationSummary = preparation.DownloadPlan.IsReady
            ? $"已生成 {preparation.DownloadPlan.Artifacts.Count} 个下载计划项；下载器尚未迁移，未写入任何文件。"
            : string.Join(Environment.NewLine, preparation.DownloadPlan.BlockingReasons);
    }

    private async Task RefreshLaunchArgumentPreparationAsync()
    {
        if (selectedInstance is null)
        {
            LaunchArgumentSummary = "未选择可读取版本元数据的本地实例。";
            ClasspathSummary = "未选择可读取版本元数据的本地实例。";
            return;
        }

        var preparation = await launchPreparationService.PrepareAsync(selectedInstance, selectedAccount);
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
        gameLaunchPreparation = await gameLaunchService.PrepareAsync(selectedInstance, selectedAccount, selectedJava);
        CanLaunchGame = gameLaunchPreparation.CanLaunch;
        GameLaunchSummary = gameLaunchPreparation.CanLaunch
            ? "启动条件和进程请求均已准备。点击“启动游戏”后将先安全准备 native 库，再启动 Java 进程。"
            : string.Join(Environment.NewLine, gameLaunchPreparation.BlockingReasons);
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
        AccountSummary = $"本次会话使用离线账户：{account.DisplayName}。未写入密码或令牌。";
        UpdateLaunchPreflight();
        await RefreshLaunchArgumentPreparationAsync();
        await RefreshGameLaunchPreparationAsync();
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
            _ = ObserveGameProcessAsync(session);
        }
        catch (Exception exception)
        {
            GameLaunchSummary = $"启动游戏失败：{exception.Message}";
            CanLaunchGame = false;
        }
    }

    private async Task ObserveGameProcessAsync(GameProcessSession session)
    {
        var outputCount = 0;
        await foreach (var _ in session.Output.ReadAllAsync())
        {
            outputCount++;
        }

        var exitCode = await session.ExitCode;
        GameLaunchSummary = $"游戏进程已退出（代码 {exitCode}，捕获 {outputCount} 行输出）。";
        CanLaunchGame = false;
    }

    private void UpdateLaunchPreflight()
    {
        var readiness = launchReadinessService.Evaluate(selectedInstance, selectedAccount, selectedJava);
        LaunchPreflightSummary = readiness.CanLaunch
            ? "启动前检查已通过。进程启动条件将继续检查类路径与版本参数。"
            : string.Join(Environment.NewLine, readiness.BlockingReasons);
    }
}
