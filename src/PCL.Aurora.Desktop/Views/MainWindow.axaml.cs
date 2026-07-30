using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Avalonia.Media.Imaging;
using PCL.Aurora.Application;
using PCL.Aurora.Desktop.Controls;
using PCL.Aurora.Desktop.Services;
using PCL.Aurora.Domain;

namespace PCL.Aurora.Desktop.Views;

public partial class MainWindow : Window
{
    private string currentDownloadSection = "game";
    private string currentMoreSection = "help";
    private ViewModels.MainViewModel? subscribedViewModel;
    private Bitmap? launcherBackgroundBitmap;
    private Bitmap? launcherTitleBitmap;
    private bool isFeatureHidingSuspended;
    private bool isRevertingAnnouncementSelection;

    private static readonly string[] HelpTopics =
    {
        "启动前检查",
        "正版与离线账户",
        "下载与安装",
        "实例与版本选择",
        "Java 与内存",
        "日志与故障排查",
    };

    public MainWindow()
    {
        InitializeComponent();
        PclMotionService.Attach(this);
        PopulateMorePlaceholder("help");
        DataContextChanged += (_, _) => SubscribeToViewModel();
        KeyDown += MainWindowKeyDown;
        Opened += async (_, _) =>
        {
            SubscribeToViewModel();
            if (DataContext is ViewModels.MainViewModel viewModel)
            {
                await viewModel.InitializeAsync();
            }
        };
        Closed += (_, _) =>
        {
            SubscribeToViewModel(null);
            launcherBackgroundBitmap?.Dispose();
            launcherTitleBitmap?.Dispose();
        };
    }

    private void SubscribeToViewModel() => SubscribeToViewModel(DataContext as ViewModels.MainViewModel);

    private void SubscribeToViewModel(ViewModels.MainViewModel? viewModel)
    {
        if (ReferenceEquals(subscribedViewModel, viewModel))
        {
            return;
        }

        if (subscribedViewModel is not null)
        {
            subscribedViewModel.MicrosoftDeviceCodeAvailable -= MicrosoftDeviceCodeAvailable;
            subscribedViewModel.GameProcessStarted -= GameProcessStarted;
            subscribedViewModel.GameProcessExited -= GameProcessExited;
            subscribedViewModel.PropertyChanged -= ViewModelPropertyChanged;
        }

        subscribedViewModel = viewModel;
        if (subscribedViewModel is not null)
        {
            subscribedViewModel.MicrosoftDeviceCodeAvailable += MicrosoftDeviceCodeAvailable;
            subscribedViewModel.GameProcessStarted += GameProcessStarted;
            subscribedViewModel.GameProcessExited += GameProcessExited;
            subscribedViewModel.PropertyChanged += ViewModelPropertyChanged;
            ApplyInterfacePreferences(subscribedViewModel);
        }
    }

    private void ViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (sender is not ViewModels.MainViewModel viewModel)
        {
            return;
        }

        if (e.PropertyName is nameof(ViewModels.MainViewModel.InterfaceWindowOpacityFraction)
            or nameof(ViewModels.MainViewModel.LockWindowSize)
            or nameof(ViewModels.MainViewModel.GlobalInterfaceFont)
            or nameof(ViewModels.MainViewModel.TitleContentTypeIndex)
            or nameof(ViewModels.MainViewModel.CustomTitleText)
            or nameof(ViewModels.MainViewModel.InterfaceBackgroundOpacity)
            or nameof(ViewModels.MainViewModel.InterfaceBackgroundBlurRadius)
            or nameof(ViewModels.MainViewModel.BackgroundSuitIndex)
            or nameof(ViewModels.MainViewModel.LightThemeColorIndex)
            or nameof(ViewModels.MainViewModel.DarkThemeColorIndex)
            or nameof(ViewModels.MainViewModel.SelectedThemeMode))
        {
            Dispatcher.UIThread.Post(() => ApplyInterfacePreferences(viewModel));
        }

        else if (e.PropertyName?.StartsWith("Hide", StringComparison.Ordinal) == true)
        {
            Dispatcher.UIThread.Post(() => ApplyFeatureVisibility(viewModel));
        }
    }

    private void ApplyInterfacePreferences(ViewModels.MainViewModel viewModel)
    {
        Opacity = viewModel.InterfaceWindowOpacityFraction;
        CanResize = !viewModel.LockWindowSize;
        FontFamily = string.IsNullOrWhiteSpace(viewModel.GlobalInterfaceFont)
            ? Avalonia.Media.FontFamily.Parse("avares://PCL.Aurora.Desktop/Fonts/HarmonyOS_Sans_SC#HarmonyOS Sans SC")
            : new Avalonia.Media.FontFamily(viewModel.GlobalInterfaceFont.Trim());

        MainTitleLabel.IsVisible = viewModel.TitleContentTypeIndex != (int)LauncherTitleContentType.None &&
                                   viewModel.TitleContentTypeIndex != (int)LauncherTitleContentType.Image;
        MainTitleLabel.Text = viewModel.TitleContentTypeIndex == (int)LauncherTitleContentType.Text &&
                              !string.IsNullOrWhiteSpace(viewModel.CustomTitleText)
            ? viewModel.CustomTitleText
            : "PCL Aurora";
        MainTitleImage.IsVisible = viewModel.TitleContentTypeIndex == (int)LauncherTitleContentType.Image &&
                                   launcherTitleBitmap is not null;
        LauncherBackgroundImage.Opacity = viewModel.InterfaceBackgroundOpacity / 1000d;
        LauncherBackgroundImage.Effect = viewModel.InterfaceBackgroundBlurRadius > 0
            ? new Avalonia.Media.ImmutableBlurEffect(viewModel.InterfaceBackgroundBlurRadius)
            : null;
        LauncherBackgroundImage.Stretch = viewModel.BackgroundSuitIndex switch
        {
            (int)LauncherBackgroundSuitMode.Center => Avalonia.Media.Stretch.None,
            (int)LauncherBackgroundSuitMode.Stretch => Avalonia.Media.Stretch.Fill,
            (int)LauncherBackgroundSuitMode.Tile => Avalonia.Media.Stretch.None,
            _ => Avalonia.Media.Stretch.UniformToFill,
        };
        ApplyThemeColor(viewModel);
        ApplyFeatureVisibility(viewModel);
    }

    private void ApplyThemeColor(ViewModels.MainViewModel viewModel)
    {
        var isDark = viewModel.SelectedThemeMode.Mode == LauncherThemeMode.Dark ||
                     (viewModel.SelectedThemeMode.Mode == LauncherThemeMode.System &&
                      Avalonia.Application.Current?.ActualThemeVariant == Avalonia.Styling.ThemeVariant.Dark);
        var theme = (LauncherColorTheme)(isDark ? viewModel.DarkThemeColorIndex : viewModel.LightThemeColorIndex);
        var colors = theme switch
        {
            LauncherColorTheme.SkyBlue => (Primary: "#1787D4", Start: "#1069B8", Middle: "#1787D4", End: "#1069B8"),
            LauncherColorTheme.CrashBlue => (Primary: "#5B59C9", Start: "#4544A8", Middle: "#5B59C9", End: "#4544A8"),
            _ => (Primary: "#1370F3", Start: "#106AC4", Middle: "#1377DD", End: "#106AC5"),
        };

        if (Resources["PclThemePrimaryBrush"] is Avalonia.Media.SolidColorBrush primary)
        {
            primary.Color = Avalonia.Media.Color.Parse(colors.Primary);
        }

        if (Resources["PclTitleBarBrush"] is Avalonia.Media.LinearGradientBrush title && title.GradientStops.Count >= 3)
        {
            title.GradientStops[0].Color = Avalonia.Media.Color.Parse(colors.Start);
            title.GradientStops[1].Color = Avalonia.Media.Color.Parse(colors.Middle);
            title.GradientStops[2].Color = Avalonia.Media.Color.Parse(colors.End);
        }
    }

    private void ApplyFeatureVisibility(ViewModels.MainViewModel viewModel)
    {
        if (isFeatureHidingSuspended)
        {
            DownloadNavigation.IsVisible = true;
            SettingsNavigation.IsVisible = true;
            MoreNavigation.IsVisible = true;
            foreach (var navigation in SettingsNavigationPanel.GetVisualDescendants().OfType<PclNavigationButton>())
            {
                SetNavigationVisibility(navigation, true);
            }
            foreach (var navigation in MoreNavigationPanel.GetVisualDescendants().OfType<PclNavigationButton>())
            {
                SetNavigationVisibility(navigation, true);
            }
            return;
        }

        DownloadNavigation.IsVisible = !viewModel.HidePageDownload;
        SettingsNavigation.IsVisible = !viewModel.HidePageSettings;
        MoreNavigation.IsVisible = !viewModel.HidePageTools;

        foreach (var navigation in SettingsNavigationPanel.GetVisualDescendants().OfType<PclNavigationButton>())
        {
            var hidden = navigation.Tag switch
            {
                "launch" => viewModel.HideSetupLaunch,
                "java" => viewModel.HideSetupJava,
                "manage" => viewModel.HideSetupManage,
                "link" => viewModel.HideSetupLink,
                "interface" => viewModel.HideSetupInterface,
                "language" => viewModel.HideSetupLanguage,
                "misc" => viewModel.HideSetupMisc,
                "update" => viewModel.HideSetupUpdate,
                "about" => viewModel.HideSetupAbout,
                "feedback" => viewModel.HideSetupFeedback,
                "log" => viewModel.HideSetupLog,
                _ => false,
            };
            SetNavigationVisibility(navigation, !hidden);
        }

        foreach (var navigation in MoreNavigationPanel.GetVisualDescendants().OfType<PclNavigationButton>())
        {
            var hidden = navigation.Tag switch
            {
                "toolbox" => viewModel.HideToolsToolbox,
                "feedback" => viewModel.HideSetupFeedback,
                "logs" => viewModel.HideSetupLog,
                _ => false,
            };
            SetNavigationVisibility(navigation, !hidden);
        }
    }

    private static void SetNavigationVisibility(PclNavigationButton navigation, bool isVisible)
    {
        var row = navigation.GetVisualAncestors()
            .OfType<Grid>()
            .FirstOrDefault(candidate => candidate.Classes.Contains("nav-row"));
        if (row is not null)
        {
            row.IsVisible = isVisible;
        }
        else
        {
            navigation.IsVisible = isVisible;
        }
    }

    private void MainWindowKeyDown(object? sender, KeyEventArgs e)
    {
        if (MessageDialogHost.IsDialogOpen)
        {
            e.Handled = true;
            return;
        }

        if (e.Key != Key.F12 || DataContext is not ViewModels.MainViewModel viewModel)
        {
            return;
        }

        isFeatureHidingSuspended = !isFeatureHidingSuspended;
        ApplyFeatureVisibility(viewModel);
        e.Handled = true;
    }

    private void GameProcessStarted(object? sender, MinecraftLauncherVisibility visibility) =>
        Dispatcher.UIThread.Post(() =>
        {
            switch (visibility)
            {
                case MinecraftLauncherVisibility.ExitImmediately:
                    Close();
                    break;
                case MinecraftLauncherVisibility.HideAndExit:
                case MinecraftLauncherVisibility.HideAndReopen:
                    Hide();
                    break;
                case MinecraftLauncherVisibility.MinimizeAndReopen:
                    WindowState = WindowState.Minimized;
                    break;
            }
        });

    private void GameProcessExited(object? sender, MinecraftLauncherVisibility visibility) =>
        Dispatcher.UIThread.Post(() =>
        {
            switch (visibility)
            {
                case MinecraftLauncherVisibility.HideAndExit:
                    Close();
                    break;
                case MinecraftLauncherVisibility.HideAndReopen:
                    Show();
                    Activate();
                    break;
                case MinecraftLauncherVisibility.MinimizeAndReopen:
                    WindowState = WindowState.Normal;
                    Activate();
                    break;
            }
        });

    private async void MicrosoftDeviceCodeAvailable(object? sender, string code)
    {
        try
        {
            if (TopLevel.GetTopLevel(this)?.Clipboard is { } clipboard)
            {
                await clipboard.SetTextAsync(code);
            }
        }
        catch
        {
            // Clipboard access is optional; the dialog still exposes an explicit copy action.
        }
    }

    private async void ProjectPageLinkClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string target } &&
            DataContext is ViewModels.MainViewModel viewModel)
        {
            await viewModel.OpenProjectPageCommand.ExecuteAsync(target);
        }
    }

    private async void MainNavigationClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string value } || !int.TryParse(value, out var page))
        {
            return;
        }

        var loadTask = page == 1
            ? LoadDownloadSectionAsync(currentDownloadSection)
            : Task.CompletedTask;
        await SelectMainPageAsync(page);
        await loadTask;
    }

    private async void OpenDownloadPageClick(object? sender, RoutedEventArgs e)
    {
        var loadTask = LoadDownloadSectionAsync(currentDownloadSection);
        await SelectMainPageAsync(1);
        await loadTask;
    }

    private async void CopyMicrosoftCodeClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not ViewModels.MainViewModel { HasMicrosoftDeviceCode: true } viewModel ||
            TopLevel.GetTopLevel(this)?.Clipboard is not { } clipboard)
        {
            return;
        }

        await clipboard.SetTextAsync(viewModel.MicrosoftDeviceCode);
    }

    private void TitleBarPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.Source is not Button && e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            BeginMoveDrag(e);
        }
    }

    private void ResizeWindowPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (CanResize &&
            sender is Control { Tag: string edgeName } &&
            Enum.TryParse<WindowEdge>(edgeName, out var edge) &&
            e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            BeginResizeDrag(edge, e);
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

    private Task SelectMainPageAsync(int page)
    {
        var pages = new Control[] { LaunchPage, DownloadPage, SettingsPage, MorePage };
        var selectedPage = pages[page];

        var navigation = new[] { LaunchNavigation, DownloadNavigation, SettingsNavigation, MoreNavigation };
        for (var index = 0; index < navigation.Length; index++)
        {
            navigation[index].Classes.Set("selected", index == page);
        }

        return PclMotionService.SwitchSectionsAsync(MainPages, pages, selectedPage);
    }

    private async void MoreNavigationClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not PclNavigationButton { Tag: string section } selectedNavigation)
        {
            return;
        }

        foreach (var navigation in MoreNavigationPanel.GetVisualDescendants().OfType<PclNavigationButton>())
        {
            var isSelected = navigation == selectedNavigation;
            navigation.Classes.Set("selected", isSelected);
            var row = navigation.GetVisualAncestors()
                .OfType<Grid>()
                .FirstOrDefault(candidate => candidate.Classes.Contains("nav-row"));
            if (row is not null)
            {
                row.Classes.Set("selected", isSelected);
            }
        }

        var sectionChanged = currentMoreSection != section;
        currentMoreSection = section;
        await PclMotionService.SwitchSectionsAsync(
            MoreContentHost,
            [MoreContentHost],
            MoreContentHost,
            () => ApplyMoreSection(section),
            force: sectionChanged);
    }

    private void MoreSectionRefreshClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string section } || section != "help")
        {
            return;
        }

        PopulateMorePlaceholder("help");
    }

    private void ApplyMoreSection(string section)
    {
        MoreDirectorySection.IsVisible = section == "toolbox";
        MoreLogSection.IsVisible = section == "logs";
        MoreAboutSection.IsVisible = section == "about";
        MorePlaceholderSection.IsVisible = section is "help" or "feedback" or "vote";
        MorePageTitle.Text = section switch
        {
            "toolbox" => "百宝箱",
            "logs" => "查看日志",
            "about" => "关于与鸣谢",
            "feedback" => "反馈",
            "vote" => "新功能投票",
            _ => "帮助",
        };
        MorePageDescription.Text = section switch
        {
            "toolbox" => "打开常用目录并使用跨平台维护工具。",
            "logs" => "查看当前游戏会话输出与诊断信息。",
            "about" => "查看项目版本、更新入口、来源与许可证信息。",
            "feedback" => "提交问题报告、兼容性报告或功能建议。",
            "vote" => "了解候选功能并参与后续版本方向讨论。",
            _ => "查看启动、下载、实例与故障排查入口。",
        };

        if (MorePlaceholderSection.IsVisible)
        {
            PopulateMorePlaceholder(section);
        }
    }

    private void PopulateMorePlaceholder(string section)
    {
        var model = section switch
        {
            "feedback" => ("反馈", "选择反馈类型；实际提交将在 Aurora 的公开反馈入口接入后启用。", new[] { "问题反馈", "兼容性报告", "功能建议" }),
            "vote" => ("新功能投票", "查看候选功能与开发进度；投票数据将在后续接入 Aurora 自有服务。", new[] { "候选功能", "已采纳建议", "版本路线图" }),
            _ => ("帮助", "按主题查找常见问题与故障排查内容。", HelpTopics),
        };

        MorePlaceholderTitle.Text = model.Item1;
        MorePlaceholderDescription.Text = model.Item2;
        MoreHelpSearchBox.IsVisible = section == "help";
        MoreHelpSearchBox.Text = string.Empty;
        MorePlaceholderItems.ItemsSource = model.Item3;
    }

    private void MoreHelpSearchTextChanged(object? sender, TextChangedEventArgs e)
    {
        if (!MoreHelpSearchBox.IsVisible)
        {
            return;
        }

        var query = MoreHelpSearchBox.Text?.Trim() ?? string.Empty;
        MorePlaceholderItems.ItemsSource = string.IsNullOrEmpty(query)
            ? HelpTopics
            : HelpTopics.Where(topic => topic.Contains(query, StringComparison.OrdinalIgnoreCase)).ToArray();
    }

    private async void DownloadNavigationClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not PclNavigationButton { Tag: string section } selectedNavigation)
        {
            return;
        }

        await SelectDownloadSectionAsync(section, selectedNavigation);
    }

    private async void DownloadSectionRefreshClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string section })
        {
            return;
        }

        var selectedNavigation = DownloadNavigationPanel
            .GetVisualDescendants()
            .OfType<PclNavigationButton>()
            .FirstOrDefault(navigation => Equals(navigation.Tag, section));

        if (selectedNavigation is not null)
        {
            await SelectDownloadSectionAsync(section, selectedNavigation);
        }
    }

    private async Task SelectDownloadSectionAsync(string section, PclNavigationButton selectedNavigation)
    {
        var isCommunity = section is "mod" or "pack" or "datapack" or "resourcepack" or "shader" or "world" or "favorites";
        var loaderKind = section switch
        {
            "optifine" => MinecraftLoaderKind.OptiFine,
            "forge" => MinecraftLoaderKind.Forge,
            "neoforge" => MinecraftLoaderKind.NeoForge,
            "fabric" => MinecraftLoaderKind.Fabric,
            "cleanroom" => MinecraftLoaderKind.Cleanroom,
            "legacy-fabric" => MinecraftLoaderKind.LegacyFabric,
            "labymod" => MinecraftLoaderKind.LabyMod,
            "liteloader" => MinecraftLoaderKind.LiteLoader,
            _ => (MinecraftLoaderKind?)null,
        };
        var isGame = section == "game";
        var isDeferredInstaller = !isGame && !isCommunity && loaderKind is null;
        var sectionChanged = currentDownloadSection != section;
        currentDownloadSection = section;

        if (DataContext is ViewModels.MainViewModel viewModel)
        {
            viewModel.SetLoaderKindFilter(loaderKind);
            viewModel.SetCommunityResourceSection(isCommunity ? section : string.Empty);
        }

        foreach (var navigation in DownloadNavigationPanel.GetVisualDescendants().OfType<PclNavigationButton>())
        {
            var isSelected = navigation == selectedNavigation;
            navigation.Classes.Set("selected", isSelected);
            var row = navigation.GetVisualAncestors()
                .OfType<Grid>()
                .FirstOrDefault(candidate => candidate.Classes.Contains("nav-row"));
            if (row is not null)
            {
                row.Classes.Set("selected", isSelected);
            }
        }

        var selectedSection = isCommunity
            ? (Control)DownloadCommunityCard
            : isGame
                ? DownloadGameView
                : loaderKind is not null
                    ? DownloadLoaderView
                    : DownloadDeferredCard;
        Control[] downloadSections =
        [
            DownloadGameView,
            DownloadCombinedInstallView,
            DownloadLoaderView,
            DownloadCommunityCard,
            DownloadDeferredCard,
        ];
        var loadTask = LoadDownloadSectionAsync(section);
        await PclMotionService.SwitchSectionsAsync(
            DownloadContentScroller,
            downloadSections,
            selectedSection,
            () =>
            {
                DownloadDeferredTitle.Text = GetDownloadSectionTitle(section);
                DownloadContentScroller.Offset = default;
                if (isCommunity)
                {
                    ApplyCommunityCatalogState();
                }

                if (loaderKind is { } selectedLoaderKind)
                {
                    ConfigureLoaderPage(selectedLoaderKind);
                }
            },
            force: sectionChanged);
        await loadTask;
    }

    private async Task LoadDownloadSectionAsync(string section)
    {
        if (DataContext is not ViewModels.MainViewModel viewModel)
        {
            return;
        }

        if (section == "game")
        {
            await viewModel.LoadVersionCatalogPageAsync();
            return;
        }

        if (section is "mod" or "pack" or "datapack" or "resourcepack" or "shader" or "world" or "favorites")
        {
            await viewModel.LoadCommunityResourcePageAsync();
            return;
        }

        if (section is not ("optifine" or "forge" or "neoforge" or "fabric" or "cleanroom" or
            "legacy-fabric" or "labymod" or "liteloader"))
        {
            return;
        }

        var loaderKind = section switch
        {
            "optifine" => MinecraftLoaderKind.OptiFine,
            "forge" => MinecraftLoaderKind.Forge,
            "neoforge" => MinecraftLoaderKind.NeoForge,
            "fabric" => MinecraftLoaderKind.Fabric,
            "cleanroom" => MinecraftLoaderKind.Cleanroom,
            "legacy-fabric" => MinecraftLoaderKind.LegacyFabric,
            "labymod" => MinecraftLoaderKind.LabyMod,
            "liteloader" => MinecraftLoaderKind.LiteLoader,
            _ => throw new ArgumentOutOfRangeException(nameof(section), section, null),
        };
        await viewModel.LoadOfficialLoaderDirectoryPageAsync(loaderKind);
    }

    private void ConfigureLoaderPage(MinecraftLoaderKind kind)
    {
        var displayName = kind == MinecraftLoaderKind.LegacyFabric ? "Legacy Fabric" : kind.ToString();
        DownloadLoaderIntroTitle.Text = $"{displayName} 简介";
        LoaderCatalogLoadingIndicator.Text = $"正在获取 {displayName} 列表";
        DownloadLoaderWebsiteButton.Tag = kind;
        DownloadLoaderIntroDescription.Text = kind switch
        {
            MinecraftLoaderKind.Forge => "Forge 是一个模组加载器，你需要先安装 Forge 才能安装各种 Forge 模组。",
            MinecraftLoaderKind.NeoForge => "NeoForge 是 Minecraft 1.20.1+ 的模组加载器，你需要先安装它才能安装各种 NeoForge 模组，它也兼容一些 Forge 模组。",
            MinecraftLoaderKind.Fabric => "Fabric Loader 是新版 Minecraft 下的轻量化模组加载器，你需要先安装它才能安装各种 Fabric 模组。",
            MinecraftLoaderKind.OptiFine => "OptiFine 又称为高清修复，以允许安装光影、使用高清材质、提高游戏性能，但与模组的兼容性不佳。",
            MinecraftLoaderKind.Cleanroom => "Cleanroom 是针对 1.12.2 基于 Forge 二次开发的模组加载器，理论上与 99% 的 Forge 模组兼容。",
            MinecraftLoaderKind.LegacyFabric => "Legacy Fabric 是 Fabric 的旧版本移植，你需要先安装它才能安装各种 Legacy Fabric 模组。\n本页面提供 Legacy Fabric 安装器下载，在下载后你需要手动打开安装器进行安装。",
            MinecraftLoaderKind.LabyMod => "LabyMod 是 Minecraft 下的优化客户端。\n本页面提供 LabyMod 安装器下载，在下载后你需要手动打开安装器进行安装。",
            MinecraftLoaderKind.LiteLoader => "与 Forge 类似，LiteLoader 可以用于加载老版本 Minecraft 中的 LiteLoader 模组。",
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
        };
    }

    private static string GetDownloadSectionTitle(string section) => section switch
    {
        "game" => "原版游戏",
        "mod" => "模组",
        "pack" => "整合包",
        "datapack" => "数据包",
        "resourcepack" => "资源包",
        "shader" => "光影包",
        "world" => "世界",
        "favorites" => "收藏夹",
        "optifine" => "OptiFine",
        "forge" => "Forge",
        "neoforge" => "NeoForge",
        "fabric" => "Fabric",
        "cleanroom" => "Cleanroom",
        "legacy-fabric" => "Legacy Fabric",
        "labymod" => "LabyMod",
        "liteloader" => "LiteLoader",
        _ => "下载",
    };

    private async void CommunityResourceOpenClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: ViewModels.CommunityResourceItemViewModel item } ||
            DataContext is not ViewModels.MainViewModel viewModel)
        {
            return;
        }

        await ShowCommunityResourceDetailAsync(viewModel, item);
    }

    private async void LoaderWebsiteClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: MinecraftLoaderKind kind } &&
            DataContext is ViewModels.MainViewModel viewModel)
        {
            await viewModel.OpenLoaderWebsiteAsync(kind);
        }
    }

    private async void LoaderDirectoryGroupClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: ViewModels.MinecraftLoaderDirectoryGroupViewModel group } ||
            DataContext is not ViewModels.MainViewModel viewModel || !group.IsCollapsible)
        {
            return;
        }

        group.IsExpanded = !group.IsExpanded;
        if (group.IsExpanded)
        {
            await viewModel.LoadLoaderDirectoryGroupAsync(group);
        }
    }

    private async void LoaderPackageDownloadClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: ViewModels.MinecraftLoaderPackageItemViewModel item } ||
            DataContext is not ViewModels.MainViewModel viewModel || viewModel.IsLoaderPackageDownloading)
        {
            return;
        }

        var extension = Path.GetExtension(item.Package.FileName).TrimStart('.');
        var fileType = new FilePickerFileType(extension.Equals("zip", StringComparison.OrdinalIgnoreCase)
            ? "ZIP Archive"
            : "Java Archive")
        {
            Patterns = [$"*.{extension}"],
            MimeTypes = extension.Equals("zip", StringComparison.OrdinalIgnoreCase)
                ? ["application/zip"]
                : ["application/java-archive"],
        };
        var destination = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "选择保存位置",
            SuggestedFileName = item.Package.FileName,
            DefaultExtension = extension,
            FileTypeChoices = [fileType],
            SuggestedFileType = fileType,
            ShowOverwritePrompt = true,
        });
        var destinationPath = destination?.TryGetLocalPath();
        if (!string.IsNullOrWhiteSpace(destinationPath))
        {
            await viewModel.SaveLoaderPackageAsync(item.Package, destinationPath);
        }
    }

    private async void LoaderPackageChangelogClick(object? sender, RoutedEventArgs e)
    {
        e.Handled = true;
        if (sender is Button { Tag: ViewModels.MinecraftLoaderPackageItemViewModel item } &&
            DataContext is ViewModels.MainViewModel viewModel)
        {
            await viewModel.OpenLoaderPackageChangelogAsync(item.Package);
        }
    }

    private async void CommunityResourceQuickDownloadClick(object? sender, RoutedEventArgs e)
    {
        e.Handled = true;
        if (sender is not Button { Tag: ViewModels.CommunityResourceItemViewModel item } ||
            DataContext is not ViewModels.MainViewModel viewModel)
        {
            return;
        }

        await ShowCommunityResourceDetailAsync(viewModel, item);
    }

    private async Task ShowCommunityResourceDetailAsync(
        ViewModels.MainViewModel viewModel,
        ViewModels.CommunityResourceItemViewModel item)
    {
        viewModel.SelectedCommunityResource = item;
        var loadTask = viewModel.LoadSelectedCommunityResourceVersionsAsync();
        await PclMotionService.SwitchSectionsAsync(
            DownloadContentScroller,
            [DownloadCommunityCatalogView, DownloadCommunityDetailView],
            DownloadCommunityDetailView,
            () =>
            {
                MainTitleBar.IsVisible = false;
                CommunityDetailTitleBar.IsVisible = true;
                CommunityDetailTitleBarTitle.Text = item.Project.HasTranslatedTitle
                    ? $"资源下载 - {item.Project.DisplayTitle} ({item.Project.Title})"
                    : $"资源下载 - {item.Project.DisplayTitle}";
                DownloadPageLayout.ColumnDefinitions[0].Width = new GridLength(0);
                DownloadSidebar.IsVisible = false;
                DownloadContentScroller.Offset = default;
            });
        await loadTask;
    }

    private void CommunityVersionGameFilterClick(object? sender, RoutedEventArgs e)
    {
        if (sender is RadioButton { Tag: ViewModels.CommunityResourceVersionFilterOption option } &&
            DataContext is ViewModels.MainViewModel viewModel)
        {
            viewModel.SelectCommunityGameVersionFilter(option);
        }
    }

    private void CommunityVersionLoaderFilterClick(object? sender, RoutedEventArgs e)
    {
        if (sender is RadioButton { Tag: ViewModels.CommunityResourceVersionFilterOption option } &&
            DataContext is ViewModels.MainViewModel viewModel)
        {
            viewModel.SelectCommunityLoaderFilter(option);
        }
    }

    private async void CommunityVersionDownloadClick(object? sender, RoutedEventArgs e)
    {
        e.Handled = true;
        if (sender is Button { Tag: CommunityResourceVersion version } &&
            DataContext is ViewModels.MainViewModel { SelectedCommunityResource.Project: { } project } viewModel)
        {
            if (project.Type == CommunityResourceType.ModPack)
            {
                var instanceName = await ShowTextPromptAsync(
                    "导入整合包",
                    "输入整合包文件夹名称",
                    GetSuggestedModpackName(project));
                if (string.IsNullOrWhiteSpace(instanceName))
                {
                    return;
                }

                var parentFolders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
                {
                    Title = "选择整合包保存位置",
                    AllowMultiple = false,
                });
                var parentDirectory = parentFolders.SingleOrDefault()?.TryGetLocalPath();
                if (string.IsNullOrWhiteSpace(parentDirectory))
                {
                    return;
                }

                await viewModel.ImportCommunityModpackVersionAsync(version, parentDirectory, instanceName.Trim());
                return;
            }

            if (project.Type == CommunityResourceType.World)
            {
                var worldName = await ShowTextPromptAsync(
                    "导入世界",
                    "输入世界文件夹名称",
                    GetSuggestedResourceFolderName(project));
                if (string.IsNullOrWhiteSpace(worldName))
                {
                    return;
                }

                var parentFolders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
                {
                    Title = "选择世界保存位置",
                    AllowMultiple = false,
                });
                var parentDirectory = parentFolders.SingleOrDefault()?.TryGetLocalPath();
                if (string.IsNullOrWhiteSpace(parentDirectory))
                {
                    return;
                }

                await viewModel.ImportCommunityWorldVersionAsync(
                    version,
                    parentDirectory,
                    worldName.Trim());
                return;
            }

            IReadOnlyList<CommunityResourceVersion> dependencies = [];
            if (project.Type == CommunityResourceType.Mod && viewModel.AutoInstallDependencies)
            {
                var preparation = await viewModel.PrepareCommunityResourceDependenciesAsync(version);
                if (preparation is null)
                {
                    return;
                }

                dependencies = preparation.RequiredVersions;
                if (preparation.HasDependencies || preparation.Errors.Count > 0)
                {
                    var selection = await ShowCommunityDependencySelectionAsync(preparation);
                    if (selection is null)
                    {
                        return;
                    }

                    dependencies = selection;
                }
            }

            var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
            {
                Title = GetCommunityDownloadFolderTitle(project.Type, version.VersionNumber),
                AllowMultiple = false,
            });
            var destinationDirectory = folders.SingleOrDefault()?.TryGetLocalPath();
            if (string.IsNullOrWhiteSpace(destinationDirectory))
            {
                return;
            }

            var existingFiles = new[] { version }
                .Concat(dependencies)
                .DistinctBy(item => item.Id, StringComparer.OrdinalIgnoreCase)
                .Select(item => item.PrimaryFile?.FileName)
                .Where(fileName => !string.IsNullOrWhiteSpace(fileName))
                .Select(fileName => Path.Combine(destinationDirectory, Path.GetFileName(fileName!)))
                .Where(File.Exists)
                .Select(Path.GetFileName)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
            if (existingFiles.Length > 0 &&
                !await ShowConfirmationAsync(
                    "文件已存在",
                    existingFiles.Length == 1
                        ? $"“{existingFiles[0]}”已经存在，是否覆盖？"
                        : $"目标目录中已有 {existingFiles.Length} 个同名文件，是否覆盖？"))
            {
                return;
            }

            await viewModel.DownloadCommunityResourceVersionAsync(version, destinationDirectory, dependencies);
        }
    }

    private static string GetCommunityDownloadFolderTitle(CommunityResourceType type, string versionNumber) =>
        type switch
        {
            CommunityResourceType.Mod => $"选择 {versionNumber} 的模组保存目录",
            CommunityResourceType.DataPack => "选择数据包保存目录",
            CommunityResourceType.ResourcePack => "选择资源包保存目录",
            CommunityResourceType.Shader => "选择光影包保存目录",
            CommunityResourceType.World => "选择世界保存目录",
            _ => $"选择 {versionNumber} 的下载目录",
        };

    private static string GetSuggestedModpackName(CommunityResourceProject project) =>
        GetSuggestedResourceFolderName(project);

    private static string GetSuggestedResourceFolderName(CommunityResourceProject project)
    {
        var name = new string(project.DisplayTitle
            .Where(character => !char.IsControl(character) && "<>:\"/\\|?*".IndexOf(character) < 0)
            .Take(80)
            .ToArray()).Trim();
        return string.IsNullOrWhiteSpace(name) ? project.Slug : name;
    }

    private async Task<IReadOnlyList<CommunityResourceVersion>?> ShowCommunityDependencySelectionAsync(
        CommunityResourceDependencyPreparation preparation)
    {
        var dependencyItems = new StackPanel { Spacing = 6 };
        if (preparation.RequiredVersions.Count > 0)
        {
            dependencyItems.Children.Add(new TextBlock
            {
                Text = "必要依赖",
                FontWeight = Avalonia.Media.FontWeight.SemiBold,
                Foreground = Avalonia.Media.Brushes.DimGray,
            });
            foreach (var version in preparation.RequiredVersions)
            {
                dependencyItems.Children.Add(new CheckBox
                {
                    Content = $"{version.Name}（{version.VersionNumber}）",
                    IsChecked = true,
                    IsEnabled = false,
                    MinHeight = 32,
                    VerticalContentAlignment = Avalonia.Layout.VerticalAlignment.Center,
                });
            }
        }

        var optionalChecks = new List<(CheckBox CheckBox, CommunityResourceOptionalDependency Dependency)>();
        if (preparation.OptionalDependencies.Count > 0)
        {
            dependencyItems.Children.Add(new TextBlock
            {
                Text = "可选依赖",
                Margin = new Avalonia.Thickness(0, 6, 0, 0),
                FontWeight = Avalonia.Media.FontWeight.SemiBold,
                Foreground = Avalonia.Media.Brushes.DimGray,
            });
            foreach (var dependency in preparation.OptionalDependencies)
            {
                var checkBox = new CheckBox
                {
                    Content = dependency.DisplayName,
                    IsChecked = false,
                    MinHeight = 32,
                    VerticalContentAlignment = Avalonia.Layout.VerticalAlignment.Center,
                };
                optionalChecks.Add((checkBox, dependency));
                dependencyItems.Children.Add(checkBox);
            }
        }

        if (preparation.Errors.Count > 0)
        {
            dependencyItems.Children.Add(new Border
            {
                Margin = new Avalonia.Thickness(0, 6, 0, 0),
                Padding = new Avalonia.Thickness(10, 8),
                Background = Avalonia.Media.Brush.Parse("#FFF4D8"),
                CornerRadius = new Avalonia.CornerRadius(4),
                Child = new TextBlock
                {
                    Text = string.Join(Environment.NewLine, preparation.Errors),
                    Foreground = Avalonia.Media.Brush.Parse("#795A16"),
                    TextWrapping = Avalonia.Media.TextWrapping.Wrap,
                },
            });
        }

        var result = await MessageDialogHost.ShowAsync(new PclMessageDialogOptions(
            Title: "选择依赖",
            Message: "必要依赖将与模组本体一起下载，可选依赖由你决定。",
            PrimaryButtonText: "继续下载",
            SecondaryButtonText: "取消",
            Content: dependencyItems));
        if (result != 1)
        {
            return null;
        }

        return preparation.RequiredVersions
            .Concat(optionalChecks
                .Where(item => item.CheckBox.IsChecked == true)
                .SelectMany(item => item.Dependency.Versions))
            .DistinctBy(version => version.Id, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private void CommunityResourceFavoriteClick(object? sender, RoutedEventArgs e)
    {
        e.Handled = true;
        if (sender is not Button { Tag: ViewModels.CommunityResourceItemViewModel item } button ||
            DataContext is not ViewModels.MainViewModel viewModel)
        {
            return;
        }

        CommunityFavoriteMenuItems.Children.Clear();
        foreach (var folder in viewModel.CommunityFavoriteFolders)
        {
            var isFavorite = folder.Contains(item.Project.Id);
            CommunityFavoriteMenuItems.Children.Add(CreateCommunityFavoriteMenuButton(
                isFavorite
                    ? $"从“{folder.Name}”中取消收藏"
                    : $"收藏到“{folder.Name}”",
                () => viewModel.ToggleCommunityFavoriteAsync(item.Project, folder.Id)));
        }

        ShowCommunityFavoriteMenu(button);
    }

    private async void CommunityFavoriteFolderSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (DataContext is ViewModels.MainViewModel { IsCommunityFavoritesPage: true } viewModel)
        {
            await viewModel.LoadCommunityResourcePageAsync();
        }
    }

    private void CommunityFavoriteManageClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button button || DataContext is not ViewModels.MainViewModel viewModel)
        {
            return;
        }

        CommunityFavoriteMenuItems.Children.Clear();
        CommunityFavoriteMenuItems.Children.Add(CreateCommunityFavoriteMenuButton(
            "分享当前收藏夹",
            async () =>
            {
                var json = viewModel.ExportSelectedCommunityFavoriteFolder();
                if (!string.IsNullOrWhiteSpace(json) && TopLevel.GetTopLevel(this)?.Clipboard is { } clipboard)
                {
                    await clipboard.SetTextAsync(json);
                }
            }));
        CommunityFavoriteMenuItems.Children.Add(CreateCommunityFavoriteMenuButton(
            "导入收藏",
            async () =>
            {
                var json = await ShowTextPromptAsync(
                    "导入收藏",
                    "粘贴由 PCL Aurora 分享的收藏数据",
                    string.Empty,
                    multiline: true);
                if (!string.IsNullOrWhiteSpace(json) && await viewModel.ImportCommunityFavoriteFolderAsync(json))
                {
                    await viewModel.LoadCommunityResourcePageAsync();
                }
            }));
        CommunityFavoriteMenuItems.Children.Add(new Separator { Margin = new Avalonia.Thickness(4, 2) });
        CommunityFavoriteMenuItems.Children.Add(CreateCommunityFavoriteMenuButton(
            "新建收藏夹",
            async () =>
            {
                var name = await ShowTextPromptAsync("新建收藏夹", "输入收藏夹名称");
                if (!string.IsNullOrWhiteSpace(name))
                {
                    await viewModel.CreateCommunityFavoriteFolderAsync(name);
                    await viewModel.LoadCommunityResourcePageAsync();
                }
            }));
        CommunityFavoriteMenuItems.Children.Add(CreateCommunityFavoriteMenuButton(
            "重命名收藏夹名称",
            async () =>
            {
                if (viewModel.SelectedCommunityFavoriteFolder is not { } selected)
                {
                    return;
                }

                var name = await ShowTextPromptAsync("重命名收藏夹", "输入新的收藏夹名称", selected.Name);
                if (!string.IsNullOrWhiteSpace(name))
                {
                    await viewModel.RenameSelectedCommunityFavoriteFolderAsync(name);
                }
            }));
        CommunityFavoriteMenuItems.Children.Add(CreateCommunityFavoriteMenuButton(
            "删除当前收藏夹",
            async () =>
            {
                if (viewModel.SelectedCommunityFavoriteFolder is not { } selected ||
                    !await ShowConfirmationAsync(
                        "删除收藏夹",
                        $"确定删除“{selected.Name}”及其中的 {selected.Projects.Count} 项收藏吗？"))
                {
                    return;
                }

                if (await viewModel.DeleteSelectedCommunityFavoriteFolderAsync())
                {
                    await viewModel.LoadCommunityResourcePageAsync();
                }
            },
            isDestructive: true));
        ShowCommunityFavoriteMenu(button);
    }

    private Button CreateCommunityFavoriteMenuButton(
        string text,
        Func<Task> action,
        bool isDestructive = false)
    {
        var button = new Button
        {
            Content = text,
        };
        button.Classes.Add("community-favorite-menu-item");
        if (isDestructive)
        {
            button.Classes.Add("danger");
        }

        button.Click += async (_, eventArgs) =>
        {
            eventArgs.Handled = true;
            CommunityFavoriteMenuPopup.IsOpen = false;
            await action();
        };
        return button;
    }

    private void ShowCommunityFavoriteMenu(Control placementTarget)
    {
        CommunityFavoriteMenuPopup.PlacementTarget = placementTarget;
        CommunityFavoriteMenuPopup.IsOpen = true;
        var firstMenuItem = CommunityFavoriteMenuItems.Children.OfType<Button>().FirstOrDefault();
        Dispatcher.UIThread.Post(() => firstMenuItem?.Focus());
    }

    private async void CommunityResourceBackClick(object? sender, RoutedEventArgs e)
    {
        await PclMotionService.SwitchSectionsAsync(
            DownloadContentScroller,
            [DownloadCommunityCatalogView, DownloadCommunityDetailView],
            DownloadCommunityCatalogView,
            () =>
            {
                if (DataContext is ViewModels.MainViewModel viewModel)
                {
                    viewModel.SelectedCommunityResource = null;
                }

                ApplyCommunityCatalogState();
            });
    }

    private async void CommunityResourceCopyNameClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is ViewModels.MainViewModel { SelectedCommunityResource.Project: { } project } &&
            TopLevel.GetTopLevel(this)?.Clipboard is { } clipboard)
        {
            await clipboard.SetTextAsync(project.HasTranslatedTitle
                ? $"{project.DisplayTitle} ({project.Title})"
                : project.Title);
        }
    }

    private async void CommunityResourceCopyLinkClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is ViewModels.MainViewModel { SelectedCommunityResource.Project.WebsiteUrl: { } website } &&
            TopLevel.GetTopLevel(this)?.Clipboard is { } clipboard)
        {
            await clipboard.SetTextAsync(website.AbsoluteUri);
        }
    }

    private void ApplyCommunityCatalogState()
    {
        MainTitleBar.IsVisible = true;
        CommunityDetailTitleBar.IsVisible = false;
        DownloadPageLayout.ColumnDefinitions[0].Width = new GridLength(178);
        DownloadSidebar.IsVisible = true;
        DownloadCommunityCatalogView.IsVisible = true;
        DownloadCommunityDetailView.IsVisible = false;
        DownloadContentScroller.Offset = default;
    }

    private async Task<string?> ShowTextPromptAsync(
        string title,
        string message,
        string initialValue = "",
        bool multiline = false)
    {
        var input = new TextBox
        {
            Text = initialValue,
            AcceptsReturn = multiline,
            TextWrapping = multiline ? Avalonia.Media.TextWrapping.Wrap : Avalonia.Media.TextWrapping.NoWrap,
            Height = multiline ? 92 : 36,
            VerticalContentAlignment = Avalonia.Layout.VerticalAlignment.Center,
        };
        var result = await MessageDialogHost.ShowAsync(new PclMessageDialogOptions(
            Title: title,
            Message: message,
            Content: input,
            InitialFocus: input,
            EnterConfirms: !multiline));
        return result == 1 ? input.Text : null;
    }

    private async Task<bool> ShowConfirmationAsync(string title, string message, bool isWarning = true)
    {
        return await MessageDialogHost.ShowAsync(new PclMessageDialogOptions(
            Title: title,
            Message: message,
            IsWarning: isWarning)) == 1;
    }

    private async Task ShowMessageAsync(string title, string message, bool isWarning = false)
    {
        await MessageDialogHost.ShowAsync(new PclMessageDialogOptions(
            Title: title,
            Message: message,
            SecondaryButtonText: null,
            IsWarning: isWarning));
    }

    private void VersionCategoryToggleClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not ToggleButton { IsChecked: true, Tag: string category } ||
            DataContext is not ViewModels.MainViewModel viewModel)
        {
            return;
        }

        var (itemsControl, versions) = category switch
        {
            "release" => (ReleaseVersionItems, viewModel.ReleaseVersions),
            "snapshot" => (SnapshotVersionItems, viewModel.SnapshotVersions),
            "legacy" => (LegacyVersionItems, viewModel.LegacyVersions),
            "april-fools" => (AprilFoolsVersionItems, viewModel.AprilFoolsVersions),
            _ => throw new ArgumentOutOfRangeException(nameof(category), category, null),
        };
        itemsControl.ItemsSource ??= versions;
    }

    private async void OfficialVersionOpenClick(object? sender, RoutedEventArgs e)
    {
        if (!TrySelectOfficialVersion(sender, out var viewModel))
        {
            return;
        }

        var prepareTask = viewModel.PrepareCombinedInstallerAsync(viewModel.SelectedCatalogVersion!);
        await PclMotionService.SwitchSectionsAsync(
            DownloadContentScroller,
            [DownloadGameView, DownloadCombinedInstallView, DownloadLoaderView, DownloadCommunityCard, DownloadDeferredCard],
            DownloadCombinedInstallView,
            () => DownloadContentScroller.Offset = default);
        await prepareTask;
    }

    private async void OfficialVersionChangelogClick(object? sender, RoutedEventArgs e)
    {
        e.Handled = true;
        if (TrySelectOfficialVersion(sender, out var viewModel))
        {
            await viewModel.OpenSelectedVersionChangelogAsync();
        }
    }

    private async void OfficialVersionServerClick(object? sender, RoutedEventArgs e)
    {
        e.Handled = true;
        if (!TrySelectOfficialVersion(sender, out var viewModel))
        {
            return;
        }

        var jarType = new FilePickerFileType("Java Archive")
        {
            Patterns = ["*.jar"],
            MimeTypes = ["application/java-archive"],
            AppleUniformTypeIdentifiers = ["com.sun.java-archive"],
        };
        var destination = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = $"下载 Minecraft {viewModel.SelectedCatalogVersion!.Id} 服务端",
            SuggestedFileName = $"minecraft_server.{viewModel.SelectedCatalogVersion.Id}.jar",
            DefaultExtension = "jar",
            FileTypeChoices = [jarType],
            SuggestedFileType = jarType,
            ShowOverwritePrompt = true,
        });
        var destinationPath = destination?.TryGetLocalPath();
        if (string.IsNullOrWhiteSpace(destinationPath))
        {
            return;
        }

        await viewModel.SaveSelectedVersionServerAsync(destinationPath);
    }

    private async Task ChooseDirectoryAndInstallOfficialVersionAsync(ViewModels.MainViewModel viewModel)
    {
        if (viewModel.SelectedCatalogVersion is not { } version || viewModel.IsInstallationRunning)
        {
            return;
        }

        var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = $"选择 Minecraft {version.Id} 的存放目录",
            AllowMultiple = false,
        });
        var minecraftRootDirectory = folders.SingleOrDefault()?.TryGetLocalPath();
        if (string.IsNullOrWhiteSpace(minecraftRootDirectory))
        {
            return;
        }

        await viewModel.InstallSelectedOfficialVersionAsync(minecraftRootDirectory);
    }

    private async void CombinedInstallBackClick(object? sender, RoutedEventArgs e)
    {
        await PclMotionService.SwitchSectionsAsync(
            DownloadContentScroller,
            [DownloadGameView, DownloadCombinedInstallView, DownloadLoaderView, DownloadCommunityCard, DownloadDeferredCard],
            DownloadGameView,
            () => DownloadContentScroller.Offset = default);
    }

    private void CombinedInstallComponentVersionClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: MinecraftLoaderCatalogEntry loader } &&
            DataContext is ViewModels.MainViewModel viewModel)
        {
            viewModel.SelectCombinedInstallComponent(loader);
        }
    }

    private void CombinedInstallComponentClearClick(object? sender, RoutedEventArgs e)
    {
        e.Handled = true;
        if (sender is Button { Tag: MinecraftLoaderKind kind } &&
            DataContext is ViewModels.MainViewModel viewModel)
        {
            viewModel.ClearCombinedInstallComponent(kind);
        }
    }

    private async void CombinedInstallStartClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not ViewModels.MainViewModel { SelectedCatalogVersion: { } version } viewModel ||
            viewModel.IsInstallationRunning)
        {
            return;
        }

        var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = $"选择 {viewModel.CombinedInstallationName} 的 Minecraft 目录",
            AllowMultiple = false,
        });
        var minecraftRootDirectory = folders.SingleOrDefault()?.TryGetLocalPath();
        if (string.IsNullOrWhiteSpace(minecraftRootDirectory))
        {
            return;
        }

        await viewModel.InstallSelectedCombinedVersionAsync(minecraftRootDirectory);
    }

    private bool TrySelectOfficialVersion(object? sender, out ViewModels.MainViewModel viewModel)
    {
        viewModel = null!;
        if (sender is not Button button || DataContext is not ViewModels.MainViewModel candidate)
        {
            return false;
        }

        var version = button.Tag as MinecraftVersionCatalogEntry ??
                      button.DataContext as MinecraftVersionCatalogEntry;
        if (version is null)
        {
            return false;
        }

        candidate.SelectedCatalogVersion = version;
        viewModel = candidate;
        return true;
    }

    private async void SettingsNavigationClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not PclNavigationButton { Tag: string section } selectedNavigation)
        {
            return;
        }

        foreach (var navigation in SettingsNavigationPanel.GetVisualDescendants().OfType<PclNavigationButton>())
        {
            var isSelected = navigation == selectedNavigation;
            navigation.Classes.Set("selected", isSelected);
            var row = navigation.GetVisualAncestors()
                .OfType<Grid>()
                .FirstOrDefault(candidate => candidate.Classes.Contains("nav-row"));
            if (row is not null)
            {
                row.Classes.Set("selected", isSelected);
            }
        }

        if (section == "about" && DataContext is ViewModels.MainViewModel viewModel)
        {
            viewModel.LoadContributorsCommand.Execute(null);
        }

        if (section == "interface" && DataContext is ViewModels.MainViewModel interfaceViewModel)
        {
            RefreshBackgroundVisual(interfaceViewModel);
            RefreshTitleVisual(interfaceViewModel);
        }

        var selectedSection = section switch
        {
            "launch" => (Control)SettingsLaunchSection,
            "java" => SettingsJavaSection,
            "manage" => SettingsManageSection,
            "link" => SettingsLinkSection,
            "interface" => SettingsInterfaceSection,
            "language" => SettingsLanguageSection,
            "misc" => SettingsMiscSection,
            "about" => SettingsAboutSection,
            "update" => SettingsUpdateSection,
            "feedback" => SettingsFeedbackSection,
            "log" => SettingsLogSection,
            _ => throw new ArgumentOutOfRangeException(nameof(section), section, null),
        };
        await PclMotionService.SwitchSectionsAsync(
            SettingsPage,
            [
                SettingsLaunchSection,
                SettingsJavaSection,
                SettingsManageSection,
                SettingsLinkSection,
                SettingsInterfaceSection,
                SettingsLanguageSection,
                SettingsMiscSection,
                SettingsAboutSection,
                SettingsUpdateSection,
                SettingsFeedbackSection,
                SettingsLogSection,
            ],
            selectedSection);
    }

    private async void OpenBackgroundFolderClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is ViewModels.MainViewModel viewModel)
        {
            await viewModel.OpenInterfaceContentDirectoryAsync("background");
        }
    }

    private void RefreshBackgroundContentClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is ViewModels.MainViewModel viewModel)
        {
            RefreshBackgroundVisual(viewModel);
        }
    }

    private async void ClearBackgroundContentClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not ViewModels.MainViewModel viewModel ||
            !await ShowConfirmationAsync("清空背景内容", "即将删除背景内容文件夹中的所有文件。此操作不可撤销，是否确定？"))
        {
            return;
        }

        ClearDirectoryFiles(viewModel.GetInterfaceContentDirectory("background"));
        RefreshBackgroundVisual(viewModel);
    }

    private async void OpenMusicFolderClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is ViewModels.MainViewModel viewModel)
        {
            await viewModel.OpenInterfaceContentDirectoryAsync("music");
        }
    }

    private void RefreshMusicContentClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is ViewModels.MainViewModel viewModel)
        {
            Directory.CreateDirectory(viewModel.GetInterfaceContentDirectory("music"));
        }
    }

    private async void ClearMusicContentClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not ViewModels.MainViewModel viewModel ||
            !await ShowConfirmationAsync("清空背景音乐", "即将删除背景音乐文件夹中的所有文件。此操作不可撤销，是否确定？"))
        {
            return;
        }

        ClearDirectoryFiles(viewModel.GetInterfaceContentDirectory("music"));
    }

    private async void ChangeTitleImageClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not ViewModels.MainViewModel viewModel)
        {
            return;
        }

        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "选择标题栏图片",
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType("图片") { Patterns = ["*.png", "*.jpg", "*.jpeg", "*.webp", "*.bmp"] },
            ],
        });
        var source = files.SingleOrDefault()?.TryGetLocalPath();
        if (string.IsNullOrWhiteSpace(source))
        {
            return;
        }

        var directory = viewModel.GetInterfaceContentDirectory("title");
        ClearDirectoryFiles(directory);
        File.Copy(source, Path.Combine(directory, $"Title{Path.GetExtension(source)}"), overwrite: true);
        RefreshTitleVisual(viewModel);
    }

    private void ClearTitleImageClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not ViewModels.MainViewModel viewModel)
        {
            return;
        }

        ClearDirectoryFiles(viewModel.GetInterfaceContentDirectory("title"));
        RefreshTitleVisual(viewModel);
    }

    private void RefreshBackgroundVisual(ViewModels.MainViewModel viewModel)
    {
        var path = FindFirstImage(viewModel.GetInterfaceContentDirectory("background"));
        launcherBackgroundBitmap?.Dispose();
        launcherBackgroundBitmap = TryLoadBitmap(path);
        LauncherBackgroundImage.Source = launcherBackgroundBitmap;
        LauncherBackgroundImage.IsVisible = launcherBackgroundBitmap is not null;
        ApplyInterfacePreferences(viewModel);
    }

    private void RefreshTitleVisual(ViewModels.MainViewModel viewModel)
    {
        var path = FindFirstImage(viewModel.GetInterfaceContentDirectory("title"));
        launcherTitleBitmap?.Dispose();
        launcherTitleBitmap = TryLoadBitmap(path);
        MainTitleImage.Source = launcherTitleBitmap;
        ApplyInterfacePreferences(viewModel);
    }

    private static Bitmap? TryLoadBitmap(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        try
        {
            return new Bitmap(path);
        }
        catch
        {
            return null;
        }
    }

    private static string? FindFirstImage(string directory) =>
        Directory.EnumerateFiles(directory)
            .Where(path => Path.GetExtension(path).ToLowerInvariant() is ".png" or ".jpg" or ".jpeg" or ".webp" or ".bmp")
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault();

    private static void ClearDirectoryFiles(string directory)
    {
        foreach (var path in Directory.EnumerateFiles(directory))
        {
            File.Delete(path);
        }
    }

    private async void AnnouncementModeSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (isRevertingAnnouncementSelection || sender is not ComboBox { SelectedIndex: 2 } comboBox)
        {
            return;
        }

        var confirmed = await ShowConfirmationAsync(
            "关闭启动器公告？",
            "关闭后，即使将来出现严重问题，你也无法收到相关通知。通常选择“仅在有重要通知时显示公告”即可尽量不受打扰。是否仍要关闭所有公告？");
        if (confirmed)
        {
            return;
        }

        isRevertingAnnouncementSelection = true;
        comboBox.SelectedItem = e.RemovedItems.Count > 0 ? e.RemovedItems[0] : comboBox.Items[0];
        isRevertingAnnouncementSelection = false;
    }

    private async void ApplyProxySettingsClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is ViewModels.MainViewModel viewModel)
        {
            await viewModel.ApplyProxySettingsAsync();
        }
    }

    private async void ExportLauncherSettingsClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not ViewModels.MainViewModel viewModel)
        {
            return;
        }

        var jsonType = new FilePickerFileType("PCL Aurora 配置文件")
        {
            Patterns = ["*.json"],
            MimeTypes = ["application/json"],
            AppleUniformTypeIdentifiers = ["public.json"],
        };
        var destination = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "选择保存位置",
            SuggestedFileName = "PCL Aurora 全局配置.json",
            DefaultExtension = "json",
            FileTypeChoices = [jsonType],
            SuggestedFileType = jsonType,
            ShowOverwritePrompt = true,
        });
        if (destination is null)
        {
            return;
        }

        await using var stream = await destination.OpenWriteAsync();
        stream.SetLength(0);
        await viewModel.ExportSettingsAsync(stream);
    }

    private async void ImportLauncherSettingsClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not ViewModels.MainViewModel viewModel)
        {
            return;
        }

        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "选择配置文件",
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType("PCL Aurora 配置文件")
                {
                    Patterns = ["*.json"],
                    MimeTypes = ["application/json"],
                    AppleUniformTypeIdentifiers = ["public.json"],
                },
            ],
        });
        var source = files.SingleOrDefault();
        if (source is null)
        {
            return;
        }

        try
        {
            await using var stream = await source.OpenReadAsync();
            await viewModel.ImportSettingsAsync(stream);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or System.Text.Json.JsonException or InvalidDataException)
        {
            await ShowMessageAsync("导入失败", exception.Message, isWarning: true);
        }
    }

    private async void StopUsingAuroraClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not ViewModels.MainViewModel viewModel ||
            !await ShowConfirmationAsync(
                "停止使用 PCL Aurora？",
                "即将清除 PCL Aurora 保存的启动器设置、代理密码和 Microsoft 登录凭据。Minecraft 实例、模组、存档与游戏文件不会被删除。此操作无法撤销，是否继续？") ||
            !await ShowConfirmationAsync("最后确认", "确定清除 PCL Aurora 的本地设置并关闭软件吗？"))
        {
            return;
        }

        await viewModel.StopUsingAuroraAsync();
        if (Avalonia.Application.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.Shutdown();
        }
    }

    private async void SettingsSectionRefreshClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string section } || DataContext is not ViewModels.MainViewModel viewModel)
        {
            return;
        }

        if (section == "java")
        {
            await viewModel.RefreshCommand.ExecuteAsync(null);
        }
    }

    private async void SettingsSectionResetClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string section } || DataContext is not ViewModels.MainViewModel viewModel)
        {
            return;
        }

        var (title, message) = section switch
        {
            "launch" => ("初始化确认", "是否要初始化 游戏-启动 页面的所有设置？该操作不可撤销。"),
            "manage" => ("初始化确认", "是否要初始化 游戏-管理 页面的所有设置？该操作不可撤销。"),
            "interface" => ("初始化确认", "是否要初始化 启动器-个性化 页面的所有设置？该操作不可撤销。"),
            "language" => ("初始化确认", "是否要初始化 启动器-语言 页面的所有设置？该操作不可撤销。"),
            "misc" => ("初始化确认", "是否要初始化 启动器-杂项 页面的所有设置？该操作不可撤销。"),
            _ => (string.Empty, string.Empty),
        };
        if (title.Length == 0 || !await ShowConfirmationAsync(title, message))
        {
            return;
        }

        switch (section)
        {
            case "launch":
                await viewModel.ResetLaunchSettingsAsync();
                break;
            case "manage":
                await viewModel.ResetGameManagementSettingsAsync();
                break;
            case "interface":
                await viewModel.ResetInterfaceSettingsAsync();
                break;
            case "language":
                await viewModel.ResetLocalizationSettingsAsync();
                break;
            case "misc":
                await viewModel.ResetMiscSettingsAsync();
                break;
        }
    }

    private void MemoryAutoClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is ViewModels.MainViewModel viewModel)
        {
            viewModel.SetMemoryAllocationMode(MinecraftMemoryAllocationMode.Automatic);
        }
    }

    private void MemoryCustomClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is ViewModels.MainViewModel viewModel)
        {
            viewModel.SetMemoryAllocationMode(MinecraftMemoryAllocationMode.Custom);
        }
    }

    private async void ContributorClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: Uri profileUri } &&
            DataContext is ViewModels.MainViewModel viewModel)
        {
            await viewModel.OpenContributorPageCommand.ExecuteAsync(profileUri);
        }
    }
}
