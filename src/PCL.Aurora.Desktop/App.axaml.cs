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
        services.AddSingleton<ISystemMemoryInfo, MacOSSystemMemoryInfo>();
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
        services.AddSingleton(MicrosoftAuthenticationConfiguration.FromEnvironmentOrAssembly(typeof(App).Assembly));
        services.AddSingleton<IMicrosoftAccountAuthenticationService, MicrosoftAccountAuthenticationService>();
        services.AddSingleton<ISecureSecretStore, MacOSKeychainSecretStore>();
        services.AddSingleton<IMicrosoftAccountSessionService, MicrosoftAccountSessionService>();
        services.AddSingleton<ILauncherPreferencesStore, JsonLauncherPreferencesStore>();
        services.AddSingleton<ILauncherPreferencesService, LauncherPreferencesService>();
        services.AddSingleton<IMinecraftDownloadExecutor, MinecraftDownloadExecutor>();
        services.AddSingleton<IMinecraftInstanceInstallationService, MinecraftInstanceInstallationService>();
        services.AddSingleton<IMinecraftVersionCatalogService, MinecraftVersionCatalogService>();
        services.AddSingleton<IMinecraftVersionArchiveService, MinecraftVersionArchiveService>();
        services.AddSingleton<ICommunityResourceLocalizationService, PclCeCommunityResourceLocalizationService>();
        services.AddSingleton<ModrinthCommunityResourceSearchService>();
        services.AddSingleton<CurseForgeCommunityResourceSearchService>();
        services.AddSingleton<ICommunityResourceSearchService, CommunityResourceSearchService>();
        services.AddSingleton<ICommunityResourceIconService, ModrinthCommunityResourceIconService>();
        services.AddSingleton<ModrinthCommunityResourceVersionService>();
        services.AddSingleton<CurseForgeCommunityResourceVersionService>();
        services.AddSingleton<ICommunityResourceVersionService, CommunityResourceVersionService>();
        services.AddSingleton<ICommunityResourceDependencyResolver, CommunityResourceDependencyResolver>();
        services.AddSingleton<ICommunityResourceInstallationService, CommunityResourceInstallationService>();
        services.AddSingleton<ICommunityResourceDownloadService, CommunityResourceDownloadService>();
        services.AddSingleton<IModrinthModpackImportService, ModrinthModpackImportService>();
        services.AddSingleton<ICommunityWorldImportService, CommunityWorldImportService>();
        services.AddSingleton<ICommunityFavoritesStore, JsonCommunityFavoritesStore>();
        services.AddSingleton<ICommunityResourceDescriptionTranslationService, PclCeCommunityResourceDescriptionTranslationService>();
        services.AddSingleton<IGitHubContributorService, GitHubContributorService>();
        services.AddSingleton<IMinecraftLoaderCatalogService, MinecraftLoaderCatalogService>();
        services.AddSingleton<IMinecraftOfficialLoaderCatalogService, MinecraftOfficialLoaderCatalogService>();
        services.AddSingleton<IMinecraftLoaderPackageDownloadService, MinecraftLoaderPackageDownloadService>();
        services.AddSingleton<IMinecraftLoaderInstallerProcessRunner, MinecraftLoaderInstallerProcessRunner>();
        services.AddSingleton<IMinecraftLoaderInstallerService, MinecraftLoaderInstallerService>();
        services.AddSingleton<IMinecraftVersionProvisioningService, MinecraftVersionProvisioningService>();
        services.AddSingleton<IMinecraftDirectoryService, MinecraftDirectoryService>();
        services.AddSingleton<INativeLibraryPreparer, MinecraftNativeLibraryPreparer>();
        services.AddSingleton<IGameProcessRunner, MinecraftGameProcessRunner>();
        services.AddSingleton<IMinecraftGameLaunchService, MinecraftGameLaunchService>();
        services.AddSingleton<IThemeService, AvaloniaThemeService>();
        services.AddTransient<MainViewModel>();
        return services.BuildServiceProvider();
    }
}
