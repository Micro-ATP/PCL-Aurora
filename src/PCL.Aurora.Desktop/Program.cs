using Avalonia;
using System;
using PCL.Aurora.Infrastructure;
using PCL.Aurora.Platform.MacOS;

namespace PCL.Aurora.Desktop;

sealed class Program
{
    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    [STAThread]
    public static void Main(string[] args) => BuildAvaloniaApp()
        .StartWithClassicDesktopLifetime(args);

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
    {
        var builder = AppBuilder.Configure<App>()
            .UsePlatformDetect();

        if (OperatingSystem.IsMacOS() && ShouldDisableHardwareAcceleration())
        {
            builder = builder.With(new AvaloniaNativePlatformOptions
            {
                RenderingMode = [AvaloniaNativeRenderingMode.Software],
            });
        }

#if DEBUG
        builder = builder.WithDeveloperTools();
#endif
        return builder.WithInterFont()
            .LogToTrace();
    }

    private static bool ShouldDisableHardwareAcceleration()
    {
        var store = new JsonLauncherPreferencesStore(new MacOSPlatformPaths());
        return store.LoadAsync().GetAwaiter().GetResult()
            .Preferences.EffectiveMiscSettings.DisableHardwareAcceleration;
    }
}
