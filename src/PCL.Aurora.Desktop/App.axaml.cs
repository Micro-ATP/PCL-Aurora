using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.DependencyInjection;
using PCL.Aurora.Application;
using PCL.Aurora.Desktop.ViewModels;
using PCL.Aurora.Desktop.Views;
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
        services.AddSingleton<IOpenPathService, MacOSOpenPathService>();
        services.AddSingleton<ISystemDiagnosticsService, SystemDiagnosticsService>();
        services.AddSingleton<IInstanceCatalogService, InstanceCatalogService>();
        services.AddSingleton<ILaunchReadinessService, LaunchReadinessService>();
        services.AddTransient<MainViewModel>();
        return services.BuildServiceProvider();
    }
}
