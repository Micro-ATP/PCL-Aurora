using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.DependencyInjection;
using PCL.Aurora.Application;
using PCL.Aurora.Desktop.ViewModels;
using PCL.Aurora.Desktop.Views;
using PCL.Aurora.Desktop.Services;
using PCL.Aurora.Infrastructure;
using PCL.Aurora.Platform.Abstractions;
using PCL.Aurora.Platform.MacOS;

namespace PCL.Aurora.Desktop;

public partial class App : Avalonia.Application
{
    private ServiceProvider? services;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            services = ConfigureServices();
            desktop.MainWindow = new MainWindow
            {
                DataContext = services.GetRequiredService<MainViewModel>(),
            };
        }

        base.OnFrameworkInitializationCompleted();
    }

    private static ServiceProvider ConfigureServices()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IPlatformInfo, MacOSPlatformInfo>();
        services.AddSingleton<IPlatformPaths, MacOSPlatformPaths>();
        services.AddSingleton<IJavaLocator, MacOSJavaLocator>();
        services.AddSingleton<IMinecraftInstanceLocator, MacOSMinecraftInstanceLocator>();
        services.AddSingleton<IMinecraftRootDirectoryProvider, MacOSMinecraftRootDirectoryProvider>();
        services.AddSingleton<IMinecraftVersionMetadataReader, MacOSMinecraftVersionMetadataReader>();
        services.AddSingleton<IOpenPathService, MacOSOpenPathService>();
        services.AddSingleton<ISystemDiagnosticsService, SystemDiagnosticsService>();
        services.AddSingleton<IInstanceCatalogService, InstanceCatalogService>();
        services.AddSingleton<ILaunchReadinessService, LaunchReadinessService>();
        services.AddSingleton<IMinecraftVersionPreparationService, MinecraftVersionPreparationService>();
        services.AddSingleton<IMinecraftAssetPreparationService, MinecraftAssetPreparationService>();
        services.AddSingleton<IMinecraftLaunchPreparationService, MinecraftLaunchPreparationService>();
        services.AddSingleton<IMinecraftAssetIndexReader, MacOSMinecraftAssetIndexReader>();
        services.AddSingleton<IAssetMapper, MinecraftAssetMapper>();
        services.AddSingleton<HttpClient>();
        services.AddSingleton<IMinecraftDownloadExecutor, MinecraftDownloadExecutor>();
        services.AddSingleton<IMinecraftInstanceInstallationService, MinecraftInstanceInstallationService>();
        services.AddSingleton<IMinecraftVersionCatalogService, MinecraftVersionCatalogService>();
        services.AddSingleton<IMinecraftLoaderCatalogService, MinecraftLoaderCatalogService>();
        services.AddSingleton<IMinecraftOfficialLoaderCatalogService, MinecraftOfficialLoaderCatalogService>();
        services.AddSingleton<IMinecraftVersionProvisioningService, MinecraftVersionProvisioningService>();
        services.AddSingleton<IMinecraftDirectoryService, MinecraftDirectoryService>();
        services.AddSingleton<ILauncherPreferencesStore, JsonLauncherPreferencesStore>();
        services.AddSingleton<ILauncherPreferencesService, LauncherPreferencesService>();
        services.AddSingleton<INativeLibraryPreparer, MinecraftNativeLibraryPreparer>();
        services.AddSingleton<IGameProcessRunner, MinecraftGameProcessRunner>();
        services.AddSingleton<IMinecraftGameLaunchService, MinecraftGameLaunchService>();
        services.AddSingleton<IThemeService, AvaloniaThemeService>();
        services.AddTransient<MainViewModel>();
        return services.BuildServiceProvider();
    }
}
