using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PCL.Aurora.Application;

namespace PCL.Aurora.Desktop.ViewModels;

public partial class MainViewModel(
    ISystemDiagnosticsService diagnosticsService,
    IInstanceCatalogService instanceCatalogService) : ViewModelBase
{
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
                : $"发现 {instances.Count} 个本地实例，其中 {instances.Count(instance => instance.Status == PCL.Aurora.Domain.MinecraftInstanceStatus.Valid)} 个可读取版本元数据。";
        }
        catch (Exception exception)
        {
            JavaSummary = $"Java 扫描失败：{exception.Message}";
            InstanceSummary = $"实例扫描失败：{exception.Message}";
        }
    }
}
