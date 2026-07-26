using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PCL.Aurora.Application;
using PCL.Aurora.Domain;

namespace PCL.Aurora.Desktop.ViewModels;

public partial class MainViewModel(
    ISystemDiagnosticsService diagnosticsService,
    IInstanceCatalogService instanceCatalogService,
    ILaunchReadinessService launchReadinessService) : ViewModelBase
{
    private MinecraftAccount? selectedAccount;
    private MinecraftInstance? selectedInstance;
    private JavaInstallation? selectedJava;

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
            UpdateLaunchPreflight();
        }
        catch (Exception exception)
        {
            JavaSummary = $"Java 扫描失败：{exception.Message}";
            InstanceSummary = $"实例扫描失败：{exception.Message}";
        }
    }

    [RelayCommand]
    private void UseOfflineAccount()
    {
        if (!OfflineAccount.TryCreate(OfflinePlayerName, out var account) || account is null)
        {
            AccountSummary = "离线用户名需为 3–16 位英文字母、数字或下划线。";
            UpdateLaunchPreflight();
            return;
        }

        selectedAccount = account;
        AccountSummary = $"本次会话使用离线账户：{account.DisplayName}。未写入密码或令牌。";
        UpdateLaunchPreflight();
    }

    private void UpdateLaunchPreflight()
    {
        var readiness = launchReadinessService.Evaluate(selectedInstance, selectedAccount, selectedJava);
        LaunchPreflightSummary = readiness.CanLaunch
            ? "启动前检查已通过。游戏进程启动器尚未迁移，因此启动按钮仍不可用。"
            : string.Join(Environment.NewLine, readiness.BlockingReasons);
    }
}
