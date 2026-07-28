using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using PCL.Aurora.Desktop.Controls;
using PCL.Aurora.Domain;

namespace PCL.Aurora.Desktop.Views;

public partial class MainWindow : Window
{
    private string currentDownloadSection = "game";

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
        PopulateMorePlaceholder("help");
        Opened += async (_, _) =>
        {
            if (DataContext is ViewModels.MainViewModel viewModel)
            {
                await viewModel.InitializeAsync();
            }
        };
    }

    private async void MainNavigationClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string value } || !int.TryParse(value, out var page))
        {
            return;
        }

        SelectMainPage(page);
        if (page == 1)
        {
            await LoadDownloadSectionAsync(currentDownloadSection);
        }
    }

    private async void OpenDownloadPageClick(object? sender, RoutedEventArgs e)
    {
        SelectMainPage(1);
        await LoadDownloadSectionAsync(currentDownloadSection);
    }

    private async void OfficialLoginClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is ViewModels.MainViewModel viewModel)
        {
            await viewModel.BeginMicrosoftLoginAsync();
        }
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
        if (sender is not PclNavigationButton { Tag: string section } selectedNavigation)
        {
            return;
        }

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

        foreach (var navigation in MoreNavigationPanel.Children.OfType<PclNavigationButton>())
        {
            navigation.Classes.Set("selected", navigation == selectedNavigation);
        }

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
            _ => (MinecraftLoaderKind?)null,
        };
        var isGame = section == "game";
        var isDeferredInstaller = !isGame && !isCommunity && loaderKind is null;
        currentDownloadSection = section;

        DownloadCommunityCard.IsVisible = isCommunity;
        DownloadDeferredCard.IsVisible = isDeferredInstaller;
        DownloadGameView.IsVisible = isGame;
        DownloadLoaderView.IsVisible = loaderKind is not null;
        DownloadDeferredTitle.Text = GetDownloadSectionTitle(section);
        DownloadContentScroller.Offset = default;

        if (DataContext is ViewModels.MainViewModel viewModel)
        {
            viewModel.SetLoaderKindFilter(loaderKind);
            viewModel.SetCommunityResourceSection(isCommunity ? section : string.Empty);
        }

        if (loaderKind is { } selectedLoaderKind)
        {
            ConfigureLoaderPage(selectedLoaderKind);
        }

        foreach (var navigation in DownloadNavigationPanel.GetVisualDescendants().OfType<PclNavigationButton>())
        {
            var isSelected = navigation == selectedNavigation;
            navigation.Classes.Set("selected", isSelected);
            var row = navigation.GetVisualAncestors()
                .OfType<Grid>()
                .FirstOrDefault(candidate => candidate.Classes.Contains("download-navigation-row"));
            if (row is not null)
            {
                row.Classes.Set("selected", isSelected);
            }
        }

        await LoadDownloadSectionAsync(section);
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

        if (section is not ("optifine" or "forge" or "neoforge" or "fabric"))
        {
            return;
        }

        if (viewModel.SelectedCatalogVersion is null)
        {
            await viewModel.LoadVersionCatalogPageAsync();
        }

        await viewModel.LoadOfficialLoaderCatalogPageAsync();
    }

    private void ConfigureLoaderPage(MinecraftLoaderKind kind)
    {
        DownloadForgeImage.IsVisible = kind == MinecraftLoaderKind.Forge;
        DownloadNeoForgeImage.IsVisible = kind == MinecraftLoaderKind.NeoForge;
        DownloadFabricImage.IsVisible = kind == MinecraftLoaderKind.Fabric;
        DownloadOptiFineImage.IsVisible = kind == MinecraftLoaderKind.OptiFine;
        DownloadLoaderIntroTitle.Text = $"{kind} 简介";
        LoaderCatalogLoadingIndicator.Text = $"正在获取 {kind} 列表";
        DownloadLoaderIntroDescription.Text = kind switch
        {
            MinecraftLoaderKind.Forge => "Forge 是一个模组加载器，你需要先安装 Forge 才能安装各种 Forge 模组。",
            MinecraftLoaderKind.NeoForge => "NeoForge 是 Minecraft 1.20.1+ 的模组加载器，你需要先安装它才能安装各种 NeoForge 模组，它也兼容一些 Forge 模组。",
            MinecraftLoaderKind.Fabric => "Fabric Loader 是新版 Minecraft 下的轻量化模组加载器，你需要先安装它才能安装各种 Fabric 模组。",
            MinecraftLoaderKind.OptiFine => "OptiFine 又称为高清修复，以允许安装光影、使用高清材质、提高游戏性能，但与模组的兼容性不佳。",
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

    private void VersionFilterClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string filter } || DataContext is not ViewModels.MainViewModel viewModel)
        {
            return;
        }

        viewModel.VersionSearchText = string.Empty;
        viewModel.SelectedVersionCategory = filter switch
        {
            "release" => MinecraftVersionCatalogCategory.Release,
            "snapshot" => MinecraftVersionCatalogCategory.Snapshot,
            "legacy" => MinecraftVersionCatalogCategory.Legacy,
            "april-fools" => MinecraftVersionCatalogCategory.AprilFools,
            _ => throw new ArgumentOutOfRangeException(nameof(filter), filter, null),
        };
        ReleaseVersionGroup.Classes.Set("selected", filter == "release");
        SnapshotVersionGroup.Classes.Set("selected", filter == "snapshot");
        LegacyVersionGroup.Classes.Set("selected", filter == "legacy");
        AprilFoolsVersionGroup.Classes.Set("selected", filter == "april-fools");
    }

    private void SettingsNavigationClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not PclNavigationButton { Tag: string section } selectedNavigation)
        {
            return;
        }

        SettingsLaunchSection.IsVisible = section == "launch";
        SettingsJavaSection.IsVisible = section == "java";
        SettingsInterfaceSection.IsVisible = section == "interface";
        SettingsPlaceholderSection.IsVisible = section is not ("launch" or "java" or "interface");

        foreach (var navigation in SettingsNavigationPanel.Children.OfType<PclNavigationButton>())
        {
            navigation.Classes.Set("selected", navigation == selectedNavigation);
        }

        if (SettingsPlaceholderSection.IsVisible)
        {
            PopulateSettingsPlaceholder(section);
        }
    }

    private void PopulateSettingsPlaceholder(string section)
    {
        var model = section switch
        {
            "manage" => ("管理", "管理实例扫描、分类与默认游戏目录。", "实例隔离", new[] { "隔离所有实例" }, "自动扫描", new[] { "启动时扫描" }, "默认排序", new[] { "按名称" }),
            "link" => ("联机", "配置跨平台联机方式与房间可见性。", "联机服务", new[] { "选择服务" }, "房间权限", new[] { "仅邀请" }, "连接质量", new[] { "自动检测" }),
            "language" => ("语言", "选择启动器界面语言和区域格式。", "界面语言", new[] { "简体中文" }, "区域格式", new[] { "跟随系统" }, "游戏语言", new[] { "跟随实例" }),
            "misc" => ("杂项", "配置通知、下载行为和启动器后台策略。", "关闭行为", new[] { "询问" }, "通知方式", new[] { "系统通知" }, "缓存清理", new[] { "手动" }),
            "about" => ("软件信息", "查看 PCL Aurora、PCL2 与 PCL-CE 的版本和来源信息。", "当前版本", new[] { "开发版本" }, "更新渠道", new[] { "正式版" }, "许可证", new[] { "查看随附材料" }),
            "update" => ("软件更新", "检查 PCL Aurora 发布页和可用更新。", "更新通道", new[] { "正式版" }, "检查频率", new[] { "启动时" }, "下载策略", new[] { "手动确认" }),
            "feedback" => ("反馈", "整理诊断信息并前往 Aurora 的问题反馈入口。", "反馈类型", new[] { "问题报告" }, "附加日志", new[] { "由用户确认" }, "公开范围", new[] { "提交前检查" }),
            "log" => ("查看日志", "查看启动器诊断、下载任务和游戏会话日志。", "日志来源", new[] { "启动器" }, "日志级别", new[] { "信息" }, "时间范围", new[] { "本次会话" }),
            _ => ("设置", "该设置页面将在后续迁移阶段接入。", "选项", new[] { "默认" }, "范围", new[] { "全局" }, "状态", new[] { "尚未接入" }),
        };

        SettingsPlaceholderTitle.Text = model.Item1;
        SettingsPlaceholderDescription.Text = model.Item2;
        SetPlaceholderOption(SettingsPlaceholderLabel1, SettingsPlaceholderOption1, model.Item3, model.Item4);
        SetPlaceholderOption(SettingsPlaceholderLabel2, SettingsPlaceholderOption2, model.Item5, model.Item6);
        SetPlaceholderOption(SettingsPlaceholderLabel3, SettingsPlaceholderOption3, model.Item7, model.Item8);
    }

    private static void SetPlaceholderOption(TextBlock label, ComboBox comboBox, string labelText, string[] options)
    {
        label.Text = labelText;
        comboBox.ItemsSource = options;
        comboBox.SelectedIndex = 0;
    }
}
