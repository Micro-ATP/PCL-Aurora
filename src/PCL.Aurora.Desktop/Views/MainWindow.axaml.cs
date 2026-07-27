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
        MoreDirectorySection.IsVisible = !showLogs;
        MoreLogSection.IsVisible = showLogs;
        MoreDirectoryNavigation.Classes.Set("selected", !showLogs);
        MoreLogsNavigation.Classes.Set("selected", showLogs);
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
