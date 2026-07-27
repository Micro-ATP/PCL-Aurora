using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using PCL.Aurora.Domain;

namespace PCL.Aurora.Desktop.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        Opened += async (_, _) =>
        {
            if (DataContext is ViewModels.MainViewModel viewModel)
            {
                await viewModel.InitializeAsync();
            }
        };
    }

    private void MainNavigationClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string value } || !int.TryParse(value, out var page))
        {
            return;
        }

        SelectMainPage(page);
    }

    private void OpenDownloadPageClick(object? sender, RoutedEventArgs e)
    {
        SelectMainPage(1);
    }

    private void TitleBarPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.Source is not Button && e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            BeginMoveDrag(e);
        }
    }

    private void MinimizeWindowClick(object? sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    private void CloseWindowClick(object? sender, RoutedEventArgs e)
    {
        Close();
    }

    private void SelectMainPage(int page)
    {
        LaunchPage.IsVisible = page == 0;
        DownloadPage.IsVisible = page == 1;
        SettingsPage.IsVisible = page == 2;
        MorePage.IsVisible = page == 3;

        var navigation = new[] { LaunchNavigation, DownloadNavigation, SettingsNavigation, MoreNavigation };
        for (var index = 0; index < navigation.Length; index++)
        {
            navigation[index].Classes.Set("selected", index == page);
        }
    }

    private void MoreNavigationClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string section })
        {
            return;
        }

        var showLogs = section == "logs";
        var showAbout = section == "about";
        MoreDirectorySection.IsVisible = !showLogs && !showAbout;
        MoreLogSection.IsVisible = showLogs;
        MoreAboutSection.IsVisible = showAbout;
        MoreDirectoryNavigation.Classes.Set("selected", !showLogs && !showAbout);
        MoreLogsNavigation.Classes.Set("selected", showLogs);
        MoreAboutNavigation.Classes.Set("selected", showAbout);
    }

    private void DownloadNavigationClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string section })
        {
            return;
        }

        var showOptiFine = section == "optifine";
        var showLoader = section is "loader" or "optifine";
        DownloadCommunityCard.IsVisible = false;
        DownloadGameCard.IsVisible = !showLoader;
        DownloadLoaderCard.IsVisible = showLoader;
        DownloadPageTitle.Text = showOptiFine ? "OptiFine" : showLoader ? "Forge / NeoForge / Fabric / OptiFine" : "原版游戏";
        DownloadPageDescription.Text = showOptiFine
            ? "选择与当前实例兼容的 OptiFine 版本。1.14+ 使用安装器，旧版创建受控继承版本。"
            : showLoader
                ? "选择当前实例兼容的加载器版本后，执行已验证的安装流程。"
            : "选择官方 Minecraft 版本，再创建本地实例并执行已验证的安装流程。";
        if (DataContext is ViewModels.MainViewModel viewModel)
        {
            viewModel.SetLoaderKindFilter(showOptiFine ? MinecraftLoaderKind.OptiFine : null);
        }

        DownloadGameNavigation.Classes.Set("selected", !showLoader);
        DownloadOptiFineNavigation.Classes.Set("selected", showOptiFine);
        DownloadLoaderNavigation.Classes.Set("selected", showLoader && !showOptiFine);
    }

    private void VersionFilterClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string filter } || DataContext is not ViewModels.MainViewModel viewModel)
        {
            return;
        }

        viewModel.VersionSearchText = string.Empty;
        viewModel.IncludeReleaseVersions = filter == "release";
        viewModel.IncludeSnapshotVersions = filter == "snapshot";
        viewModel.IncludeLegacyVersions = filter is "legacy" or "april-fools";
        ReleaseVersionGroup.Classes.Set("selected", filter == "release");
        SnapshotVersionGroup.Classes.Set("selected", filter == "snapshot");
        LegacyVersionGroup.Classes.Set("selected", filter == "legacy");
        AprilFoolsVersionGroup.Classes.Set("selected", filter == "april-fools");
    }

    private void SettingsNavigationClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string section })
        {
            return;
        }

        var showInterface = section == "interface";
        SettingsLaunchSection.IsVisible = !showInterface;
        SettingsInterfaceSection.IsVisible = showInterface;
        SettingsLaunchNavigation.Classes.Set("selected", !showInterface);
        SettingsInterfaceNavigation.Classes.Set("selected", showInterface);
    }
}
