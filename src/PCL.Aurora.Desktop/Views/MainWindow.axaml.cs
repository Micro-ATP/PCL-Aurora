using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using PCL.Aurora.Desktop.Controls;
using PCL.Aurora.Domain;

namespace PCL.Aurora.Desktop.Views;

public partial class MainWindow : Window
{
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

    private void DownloadNavigationClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not PclNavigationButton { Tag: string section } selectedNavigation)
        {
            return;
        }

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

        DownloadCommunityCard.IsVisible = isCommunity;
        DownloadDeferredCard.IsVisible = isDeferredInstaller;
        DownloadGameView.IsVisible = isGame;
        DownloadLoaderView.IsVisible = loaderKind is not null;
        DownloadPageTitle.Text = GetDownloadSectionTitle(section);
        DownloadPageDescription.Text = GetDownloadSectionDescription(section);
        if (isCommunity)
        {
            DownloadCommunityTitle.Text = DownloadPageTitle.Text;
            DownloadCommunityDescription.Text = DownloadPageDescription.Text;
        }

        if (DataContext is ViewModels.MainViewModel viewModel)
        {
            viewModel.SetLoaderKindFilter(loaderKind);
        }

        if (loaderKind is { } selectedLoaderKind)
        {
            ConfigureLoaderPage(selectedLoaderKind);
        }

        foreach (var navigation in DownloadNavigationPanel.Children.OfType<PclNavigationButton>())
        {
            navigation.Classes.Set("selected", navigation == selectedNavigation);
        }
    }

    private void ConfigureLoaderPage(MinecraftLoaderKind kind)
    {
        DownloadForgeImage.IsVisible = kind == MinecraftLoaderKind.Forge;
        DownloadNeoForgeImage.IsVisible = kind == MinecraftLoaderKind.NeoForge;
        DownloadFabricImage.IsVisible = kind == MinecraftLoaderKind.Fabric;
        DownloadOptiFineImage.IsVisible = kind == MinecraftLoaderKind.OptiFine;
        DownloadLoaderIntroTitle.Text = kind.ToString();
        DownloadLoaderIntroDescription.Text = kind switch
        {
            MinecraftLoaderKind.Forge => "按 Minecraft 版本读取 Forge 官方 Maven 目录，并通过官方安装器写入已选择的本地实例。",
            MinecraftLoaderKind.NeoForge => "按 Minecraft 版本读取 NeoForge 官方目录，并通过官方安装器写入已选择的本地实例。",
            MinecraftLoaderKind.Fabric => "读取 Fabric Meta 的稳定版与预览版目录，并通过官方安装器写入已选择的本地实例。",
            MinecraftLoaderKind.OptiFine => "读取 PCL 使用的公开 OptiFine 目录。1.14 及以上运行官方安装器，旧版本创建受控继承实例。",
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

    private static string GetDownloadSectionDescription(string section) => section switch
    {
        "game" => "选择官方 Minecraft 版本，再创建本地实例并执行已验证的安装流程。",
        "mod" => "浏览并筛选适用于当前 Minecraft 版本的模组。",
        "pack" => "浏览整合包并查看其游戏版本、加载器与依赖信息。",
        "datapack" => "浏览可安装到世界存档的数据包。",
        "resourcepack" => "浏览资源包并按游戏版本和分辨率筛选。",
        "shader" => "浏览光影包及其加载器兼容信息。",
        "world" => "浏览世界存档与地图作品。",
        "favorites" => "集中查看已收藏的社区资源。",
        "optifine" => "选择与当前实例兼容的 OptiFine 版本。1.14+ 使用安装器，旧版创建受控继承版本。",
        "forge" or "neoforge" or "fabric" => "选择当前实例兼容的加载器版本后，执行已验证的安装流程。",
        _ => "选择目标游戏版本和安装通道；实际安装能力将在对应迁移阶段接入。",
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
