using Avalonia.Controls;
using Avalonia.Interactivity;

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

    private void SelectMainPage(int page)
    {
        MainTabs.SelectedIndex = page;

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

        var showLoader = section == "loader";
        DownloadCommunityCard.IsVisible = false;
        DownloadGameCard.IsVisible = !showLoader;
        DownloadLoaderCard.IsVisible = showLoader;
        DownloadPageTitle.Text = showLoader ? "Forge / NeoForge / Fabric / OptiFine" : "原版游戏";
        DownloadPageDescription.Text = showLoader
            ? "选择当前实例兼容的加载器版本后，执行已验证的安装流程。OptiFine 当前支持 1.14+ 安装器路径。"
            : "选择官方 Minecraft 版本，再创建本地实例并执行已验证的安装流程。";
        DownloadGameNavigation.Classes.Set("selected", !showLoader);
        DownloadLoaderNavigation.Classes.Set("selected", showLoader);
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
