using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PCL.Aurora.Application;

namespace PCL.Aurora.Desktop.ViewModels;

public partial class MainViewModel(ISystemDiagnosticsService diagnosticsService) : ViewModelBase
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

    [RelayCommand]
    private async Task RefreshAsync()
    {
        try
        {
            var diagnostics = await diagnosticsService.GetAsync();
            OperatingSystem = $"{diagnostics.Platform.OperatingSystem} ({diagnostics.Platform.Version})";
            Architecture = diagnostics.Platform.Architecture.ToString();
            Runtime = diagnostics.Platform.RuntimeVersion;
            ApplicationDataDirectory = diagnostics.Paths.ApplicationDataDirectory;
            CacheDirectory = diagnostics.Paths.CacheDirectory;
            JavaSummary = diagnostics.JavaInstallations.Count == 0
                ? "未发现可用 Java。"
                : $"发现 {diagnostics.JavaInstallations.Count} 个 Java，其中 {diagnostics.JavaInstallations.Count(java => java.IsCompatible)} 个与当前架构兼容。";
        }
        catch (Exception exception)
        {
            JavaSummary = $"Java 扫描失败：{exception.Message}";
        }
    }
}
