using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Platform;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Avalonia.Media.Imaging;
using Avalonia.Media;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using PCL.Aurora.Application;
using PCL.Aurora.Desktop.Controls;
using PCL.Aurora.Desktop.Services;
using PCL.Aurora.Desktop.Models;
using PCL.Aurora.Domain;
using PCL.Aurora.Platform.Abstractions;

namespace PCL.Aurora.Desktop.Views;

public partial class MainWindow : Window
{
    private const int MaximumHomepageContentLength = 2_000_000;
    private static readonly Uri HomepageTutorialUri =
        new("https://docs.pclc.cc/ce/customization/xaml-format");
    private static readonly Uri MinecraftNewsApiUri =
        new("https://net-secondary.web.minecraft-services.net/api/v1.0/zh-cn/search?pageSize=24&sortType=Recent&category=News&newsOnly=true&page=1");
    private static readonly Uri DontClickDownloadUri =
        new("https://autopatchcn.yuanshen.com/client_app/download/launcher/20260626174956_W8SYYRxXIJQrUt40/pcbackup319/yuanshen_setup_20260626.exe");
    private const string DontClickDownloadFileName = "yuanshen_setup_20260626.exe";
    private static readonly string[] HomepageHints =
    [
        "为不同 Minecraft 版本保留独立实例目录，可以减少模组与配置互相影响。",
        "内存并非越大越好；大型整合包通常也不需要占用全部可用内存。",
        "遇到启动失败时，设置中的查看日志可以导出当前启动器日志。",
        "社区资源页面支持收藏，常用模组、光影和资源包可以集中管理。",
        "PCL Aurora 会校验下载文件，校验失败的文件不会直接覆盖现有内容。",
    ];

    private string currentDownloadSection = "game";
    private string currentMoreSection = "help";
    private ViewModels.MainViewModel? subscribedViewModel;
    private Bitmap? launcherBackgroundBitmap;
    private Bitmap? launcherTitleBitmap;
    private Bitmap? startupLogoBitmap;
    private bool isFeatureHidingSuspended;
    private bool isRevertingAnnouncementSelection;
    private readonly HttpClient toolboxHttpClient = new() { Timeout = TimeSpan.FromSeconds(30) };
    private CancellationTokenSource? toolboxDownloadCancellation;
    private CancellationTokenSource? homepageRefreshCancellation;
    private CancellationTokenSource? launchingHintCancellation;
    private Bitmap? toolboxAvatarBitmap;
    private string? lastProcessedClipboardText;
    private bool isResolvingClipboardResource;

    internal INativeWindowAppearanceService? NativeWindowAppearanceService { get; init; }

    public MainWindow()
    {
        InitializeComponent();
        try
        {
            using var startupLogoStream = AssetLoader.Open(
                new Uri("avares://PCL.Aurora.Desktop/Assets/Icons/AppIcon-2048.png"));
            startupLogoBitmap = new Bitmap(startupLogoStream);
            StartupLogoImage.Source = startupLogoBitmap;
        }
        catch
        {
            StartupLogoOverlay.IsVisible = false;
            MainShellBorder.Opacity = 1;
        }
        var downloadsDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            "Downloads");
        if (Directory.Exists(downloadsDirectory))
        {
            ToolboxDownloadFolderTextBox.Text = downloadsDirectory;
        }
        PclMotionService.Attach(this);
        PclHelpView.DetailOpened += ShowHelpDetail;
        PclHelpView.DetailClosed += RestoreHelpCatalogLayout;
        PclHelpView.ActionRequested += HandleHelpAction;
        PclHomepageView.ActionRequested += HandleHelpAction;
        DataContextChanged += (_, _) => SubscribeToViewModel();
        KeyDown += MainWindowKeyDown;
        SizeChanged += (_, _) =>
        {
            UpdateLauncherWindowSize();
            if (DataContext is ViewModels.MainViewModel viewModel)
            {
                ApplyBackgroundLayout(viewModel);
            }
        };
        Activated += MainWindowActivated;
        Opened += async (_, _) =>
        {
            ApplyNativeWindowAppearance();
            SubscribeToViewModel();
            UpdateLauncherWindowSize();
            if (DataContext is ViewModels.MainViewModel viewModel)
            {
                await viewModel.InitializeAsync();
                await CompleteStartupLogoAsync(viewModel.ShowStartupLogo);
                await RefreshHomepageAsync(showSuccess: false);
            }
        };
        Closed += (_, _) =>
        {
            if (DataContext is ViewModels.MainViewModel viewModel)
            {
                _ = viewModel.StopBackgroundMusicAsync();
            }
            SubscribeToViewModel(null);
            toolboxDownloadCancellation?.Cancel();
            toolboxDownloadCancellation?.Dispose();
            homepageRefreshCancellation?.Cancel();
            homepageRefreshCancellation?.Dispose();
            launchingHintCancellation?.Cancel();
            launchingHintCancellation?.Dispose();
            toolboxHttpClient.Dispose();
            toolboxAvatarBitmap?.Dispose();
            launcherBackgroundBitmap?.Dispose();
            launcherTitleBitmap?.Dispose();
            startupLogoBitmap?.Dispose();
        };
    }

    private void UpdateLauncherWindowSize()
    {
        if (DataContext is ViewModels.MainViewModel viewModel &&
            ClientSize.Width > 0 && ClientSize.Height > 0)
        {
            viewModel.UpdateLauncherWindowSize(ClientSize.Width, ClientSize.Height);
        }
    }

    private void ApplyNativeWindowAppearance()
    {
        var platformHandle = TryGetPlatformHandle();
        var nativeWindowHandle = platformHandle is IMacOSTopLevelPlatformHandle macOSHandle
            ? macOSHandle.NSWindow
            : platformHandle?.Handle ?? 0;
        NativeWindowAppearanceService?.TryEnableTransparentBackground(nativeWindowHandle);
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
            subscribedViewModel.MinecraftVersionUpdateAvailable -= MinecraftVersionUpdateAvailable;
            subscribedViewModel.LauncherRestartRequested -= LauncherRestartRequested;
            subscribedViewModel.LaunchingHintRequested -= LaunchingHintRequested;
            subscribedViewModel.PropertyChanged -= ViewModelPropertyChanged;
        }

        subscribedViewModel = viewModel;
        if (subscribedViewModel is not null)
        {
            subscribedViewModel.MicrosoftDeviceCodeAvailable += MicrosoftDeviceCodeAvailable;
            subscribedViewModel.GameProcessStarted += GameProcessStarted;
            subscribedViewModel.GameProcessExited += GameProcessExited;
            subscribedViewModel.MinecraftVersionUpdateAvailable += MinecraftVersionUpdateAvailable;
            subscribedViewModel.LauncherRestartRequested += LauncherRestartRequested;
            subscribedViewModel.LaunchingHintRequested += LaunchingHintRequested;
            subscribedViewModel.PropertyChanged += ViewModelPropertyChanged;
            ApplyInterfacePreferences(subscribedViewModel);
        }
    }

    private void LauncherRestartRequested(object? sender, EventArgs e) =>
        Dispatcher.UIThread.Post(Close);

    private async void LaunchingHintRequested(object? sender, EventArgs e)
    {
        launchingHintCancellation?.Cancel();
        launchingHintCancellation?.Dispose();
        launchingHintCancellation = new CancellationTokenSource();
        var cancellationToken = launchingHintCancellation.Token;
        LaunchingHintText.Text = HomepageHints[Random.Shared.Next(HomepageHints.Length)];
        LaunchingHintOverlay.IsVisible = true;
        LaunchingHintOverlay.Opacity = 1;
        try
        {
            await Task.Delay(3200, cancellationToken);
            LaunchingHintOverlay.Opacity = 0;
            await Task.Delay(220, cancellationToken);
            LaunchingHintOverlay.IsVisible = false;
        }
        catch (OperationCanceledException)
        {
        }
    }

    private async Task CompleteStartupLogoAsync(bool showStartupLogo)
    {
        if (!showStartupLogo)
        {
            StartupLogoOverlay.IsVisible = false;
            MainShellBorder.Opacity = 1;
            return;
        }
        await Task.Delay(420);
        MainShellBorder.Opacity = 1;
        StartupLogoOverlay.Opacity = 0;
        await Task.Delay(260);
        StartupLogoOverlay.IsVisible = false;
    }

    private async void MinecraftVersionUpdateAvailable(object? sender, MinecraftVersionCatalogEntry version)
    {
        var category = MinecraftVersionCatalogFilter.GetCategory(version);
        await ShowMessageAsync(
            category == MinecraftVersionCatalogCategory.Release ? "Minecraft 正式版更新" : "Minecraft 快照版更新",
            $"发现新版本 {version.Id}，可在下载页面查看并安装。运行时间：{version.ReleaseTime.ToLocalTime():yyyy-MM-dd HH:mm}");
    }

    private async void MainWindowActivated(object? sender, EventArgs e)
    {
        if (isResolvingClipboardResource ||
            DataContext is not ViewModels.MainViewModel { ReadClipboard: true } viewModel ||
            TopLevel.GetTopLevel(this)?.Clipboard is not { } clipboard)
        {
            return;
        }

        string? text;
        try
        {
            text = (await clipboard.TryGetTextAsync())?.Trim();
        }
        catch
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(text) ||
            string.Equals(text, lastProcessedClipboardText, StringComparison.Ordinal) ||
            !TryGetCommunityResourceUri(text, out var uri))
        {
            return;
        }

        lastProcessedClipboardText = text;
        isResolvingClipboardResource = true;
        try
        {
            if (!await ShowConfirmationAsync(
                    "检测到社区资源链接",
                    $"是否在 PCL Aurora 中打开这个社区资源？\n{uri.AbsoluteUri}",
                    isWarning: false))
            {
                return;
            }

            var item = await viewModel.ResolveCommunityResourceLinkAsync(uri);
            if (item is null)
            {
                await ShowMessageAsync("无法识别社区资源", "未能从该链接找到可用项目。", isWarning: true);
                return;
            }

            currentDownloadSection = item.Project.Type switch
            {
                CommunityResourceType.Mod => "mod",
                CommunityResourceType.ModPack => "pack",
                CommunityResourceType.DataPack => "datapack",
                CommunityResourceType.ResourcePack => "resourcepack",
                CommunityResourceType.Shader => "shader",
                CommunityResourceType.World => "world",
                _ => "mod",
            };
            viewModel.SetCommunityResourceSection(currentDownloadSection);
            await SelectMainPageAsync(1);
            await ShowCommunityResourceDetailAsync(viewModel, item);
        }
        catch (Exception exception)
        {
            await ShowMessageAsync("打开社区资源失败", exception.Message, isWarning: true);
        }
        finally
        {
            isResolvingClipboardResource = false;
        }
    }

    private static bool TryGetCommunityResourceUri(string text, out Uri uri)
    {
        uri = null!;
        if (!Uri.TryCreate(text, UriKind.Absolute, out var candidate) ||
            candidate.Scheme != Uri.UriSchemeHttps)
        {
            return false;
        }

        var host = candidate.Host.Trim().ToLowerInvariant();
        if (host is not ("modrinth.com" or "www.modrinth.com" or "curseforge.com" or "www.curseforge.com"))
        {
            return false;
        }

        uri = candidate;
        return true;
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
            or nameof(ViewModels.MainViewModel.EnableAdvancedMaterial)
            or nameof(ViewModels.MainViewModel.InterfaceBlurRadius)
            or nameof(ViewModels.MainViewModel.InterfaceBlurSamplingRate)
            or nameof(ViewModels.MainViewModel.InterfaceBlurKernelIndex)
            or nameof(ViewModels.MainViewModel.MotdInterfaceFont)
            or nameof(ViewModels.MainViewModel.UseColorfulBackground)
            or nameof(ViewModels.MainViewModel.TitleLeftAligned)
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

        if (e.PropertyName is nameof(ViewModels.MainViewModel.HomepageTypeIndex)
            or nameof(ViewModels.MainViewModel.HomepagePresetIndex)
            or nameof(ViewModels.MainViewModel.HomepageUrl))
        {
            QueueHomepageRefresh();
        }
    }

    private void QueueHomepageRefresh()
    {
        homepageRefreshCancellation?.Cancel();
        homepageRefreshCancellation?.Dispose();
        homepageRefreshCancellation = new CancellationTokenSource();
        _ = RefreshHomepageAfterDelayAsync(homepageRefreshCancellation.Token);
    }

    private async Task RefreshHomepageAfterDelayAsync(CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(450, cancellationToken);
            await RefreshHomepageAsync(showSuccess: false, cancellationToken);
        }
        catch (OperationCanceledException)
        {
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
        LauncherBackgroundHost.Opacity = viewModel.InterfaceBackgroundOpacity / 1000d;
        var advancedBlur = viewModel.EnableAdvancedMaterial
            ? viewModel.InterfaceBlurRadius * (viewModel.InterfaceBlurSamplingRate / 100d) *
              (viewModel.InterfaceBlurKernelIndex == (int)LauncherBlurKernel.Box ? 0.75d : 1d)
            : 0;
        var effectiveBlur = Math.Max(viewModel.InterfaceBackgroundBlurRadius, advancedBlur);
        LauncherBackgroundHost.Effect = effectiveBlur > 0
            ? new ImmutableBlurEffect(effectiveBlur)
            : null;
        TransparencyLevelHint = viewModel.EnableAdvancedMaterial
            ? [WindowTransparencyLevel.AcrylicBlur, WindowTransparencyLevel.Blur, WindowTransparencyLevel.Transparent]
            : [WindowTransparencyLevel.Transparent];
        ApplyNativeWindowAppearance();
        LauncherColorOverlay.IsVisible = viewModel.UseColorfulBackground;
        PclHomepageView.FontFamily = string.IsNullOrWhiteSpace(viewModel.MotdInterfaceFont)
            ? FontFamily
            : new FontFamily(viewModel.MotdInterfaceFont.Trim());
        var alignNavigationLeft = viewModel.TitleContentTypeIndex == (int)LauncherTitleContentType.None &&
                                  viewModel.TitleLeftAligned;
        MainTitleBarGrid.ColumnDefinitions = new ColumnDefinitions(alignNavigationLeft ? "0,Auto,*" : "*,Auto,*");
        ApplyBackgroundLayout(viewModel);
        ApplyThemeColor(viewModel);
        ApplyFeatureVisibility(viewModel);
    }

    private void ApplyBackgroundLayout(ViewModels.MainViewModel viewModel)
    {
        var hasBackground = launcherBackgroundBitmap is not null;
        LauncherBackgroundImage.IsVisible = hasBackground;
        LauncherBackgroundTile.IsVisible = false;
        LauncherBackgroundImage.HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch;
        LauncherBackgroundImage.VerticalAlignment = Avalonia.Layout.VerticalAlignment.Stretch;
        LauncherBackgroundImage.Stretch = Stretch.UniformToFill;
        LauncherBackgroundTile.Background = null;
        if (!hasBackground) return;

        var mode = (LauncherBackgroundSuitMode)viewModel.BackgroundSuitIndex;
        if (mode == LauncherBackgroundSuitMode.Tile)
        {
            var pixelSize = launcherBackgroundBitmap!.PixelSize;
            LauncherBackgroundImage.IsVisible = false;
            LauncherBackgroundTile.IsVisible = true;
            LauncherBackgroundTile.Background = new ImageBrush
            {
                Source = launcherBackgroundBitmap,
                Stretch = Stretch.None,
                TileMode = TileMode.Tile,
                SourceRect = new RelativeRect(0, 0, 1, 1, RelativeUnit.Relative),
                DestinationRect = new RelativeRect(0, 0, pixelSize.Width, pixelSize.Height, RelativeUnit.Absolute),
            };
            return;
        }

        switch (mode)
        {
            case LauncherBackgroundSuitMode.Center:
                SetBackgroundPlacement(Avalonia.Layout.HorizontalAlignment.Center, Avalonia.Layout.VerticalAlignment.Center);
                break;
            case LauncherBackgroundSuitMode.Fit:
                LauncherBackgroundImage.Stretch = Stretch.UniformToFill;
                break;
            case LauncherBackgroundSuitMode.Stretch:
                LauncherBackgroundImage.Stretch = Stretch.Fill;
                break;
            case LauncherBackgroundSuitMode.TopLeft:
                SetBackgroundPlacement(Avalonia.Layout.HorizontalAlignment.Left, Avalonia.Layout.VerticalAlignment.Top);
                break;
            case LauncherBackgroundSuitMode.TopRight:
                SetBackgroundPlacement(Avalonia.Layout.HorizontalAlignment.Right, Avalonia.Layout.VerticalAlignment.Top);
                break;
            case LauncherBackgroundSuitMode.BottomLeft:
                SetBackgroundPlacement(Avalonia.Layout.HorizontalAlignment.Left, Avalonia.Layout.VerticalAlignment.Bottom);
                break;
            case LauncherBackgroundSuitMode.BottomRight:
                SetBackgroundPlacement(Avalonia.Layout.HorizontalAlignment.Right, Avalonia.Layout.VerticalAlignment.Bottom);
                break;
            case LauncherBackgroundSuitMode.Smart:
                var pixelSize = launcherBackgroundBitmap!.PixelSize;
                if (pixelSize.Width < ClientSize.Width / 2d && pixelSize.Height < ClientSize.Height / 2d)
                {
                    LauncherBackgroundImage.IsVisible = false;
                    LauncherBackgroundTile.IsVisible = true;
                    LauncherBackgroundTile.Background = new ImageBrush
                    {
                        Source = launcherBackgroundBitmap,
                        Stretch = Stretch.None,
                        TileMode = TileMode.Tile,
                        SourceRect = new RelativeRect(0, 0, 1, 1, RelativeUnit.Relative),
                        DestinationRect = new RelativeRect(0, 0, pixelSize.Width, pixelSize.Height, RelativeUnit.Absolute),
                    };
                }
                else
                {
                    LauncherBackgroundImage.Stretch = Stretch.UniformToFill;
                }
                break;
            default:
                LauncherBackgroundImage.Stretch = Stretch.UniformToFill;
                break;
        }
    }

    private void SetBackgroundPlacement(
        Avalonia.Layout.HorizontalAlignment horizontal,
        Avalonia.Layout.VerticalAlignment vertical)
    {
        LauncherBackgroundImage.Stretch = Stretch.None;
        LauncherBackgroundImage.HorizontalAlignment = horizontal;
        LauncherBackgroundImage.VerticalAlignment = vertical;
    }

    private void ApplyThemeColor(ViewModels.MainViewModel viewModel)
    {
        var isDark = viewModel.SelectedThemeMode.Mode == LauncherThemeMode.Dark ||
                     (viewModel.SelectedThemeMode.Mode == LauncherThemeMode.System &&
                      Avalonia.Application.Current?.ActualThemeVariant == Avalonia.Styling.ThemeVariant.Dark);
        ApplySemanticPalette(isDark, launcherBackgroundBitmap is not null);
        var theme = (LauncherColorTheme)(isDark ? viewModel.DarkThemeColorIndex : viewModel.LightThemeColorIndex);
        var colors = theme switch
        {
            LauncherColorTheme.SkyBlue => (Primary: "#1787D4", Start: "#1069B8", Middle: "#1787D4", End: "#1069B8"),
            LauncherColorTheme.CrashBlue => (Primary: "#5B59C9", Start: "#4544A8", Middle: "#5B59C9", End: "#4544A8"),
            _ => (Primary: "#1370F3", Start: "#106AC4", Middle: "#1377DD", End: "#106AC5"),
        };

        if (Avalonia.Application.Current?.Resources["PclThemePrimaryBrush"] is Avalonia.Media.SolidColorBrush primary)
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

    private void ApplySemanticPalette(bool isDark, bool hasBackgroundImage)
    {
        var colors = isDark
            ? new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["PclPageBackgroundBrush"] = hasBackgroundImage ? "#C024282E" : "#24282E",
                ["PclTextPrimaryBrush"] = "#EEF2F7",
                ["PclTextSecondaryBrush"] = "#B6C0CB",
                ["PclTextMutedBrush"] = "#8994A1",
                ["PclSurfaceBrush"] = "#292E35",
                ["PclSurfaceRaisedBrush"] = "#30363E",
                ["PclCardBrush"] = "#E62B3037",
                ["PclCardHoverBrush"] = "#34404E",
                ["PclControlBackgroundBrush"] = "#30363E",
                ["PclControlHoverBrush"] = "#394655",
                ["PclControlPressedBrush"] = "#273F58",
                ["PclControlSelectedBrush"] = "#334F6D",
                ["PclControlSelectedHoverBrush"] = "#3B5E82",
                ["PclControlBorderBrush"] = "#556577",
                ["PclControlAccentBorderBrush"] = "#6F9FD5",
                ["PclDividerBrush"] = "#424B56",
                ["PclDisabledSurfaceBrush"] = "#292D32",
                ["PclDisabledTextBrush"] = "#6E7782",
                ["PclHintBackgroundBrush"] = "#26384B",
                ["PclHintBorderBrush"] = "#426787",
                ["PclWarningBackgroundBrush"] = "#4A4020",
                ["PclWarningBorderBrush"] = "#8E7837",
                ["PclWarningTextBrush"] = "#E6CF84",
                ["PclDangerBackgroundBrush"] = "#4A292C",
                ["PclDangerTextBrush"] = "#FF9A9A",
                ["PclSuccessBackgroundBrush"] = "#244333",
                ["PclSuccessTextBrush"] = "#86D8A8",
                ["PclOverlaySurfaceBrush"] = "#F230363E",
            }
            : new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["PclPageBackgroundBrush"] = hasBackgroundImage ? "#B8D9EAF9" : "#D9EAF9",
                ["PclTextPrimaryBrush"] = "#343D4A",
                ["PclTextSecondaryBrush"] = "#6F7C8B",
                ["PclTextMutedBrush"] = "#87929D",
                ["PclSurfaceBrush"] = "#FCFDFF",
                ["PclSurfaceRaisedBrush"] = "#FBFBFB",
                ["PclCardBrush"] = "#D2FBFBFB",
                ["PclCardHoverBrush"] = "#EAF2FE",
                ["PclControlBackgroundBrush"] = "#FFFFFF",
                ["PclControlHoverBrush"] = "#F8FBFE",
                ["PclControlPressedBrush"] = "#F2F8FF",
                ["PclControlSelectedBrush"] = "#E4F1FF",
                ["PclControlSelectedHoverBrush"] = "#DCEEFF",
                ["PclControlBorderBrush"] = "#C3D2E2",
                ["PclControlAccentBorderBrush"] = "#83B8F8",
                ["PclDividerBrush"] = "#D1DDEA",
                ["PclDisabledSurfaceBrush"] = "#F4F6F8",
                ["PclDisabledTextBrush"] = "#9AA5B1",
                ["PclHintBackgroundBrush"] = "#EAF4FF",
                ["PclHintBorderBrush"] = "#BFD9F3",
                ["PclWarningBackgroundBrush"] = "#FFF5D6",
                ["PclWarningBorderBrush"] = "#E8D69A",
                ["PclWarningTextBrush"] = "#765B00",
                ["PclDangerBackgroundBrush"] = "#80FBDDDD",
                ["PclDangerTextBrush"] = "#A44343",
                ["PclSuccessBackgroundBrush"] = "#E8F7EE",
                ["PclSuccessTextBrush"] = "#287A49",
                ["PclOverlaySurfaceBrush"] = "#F2FFFFFF",
            };

        foreach (var (key, value) in colors)
        {
            if (Avalonia.Application.Current?.Resources[key] is SolidColorBrush brush)
            {
                brush.Color = Color.Parse(value);
            }
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

    private async void AddJavaClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not ViewModels.MainViewModel viewModel ||
            TopLevel.GetTopLevel(this)?.StorageProvider is not { } storageProvider)
        {
            return;
        }

        var files = await storageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "选择 Java 程序",
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType("Java 程序")
                {
                    Patterns = OperatingSystem.IsWindows()
                        ? ["java.exe", "javaw.exe"]
                        : ["java"],
                },
                FilePickerFileTypes.All,
            ],
        });
        var executablePath = files.FirstOrDefault()?.TryGetLocalPath();
        if (string.IsNullOrWhiteSpace(executablePath))
        {
            return;
        }

        try
        {
            var installation = await viewModel.AddManualJavaAsync(executablePath);
            await ShowMessageAsync(
                "添加 Java",
                $"已添加 Java {installation.Version ?? "未知版本"}（{installation.Architecture}）。");
        }
        catch (Exception exception)
        {
            await ShowMessageAsync("添加 Java 失败", exception.Message, isWarning: true);
        }
    }

    private async void RefreshHomepageClick(object? sender, RoutedEventArgs e)
    {
        homepageRefreshCancellation?.Cancel();
        await RefreshHomepageAsync(showSuccess: true);
    }

    private async void HomepageTutorialClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is ViewModels.MainViewModel viewModel)
        {
            await viewModel.OpenExternalUriAsync(HomepageTutorialUri);
        }
    }

    private async void MainNavigationClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string value } || !int.TryParse(value, out var page))
        {
            return;
        }

        if (page != 3)
        {
            PclHelpView.ResetToHome();
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

        PclHelpView.ResetToHome();

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

    private async void MoreSectionRefreshClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string section } || section != "help")
        {
            return;
        }

        PclHelpView.ResetToHome();
        await PclHelpView.ReloadAsync();
        MoreContentScroller.Offset = default;
    }

    private async void ClearToolboxCacheClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not ViewModels.MainViewModel viewModel)
        {
            return;
        }

        try
        {
            IStorageFolder? suggestedStartLocation = null;
            if (Directory.Exists(viewModel.MinecraftRootDirectory))
            {
                suggestedStartLocation = await StorageProvider.TryGetFolderFromPathAsync(
                    new Uri(viewModel.MinecraftRootDirectory));
            }

            var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
            {
                Title = "选择要清理的 Minecraft 文件夹",
                AllowMultiple = false,
                SuggestedStartLocation = suggestedStartLocation,
            });
            var root = folders.FirstOrDefault()?.TryGetLocalPath();
            if (string.IsNullOrWhiteSpace(root))
            {
                return;
            }

            var plan = await viewModel.ScanToolboxGameJunkAsync(root);
            if (plan.IsEmpty)
            {
                await ShowMessageAsync("清理游戏垃圾", viewModel.ToolboxStatusText);
                return;
            }

            if (!await ShowConfirmationAsync(
                    "清理游戏垃圾",
                    $"将从所选 Minecraft 文件夹清理 {plan.FileCount} 个日志、崩溃报告和可再生成临时文件，共 {FormatToolboxBytes(plan.TotalSize)}。不会删除存档、模组、资源包或设置。是否继续？"))
            {
                return;
            }

            await viewModel.CleanToolboxGameJunkAsync(plan);
            await ShowMessageAsync("清理游戏垃圾", viewModel.ToolboxStatusText);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidOperationException or ArgumentException)
        {
            await ShowMessageAsync("清理游戏垃圾", $"无法清理所选目录：{exception.Message}", isWarning: true);
        }
    }

    private async void ToolboxLuckClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is ViewModels.MainViewModel viewModel)
        {
            viewModel.ShowToolboxLuckCommand.Execute(null);
            await ShowMessageAsync("今日人品", viewModel.ToolboxStatusText);
        }
    }

    private async void ToolboxMemoryOptimizationClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is ViewModels.MainViewModel viewModel &&
            await ShowConfirmationAsync(
                "内存优化",
                "将清理整个 macOS 系统的文件缓存，并附带回收 PCL Aurora 内存。操作可能导致短时卡顿，系统也可能要求管理员授权，但不会关闭其他应用。是否继续？"))
        {
            await viewModel.OptimizeToolboxMemoryCommand.ExecuteAsync(null);
            await ShowMessageAsync("内存优化", viewModel.ToolboxStatusText);
        }
    }

    private static string FormatToolboxBytes(long value) => value switch
    {
        < 1024 => $"{value} B",
        < 1024L * 1024 => $"{value / 1024d:0.#} KiB",
        < 1024L * 1024 * 1024 => $"{value / 1024d / 1024d:0.#} MiB",
        _ => $"{value / 1024d / 1024d / 1024d:0.#} GiB",
    };

    private async void ToolboxDontClick(object? sender, RoutedEventArgs e)
    {
        var firstConfirmation = await MessageDialogHost.ShowAsync(new PclMessageDialogOptions(
            Title: "千万别点",
            Message: "你真的要继续吗？现在返回还来得及。",
            PrimaryButtonText: "继续",
            SecondaryButtonText: "取消",
            IsWarning: true,
            EnterConfirms: false));
        if (firstConfirmation != 1)
        {
            return;
        }

        var finalConfirmation = await MessageDialogHost.ShowAsync(new PclMessageDialogOptions(
            Title: "最后确认",
            Message: "这是最后一次确认。若仍执意继续，接下来发生的一切及其后果由你自行承担。",
            PrimaryButtonText: "我执意继续",
            SecondaryButtonText: "返回",
            IsWarning: true,
            EnterConfirms: false));
        if (finalConfirmation != 1)
        {
            return;
        }

        var downloadDirectory = await GetSystemDownloadsDirectoryAsync();
        if (string.IsNullOrWhiteSpace(downloadDirectory))
        {
            ToolboxDownloadStatusTextBlock.Text = "彩蛋触发失败：无法定位系统下载文件夹。";
            await ShowMessageAsync("彩蛋触发失败", ToolboxDownloadStatusTextBlock.Text, isWarning: true);
            return;
        }

        var destinationPath = GetAvailableDownloadPath(downloadDirectory, DontClickDownloadFileName);
        var outcome = await DownloadToolboxFileAsync(
            DontClickDownloadUri,
            destinationPath,
            "彩蛋内容",
            overwriteExisting: false);
        if (outcome == ToolboxDownloadOutcome.Succeeded)
        {
            ToolboxDownloadStatusTextBlock.Text = "彩蛋已触发。";
            await ShowMessageAsync("千万别点", "骗你的，啥也没有");
        }
        else if (outcome == ToolboxDownloadOutcome.Failed)
        {
            await ShowMessageAsync("彩蛋触发失败", ToolboxDownloadStatusTextBlock.Text ?? "彩蛋触发失败。", isWarning: true);
        }
    }

    private async Task<string?> GetSystemDownloadsDirectoryAsync()
    {
        var downloadsFolder = await StorageProvider.TryGetWellKnownFolderAsync(WellKnownFolder.Downloads);
        var downloadsPath = downloadsFolder?.TryGetLocalPath();
        if (!string.IsNullOrWhiteSpace(downloadsPath))
        {
            return downloadsPath;
        }

        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return string.IsNullOrWhiteSpace(userProfile)
            ? null
            : Path.Combine(userProfile, "Downloads");
    }

    private static string GetAvailableDownloadPath(string directory, string fileName)
    {
        var destinationPath = Path.Combine(directory, fileName);
        if (!File.Exists(destinationPath))
        {
            return destinationPath;
        }

        var name = Path.GetFileNameWithoutExtension(fileName);
        var extension = Path.GetExtension(fileName);
        for (var suffix = 1; suffix < int.MaxValue; suffix++)
        {
            destinationPath = Path.Combine(directory, $"{name} ({suffix}){extension}");
            if (!File.Exists(destinationPath))
            {
                return destinationPath;
            }
        }

        throw new IOException("系统下载文件夹中没有可用的文件名。");
    }

    private void ToolboxDownloadUrlChanged(object? sender, TextChangedEventArgs e)
    {
        if (ToolboxDownloadNameTextBox.Text?.Length > 0 ||
            !Uri.TryCreate(ToolboxDownloadUrlTextBox.Text, UriKind.Absolute, out var uri))
        {
            return;
        }

        var suggestedName = Path.GetFileName(Uri.UnescapeDataString(uri.AbsolutePath));
        if (IsSafeFileName(suggestedName))
        {
            ToolboxDownloadNameTextBox.Text = suggestedName;
        }
    }

    private async void ToolboxDownloadFolderClick(object? sender, RoutedEventArgs e) =>
        await ChooseToolboxDownloadFolderAsync();

    private async Task<string?> ChooseToolboxDownloadFolderAsync()
    {
        var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "选择下载文件夹",
            AllowMultiple = false,
        });
        var path = folders.SingleOrDefault()?.TryGetLocalPath();
        if (!string.IsNullOrWhiteSpace(path))
        {
            ToolboxDownloadFolderTextBox.Text = path;
        }

        return path;
    }

    private async void ToolboxDownloadStartClick(object? sender, RoutedEventArgs e)
    {
        if (!Uri.TryCreate(ToolboxDownloadUrlTextBox.Text?.Trim(), UriKind.Absolute, out var uri) ||
            uri.Scheme != Uri.UriSchemeHttps)
        {
            await ShowMessageAsync("下载自定义文件", "请输入有效的 HTTPS 下载地址。", isWarning: true);
            return;
        }

        var fileName = ToolboxDownloadNameTextBox.Text?.Trim();
        if (!IsSafeFileName(fileName))
        {
            await ShowMessageAsync("下载自定义文件", "请输入不含路径和非法字符的文件名。", isWarning: true);
            return;
        }

        var folder = ToolboxDownloadFolderTextBox.Text;
        if (string.IsNullOrWhiteSpace(folder))
        {
            folder = await ChooseToolboxDownloadFolderAsync();
        }
        if (string.IsNullOrWhiteSpace(folder))
        {
            return;
        }

        var destinationPath = Path.Combine(folder, fileName!);
        await DownloadToolboxFileAsync(uri, destinationPath, fileName!);
    }

    private async Task<ToolboxDownloadOutcome> DownloadToolboxFileAsync(
        Uri uri,
        string destinationPath,
        string displayName,
        bool overwriteExisting = true)
    {
        var destinationDirectory = Path.GetDirectoryName(destinationPath);
        if (string.IsNullOrWhiteSpace(destinationDirectory))
        {
            ToolboxDownloadStatusTextBlock.Text = "下载失败：保存位置无效。";
            return ToolboxDownloadOutcome.Failed;
        }

        var temporaryPath = Path.Combine(
            destinationDirectory,
            $".{Path.GetFileName(destinationPath)}.{Guid.NewGuid():N}.partial");
        toolboxDownloadCancellation?.Cancel();
        toolboxDownloadCancellation?.Dispose();
        toolboxDownloadCancellation = new CancellationTokenSource();
        try
        {
            Directory.CreateDirectory(destinationDirectory);
            ToolboxDownloadStatusTextBlock.Text = $"正在下载 {displayName}…";
            using var response = await toolboxHttpClient.GetAsync(
                uri,
                HttpCompletionOption.ResponseHeadersRead,
                toolboxDownloadCancellation.Token);
            response.EnsureSuccessStatusCode();
            await using (var source = await response.Content.ReadAsStreamAsync(toolboxDownloadCancellation.Token))
            await using (var target = new FileStream(
                             temporaryPath,
                             FileMode.CreateNew,
                             FileAccess.Write,
                             FileShare.None,
                             81920,
                             useAsync: true))
            {
                await source.CopyToAsync(target, toolboxDownloadCancellation.Token);
                await target.FlushAsync(toolboxDownloadCancellation.Token);
            }
            File.Move(temporaryPath, destinationPath, overwrite: overwriteExisting);
            ToolboxDownloadStatusTextBlock.Text = $"已下载到 {destinationPath}";
            return ToolboxDownloadOutcome.Succeeded;
        }
        catch (OperationCanceledException)
        {
            ToolboxDownloadStatusTextBlock.Text = "下载已取消。";
            return ToolboxDownloadOutcome.Canceled;
        }
        catch (Exception exception) when (exception is HttpRequestException or IOException or UnauthorizedAccessException)
        {
            ToolboxDownloadStatusTextBlock.Text = $"下载失败：{exception.Message}";
            return ToolboxDownloadOutcome.Failed;
        }
        finally
        {
            try
            {
                File.Delete(temporaryPath);
            }
            catch (IOException)
            {
            }
        }
    }

    private enum ToolboxDownloadOutcome
    {
        Succeeded,
        Canceled,
        Failed,
    }

    private async void ToolboxDownloadOpenClick(object? sender, RoutedEventArgs e)
    {
        var folder = ToolboxDownloadFolderTextBox.Text;
        if (DataContext is not ViewModels.MainViewModel viewModel ||
            string.IsNullOrWhiteSpace(folder))
        {
            folder = await ChooseToolboxDownloadFolderAsync();
        }
        if (DataContext is ViewModels.MainViewModel model && !string.IsNullOrWhiteSpace(folder))
        {
            Directory.CreateDirectory(folder);
            await model.OpenToolboxFolderAsync(folder);
        }
    }

    private async void ToolboxSkinDownloadClick(object? sender, RoutedEventArgs e)
    {
        var playerName = ToolboxSkinNameTextBox.Text?.Trim();
        if (string.IsNullOrWhiteSpace(playerName) ||
            playerName.Length is < 3 or > 16 ||
            playerName.Any(character => !char.IsAsciiLetterOrDigit(character) && character != '_'))
        {
            await ShowMessageAsync("皮肤下载", "请输入有效的 Minecraft 玩家名。", isWarning: true);
            return;
        }

        try
        {
            ToolboxSkinStatusTextBlock.Text = $"正在获取 {playerName} 的皮肤…";
            using var profileResponse = await toolboxHttpClient.GetAsync(
                $"https://api.mojang.com/users/profiles/minecraft/{Uri.EscapeDataString(playerName)}");
            if (!profileResponse.IsSuccessStatusCode)
            {
                throw new InvalidDataException("未找到该正版玩家。");
            }
            using var profile = JsonDocument.Parse(await profileResponse.Content.ReadAsStreamAsync());
            var profileId = profile.RootElement.GetProperty("id").GetString();
            if (string.IsNullOrWhiteSpace(profileId))
            {
                throw new InvalidDataException("玩家资料未返回 UUID。");
            }

            using var sessionResponse = await toolboxHttpClient.GetAsync(
                $"https://sessionserver.mojang.com/session/minecraft/profile/{profileId}");
            sessionResponse.EnsureSuccessStatusCode();
            using var session = JsonDocument.Parse(await sessionResponse.Content.ReadAsStreamAsync());
            var textureValue = session.RootElement.GetProperty("properties")[0].GetProperty("value").GetString();
            using var texture = JsonDocument.Parse(Convert.FromBase64String(textureValue ?? string.Empty));
            var skinUrl = texture.RootElement.GetProperty("textures").GetProperty("SKIN").GetProperty("url").GetString();
            if (!Uri.TryCreate(skinUrl, UriKind.Absolute, out var skinUri) || skinUri.Scheme != Uri.UriSchemeHttps)
            {
                throw new InvalidDataException("玩家资料中没有可用的 HTTPS 皮肤地址。");
            }

            var pngType = CreatePngFileType();
            var destination = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = $"保存 {playerName} 的皮肤",
                SuggestedFileName = $"{playerName}.png",
                DefaultExtension = "png",
                FileTypeChoices = [pngType],
                SuggestedFileType = pngType,
                ShowOverwritePrompt = true,
            });
            if (destination is null)
            {
                ToolboxSkinStatusTextBlock.Text = "已取消保存。";
                return;
            }

            var bytes = await toolboxHttpClient.GetByteArrayAsync(skinUri);
            await using var output = await destination.OpenWriteAsync();
            output.SetLength(0);
            await output.WriteAsync(bytes);
            ToolboxSkinStatusTextBlock.Text = "皮肤已保存。";
        }
        catch (Exception exception) when (exception is HttpRequestException or JsonException or FormatException or InvalidDataException or IOException)
        {
            ToolboxSkinStatusTextBlock.Text = $"皮肤获取失败：{exception.Message}";
        }
    }

    private async void ToolboxServerQueryClick(object? sender, RoutedEventArgs e)
    {
        if (!TryParseServerEndpoint(ToolboxServerAddressTextBox.Text, out var host, out var port))
        {
            await ShowMessageAsync("服务器查询", "请输入有效的服务器地址，可在末尾附加端口。", isWarning: true);
            return;
        }

        ToolboxServerStatusTextBlock.Text = "正在查询服务器…";
        try
        {
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            var statusJson = await QueryMinecraftServerAsync(host, port);
            stopwatch.Stop();
            using var status = JsonDocument.Parse(statusJson);
            var root = status.RootElement;
            var version = root.TryGetProperty("version", out var versionElement) &&
                          versionElement.TryGetProperty("name", out var versionName)
                ? versionName.GetString() ?? "未知版本"
                : "未知版本";
            var online = root.TryGetProperty("players", out var players) &&
                         players.TryGetProperty("online", out var onlineElement)
                ? onlineElement.GetInt32()
                : 0;
            var maximum = players.ValueKind == JsonValueKind.Object &&
                          players.TryGetProperty("max", out var maxElement)
                ? maxElement.GetInt32()
                : 0;
            var description = root.TryGetProperty("description", out var descriptionElement)
                ? FlattenMinecraftText(descriptionElement)
                : string.Empty;
            ToolboxServerStatusTextBlock.Text =
                $"{version} · {online}/{maximum} 人 · {stopwatch.ElapsedMilliseconds} ms" +
                (string.IsNullOrWhiteSpace(description) ? string.Empty : $"\n{description}");
        }
        catch (Exception exception) when (exception is SocketException or IOException or JsonException or TimeoutException or OperationCanceledException)
        {
            ToolboxServerStatusTextBlock.Text = $"查询失败：{exception.Message}";
        }
    }

    private void ToolboxAchievementPreviewClick(object? sender, RoutedEventArgs e) =>
        UpdateToolboxAchievementPreview();

    private async void ToolboxAchievementSaveClick(object? sender, RoutedEventArgs e)
    {
        UpdateToolboxAchievementPreview();
        await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Render);
        var pngType = CreatePngFileType();
        var destination = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "保存成就图片",
            SuggestedFileName = $"{SanitizeSuggestedName(ToolboxAchievementIdTextBox.Text, "achievement")}.png",
            DefaultExtension = "png",
            FileTypeChoices = [pngType],
            SuggestedFileType = pngType,
            ShowOverwritePrompt = true,
        });
        if (destination is null)
        {
            return;
        }

        var size = new PixelSize(
            Math.Max(1, (int)Math.Ceiling(ToolboxAchievementPreviewBorder.Bounds.Width)),
            Math.Max(1, (int)Math.Ceiling(ToolboxAchievementPreviewBorder.Bounds.Height)));
        using var bitmap = new RenderTargetBitmap(size, new Vector(96, 96));
        bitmap.Render(ToolboxAchievementPreviewBorder);
        await using var output = await destination.OpenWriteAsync();
        output.SetLength(0);
        bitmap.Save(output, PngBitmapEncoderOptions.Default);
    }

    private void UpdateToolboxAchievementPreview()
    {
        ToolboxAchievementPreviewTitle.Text = string.IsNullOrWhiteSpace(ToolboxAchievementTitleTextBox.Text)
            ? "新的成就"
            : ToolboxAchievementTitleTextBox.Text.Trim();
        ToolboxAchievementPreviewDescription.Text = string.IsNullOrWhiteSpace(ToolboxAchievementDescriptionTextBox.Text)
            ? "PCL Aurora 百宝箱"
            : ToolboxAchievementDescriptionTextBox.Text.Trim() +
              (string.IsNullOrWhiteSpace(ToolboxAchievementLine2TextBox.Text)
                  ? string.Empty
                  : $" · {ToolboxAchievementLine2TextBox.Text.Trim()}");
        ToolboxAchievementPreviewBorder.IsVisible = true;
    }

    private async void ToolboxAvatarSelectClick(object? sender, RoutedEventArgs e)
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "选择 Minecraft 皮肤",
            AllowMultiple = false,
            FileTypeFilter = [CreatePngFileType()],
        });
        var source = files.SingleOrDefault();
        if (source is null)
        {
            return;
        }

        try
        {
            await using var stream = await source.OpenReadAsync();
            var bitmap = new Bitmap(stream);
            if (bitmap.PixelSize.Width < 64 || bitmap.PixelSize.Height < 32 || bitmap.PixelSize.Width % 64 != 0)
            {
                bitmap.Dispose();
                throw new InvalidDataException("皮肤图片尺寸必须为 64×32、64×64 或其整数倍。");
            }

            toolboxAvatarBitmap?.Dispose();
            toolboxAvatarBitmap = bitmap;
            var scale = bitmap.PixelSize.Width / 64;
            ToolboxAvatarFaceImage.Source = new CroppedBitmap(bitmap, new PixelRect(8 * scale, 8 * scale, 8 * scale, 8 * scale));
            ToolboxAvatarOverlayImage.Source = new CroppedBitmap(bitmap, new PixelRect(40 * scale, 8 * scale, 8 * scale, 8 * scale));
            ToolboxAvatarPreviewGrid.IsVisible = true;
            ToolboxAvatarStatusTextBlock.Text = "已载入皮肤，可保存头像。";
        }
        catch (Exception exception) when (exception is IOException or InvalidDataException or ArgumentException)
        {
            ToolboxAvatarStatusTextBlock.Text = $"无法读取皮肤：{exception.Message}";
        }
    }

    private async void ToolboxAvatarSaveClick(object? sender, RoutedEventArgs e)
    {
        if (toolboxAvatarBitmap is null || !ToolboxAvatarPreviewGrid.IsVisible)
        {
            await ShowMessageAsync("头像生成器", "请先选择一张 Minecraft 皮肤。", isWarning: true);
            return;
        }

        var size = ToolboxAvatarSizeComboBox.SelectedIndex switch
        {
            1 => 96,
            2 => 128,
            _ => 64,
        };
        var pngType = CreatePngFileType();
        var destination = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "保存 Minecraft 头像",
            SuggestedFileName = $"minecraft-avatar-{size}.png",
            DefaultExtension = "png",
            FileTypeChoices = [pngType],
            SuggestedFileType = pngType,
            ShowOverwritePrompt = true,
        });
        if (destination is null)
        {
            return;
        }

        var oldWidth = ToolboxAvatarPreviewGrid.Width;
        var oldHeight = ToolboxAvatarPreviewGrid.Height;
        ToolboxAvatarPreviewGrid.Width = size;
        ToolboxAvatarPreviewGrid.Height = size;
        await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Render);
        using var bitmap = new RenderTargetBitmap(new PixelSize(size, size), new Vector(96, 96));
        bitmap.Render(ToolboxAvatarPreviewGrid);
        await using var output = await destination.OpenWriteAsync();
        output.SetLength(0);
        bitmap.Save(output, PngBitmapEncoderOptions.Default);
        ToolboxAvatarPreviewGrid.Width = oldWidth;
        ToolboxAvatarPreviewGrid.Height = oldHeight;
        ToolboxAvatarStatusTextBlock.Text = $"已保存 {size}×{size} 头像。";
    }

    private static bool IsSafeFileName(string? fileName) =>
        !string.IsNullOrWhiteSpace(fileName) &&
        fileName is not "." and not ".." &&
        string.Equals(fileName, Path.GetFileName(fileName), StringComparison.Ordinal) &&
        fileName.IndexOfAny(Path.GetInvalidFileNameChars()) < 0;

    private static string SanitizeSuggestedName(string? value, string fallback)
    {
        var result = string.Concat((value ?? string.Empty).Select(character =>
            Path.GetInvalidFileNameChars().Contains(character) ? '_' : character)).Trim();
        return string.IsNullOrWhiteSpace(result) ? fallback : result;
    }

    private static FilePickerFileType CreatePngFileType() => new("PNG 图片")
    {
        Patterns = ["*.png"],
        MimeTypes = ["image/png"],
        AppleUniformTypeIdentifiers = ["public.png"],
    };

    private static bool TryParseServerEndpoint(string? value, out string host, out int port)
    {
        host = string.Empty;
        port = 25565;
        var text = value?.Trim();
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        if (text.StartsWith('['))
        {
            var end = text.IndexOf(']');
            if (end <= 1)
            {
                return false;
            }
            host = text[1..end];
            if (end + 1 < text.Length &&
                (!text.AsSpan(end + 1).StartsWith(":") || !int.TryParse(text[(end + 2)..], out port)))
            {
                return false;
            }
        }
        else
        {
            var separator = text.LastIndexOf(':');
            if (separator > 0 && text.IndexOf(':') == separator)
            {
                host = text[..separator];
                if (!int.TryParse(text[(separator + 1)..], out port))
                {
                    return false;
                }
            }
            else
            {
                host = text;
            }
        }

        return !string.IsNullOrWhiteSpace(host) && port is > 0 and <= ushort.MaxValue;
    }

    private static async Task<string> QueryMinecraftServerAsync(string host, int port)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(8));
        using var client = new TcpClient();
        await client.ConnectAsync(host, port, timeout.Token);
        await using var stream = client.GetStream();

        using var handshakeBody = new MemoryStream();
        WriteVarInt(handshakeBody, 0);
        WriteVarInt(handshakeBody, 765);
        WriteMinecraftString(handshakeBody, host);
        handshakeBody.WriteByte((byte)(port >> 8));
        handshakeBody.WriteByte((byte)port);
        WriteVarInt(handshakeBody, 1);
        await WritePacketAsync(stream, handshakeBody.ToArray(), timeout.Token);
        await WritePacketAsync(stream, [0], timeout.Token);

        var packetLength = await ReadVarIntAsync(stream, timeout.Token);
        if (packetLength is <= 0 or > 2_097_152)
        {
            throw new InvalidDataException("服务器返回了无效的状态包长度。");
        }
        if (await ReadVarIntAsync(stream, timeout.Token) != 0)
        {
            throw new InvalidDataException("服务器返回了非状态响应。");
        }
        var jsonLength = await ReadVarIntAsync(stream, timeout.Token);
        if (jsonLength is <= 0 or > 2_097_152)
        {
            throw new InvalidDataException("服务器状态内容长度无效。");
        }
        var buffer = new byte[jsonLength];
        await stream.ReadExactlyAsync(buffer, timeout.Token);
        return Encoding.UTF8.GetString(buffer);
    }

    private static async Task WritePacketAsync(Stream stream, byte[] body, CancellationToken cancellationToken)
    {
        using var packet = new MemoryStream();
        WriteVarInt(packet, body.Length);
        packet.Write(body);
        await stream.WriteAsync(packet.ToArray(), cancellationToken);
        await stream.FlushAsync(cancellationToken);
    }

    private static void WriteMinecraftString(Stream stream, string value)
    {
        var bytes = Encoding.UTF8.GetBytes(value);
        WriteVarInt(stream, bytes.Length);
        stream.Write(bytes);
    }

    private static void WriteVarInt(Stream stream, int value)
    {
        do
        {
            var current = (byte)(value & 0x7F);
            value = (int)((uint)value >> 7);
            if (value != 0)
            {
                current |= 0x80;
            }
            stream.WriteByte(current);
        } while (value != 0);
    }

    private static async Task<int> ReadVarIntAsync(Stream stream, CancellationToken cancellationToken)
    {
        var result = 0;
        for (var position = 0; position < 35; position += 7)
        {
            var buffer = new byte[1];
            if (await stream.ReadAsync(buffer, cancellationToken) != 1)
            {
                throw new EndOfStreamException("服务器在状态响应完成前关闭了连接。");
            }
            result |= (buffer[0] & 0x7F) << position;
            if ((buffer[0] & 0x80) == 0)
            {
                return result;
            }
        }
        throw new InvalidDataException("服务器返回了过长的 VarInt。");
    }

    private static string FlattenMinecraftText(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.String)
        {
            return element.GetString() ?? string.Empty;
        }
        if (element.ValueKind != JsonValueKind.Object)
        {
            return string.Empty;
        }

        var builder = new StringBuilder();
        if (element.TryGetProperty("text", out var text))
        {
            builder.Append(text.GetString());
        }
        if (element.TryGetProperty("extra", out var extra) && extra.ValueKind == JsonValueKind.Array)
        {
            foreach (var child in extra.EnumerateArray())
            {
                builder.Append(FlattenMinecraftText(child));
            }
        }
        return builder.ToString();
    }

    private void ApplyMoreSection(string section)
    {
        MoreDirectorySection.IsVisible = section == "toolbox";
        MoreLogSection.IsVisible = section == "logs";
        PclHelpView.IsVisible = section == "help";
        MoreContentHost.Margin = section == "toolbox"
            ? new Thickness(25, 10, 25, 10)
            : new Thickness(25, 25, 25, 10);
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

        await QuickDownloadCommunityResourceAsync(viewModel, item);
    }

    private async Task QuickDownloadCommunityResourceAsync(
        ViewModels.MainViewModel viewModel,
        ViewModels.CommunityResourceItemViewModel item)
    {
        var project = item.Project;
        var versions = await viewModel.GetCommunityQuickDownloadVersionsAsync(project);
        if (versions.Count == 0)
        {
            await ShowMessageAsync("快速下载", viewModel.CommunityResourceSummary, isWarning: true);
            return;
        }

        var behavior = viewModel.CommunityQuickDownloadBehavior;
        if (behavior == CommunityQuickDownloadBehavior.AlwaysAsk)
        {
            var choice = await ShowQuickDownloadChoiceAsync();
            if (choice is null)
            {
                return;
            }

            behavior = choice.Value;
        }

        MinecraftInstance? instance = null;
        string? destinationDirectory = null;
        switch (behavior)
        {
            case CommunityQuickDownloadBehavior.CurrentInstance:
                instance = viewModel.SelectedInstance;
                if (instance is null)
                {
                    await ShowMessageAsync("快速下载", "当前没有选中的实例，请先选择实例或改用选择路径。", isWarning: true);
                    return;
                }
                break;
            case CommunityQuickDownloadBehavior.AskInstance:
                instance = await ShowQuickDownloadInstanceChoiceAsync(viewModel, project, versions);
                if (instance is null)
                {
                    return;
                }
                break;
            case CommunityQuickDownloadBehavior.AskPath:
                var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
                {
                    Title = "选择资源保存目录",
                    AllowMultiple = false,
                });
                destinationDirectory = folders.SingleOrDefault()?.TryGetLocalPath();
                if (string.IsNullOrWhiteSpace(destinationDirectory))
                {
                    return;
                }
                break;
            default:
                return;
        }

        var version = viewModel.GetLatestCompatibleCommunityVersion(project, versions, instance);
        if (version is null)
        {
            await ShowMessageAsync("快速下载", "当前实例没有兼容的社区资源版本，请改用选择路径。", isWarning: true);
            return;
        }

        destinationDirectory ??= viewModel.GetCommunityInstanceDownloadDirectory(instance!, project.Type);
        await viewModel.QuickDownloadCommunityResourceAsync(project, version, destinationDirectory);
        if (viewModel.CommunityResourceSummary.StartsWith("已", StringComparison.Ordinal))
        {
            await ShowMessageAsync("快速下载", viewModel.CommunityResourceSummary);
        }
    }

    private async Task<CommunityQuickDownloadBehavior?> ShowQuickDownloadChoiceAsync()
    {
        var options = new[]
        {
            new RadioButton { Content = "下载到当前选中实例", IsChecked = true, MinHeight = 30 },
            new RadioButton { Content = "选择一个实例", MinHeight = 30 },
            new RadioButton { Content = "选择一个保存目录", MinHeight = 30 },
        };
        var content = new StackPanel { Spacing = 2 };
        foreach (var option in options)
        {
            content.Children.Add(option);
        }

        var result = await MessageDialogHost.ShowAsync(new PclMessageDialogOptions(
            Title: "快速下载",
            Message: "请选择下载位置。",
            PrimaryButtonText: "继续",
            SecondaryButtonText: "取消",
            Content: content,
            InitialFocus: options[0]));
        if (result != 1)
        {
            return null;
        }

        return options[0].IsChecked == true
            ? CommunityQuickDownloadBehavior.CurrentInstance
            : options[1].IsChecked == true
                ? CommunityQuickDownloadBehavior.AskInstance
                : CommunityQuickDownloadBehavior.AskPath;
    }

    private async Task<MinecraftInstance?> ShowQuickDownloadInstanceChoiceAsync(
        ViewModels.MainViewModel viewModel,
        CommunityResourceProject project,
        IReadOnlyList<CommunityResourceVersion> versions)
    {
        var compatible = viewModel.AvailableInstances
            .Where(instance => instance.Status == MinecraftInstanceStatus.Valid)
            .Select(instance => (Instance: instance, Version: viewModel.GetLatestCompatibleCommunityVersion(project, versions, instance)))
            .Where(item => item.Version is not null)
            .OrderByDescending(item => ReferenceEquals(item.Instance, viewModel.SelectedInstance))
            .ThenBy(item => item.Instance.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToArray();
        if (compatible.Length == 0)
        {
            await ShowMessageAsync("快速下载", "没有找到兼容的本地实例。", isWarning: true);
            return null;
        }

        var options = compatible
            .Select((item, index) => new RadioButton
            {
                Content = $"{item.Instance.Name}（{item.Instance.VersionDisplay}）",
                IsChecked = index == 0,
                MinHeight = 30,
            })
            .ToArray();
        var content = new StackPanel { Spacing = 2 };
        foreach (var option in options)
        {
            content.Children.Add(option);
        }

        var result = await MessageDialogHost.ShowAsync(new PclMessageDialogOptions(
            Title: "选择实例",
            Message: "请选择资源保存到哪个实例。",
            PrimaryButtonText: "下载",
            SecondaryButtonText: "取消",
            Content: content,
            InitialFocus: options[0]));
        if (result != 1)
        {
            return null;
        }

        var selectedIndex = Array.FindIndex(options, option => option.IsChecked == true);
        return selectedIndex >= 0 ? compatible[selectedIndex].Instance : null;
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

    private async void DetailTitleBackClick(object? sender, RoutedEventArgs e)
    {
        if (PclHelpView.CloseDetail())
        {
            return;
        }

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

    private void ShowHelpDetail(PclHelpEntry entry)
    {
        MainTitleBar.IsVisible = false;
        CommunityDetailTitleBar.IsVisible = true;
        CommunityDetailTitleBarTitle.Text = entry.Title;
        MorePageLayout.ColumnDefinitions[0].Width = new GridLength(0);
        MoreSidebar.IsVisible = false;
        MoreContentScroller.Offset = default;
    }

    private void RestoreHelpCatalogLayout()
    {
        MainTitleBar.IsVisible = true;
        CommunityDetailTitleBar.IsVisible = false;
        MorePageLayout.ColumnDefinitions[0].Width = new GridLength(152);
        MoreSidebar.IsVisible = true;
        MoreContentScroller.Offset = default;
    }

    private async void HandleHelpAction(PclHelpAction action)
    {
        try
        {
            switch (action.Type)
            {
                case "打开网页":
                case "下载文件":
                    if (Uri.TryCreate(action.Data, UriKind.Absolute, out var uri) &&
                        uri.Scheme is "http" or "https" &&
                        DataContext is ViewModels.MainViewModel viewModel)
                    {
                        await viewModel.OpenExternalUriAsync(uri);
                    }
                    break;
                case "复制文本":
                    if (TopLevel.GetTopLevel(this)?.Clipboard is { } clipboard)
                    {
                        await clipboard.SetTextAsync(action.Data);
                    }
                    break;
                case "刷新主页":
                    await RefreshHomepageAsync(showSuccess: false);
                    break;
                case "弹出窗口":
                {
                    var parts = action.Data.Split('|', 2);
                    await ShowMessageAsync(parts[0], parts.Length > 1 ? parts[1] : string.Empty);
                    break;
                }
                case "打开文件":
                    await ShowMessageAsync(
                        "仅适用于原版 PCL",
                        "该操作调用的是 Windows 程序或 PCL2 本地目录，无法直接用于当前平台。请根据正文说明改用系统对应工具。",
                        isWarning: true);
                    break;
                case "启动游戏":
                    await ShowMessageAsync(
                        "请从启动页操作",
                        "帮助中的这个按钮是 PCL2 自定义页面事件示例。PCL Aurora 的游戏启动请回到启动页选择实例后进行。",
                        isWarning: false);
                    break;
                case "清理垃圾":
                case "内存优化":
                    await ShowMessageAsync(
                        "当前平台不支持此操作",
                        "这是一项 PCL2 的 Windows 专用帮助事件，PCL Aurora 不会在其他平台伪装执行。",
                        isWarning: true);
                    break;
            }
        }
        catch (Exception exception)
        {
            await ShowMessageAsync("操作失败", exception.Message, isWarning: true);
        }
    }

    private async Task RefreshHomepageAsync(
        bool showSuccess,
        CancellationToken cancellationToken = default)
    {
        if (DataContext is not ViewModels.MainViewModel viewModel ||
            string.IsNullOrWhiteSpace(viewModel.ApplicationDataDirectory))
        {
            return;
        }

        if (viewModel.HomepageTypeIndex == (int)LauncherHomepageType.Blank)
        {
            LaunchDefaultHomepage.IsVisible = true;
            PclHomepageView.IsVisible = false;
            return;
        }

        LaunchDefaultHomepage.IsVisible = false;
        PclHomepageView.IsVisible = true;
        PclHomepageView.ShowStandaloneLoading();

        try
        {
            var content = (LauncherHomepageType)viewModel.HomepageTypeIndex switch
            {
                LauncherHomepageType.LocalFile => await LoadLocalHomepageAsync(
                    viewModel.ApplicationDataDirectory,
                    cancellationToken),
                LauncherHomepageType.Online => await DownloadHomepageAsync(
                    ParseHomepageUri(viewModel.HomepageUrl),
                    cancellationToken),
                LauncherHomepageType.Preset when viewModel.HomepagePresetIndex == 0 => BuildTriviaHomepage(),
                LauncherHomepageType.Preset when viewModel.HomepagePresetIndex == 14 =>
                    await BuildMinecraftNewsHomepageAsync(cancellationToken),
                LauncherHomepageType.Preset => await DownloadHomepageAsync(
                    GetPresetHomepageUri(viewModel.HomepagePresetIndex),
                    cancellationToken),
                _ => throw new InvalidOperationException("不支持的主页类型。"),
            };

            cancellationToken.ThrowIfCancellationRequested();
            PclHomepageView.ShowStandaloneContent(content);
            if (showSuccess)
            {
                await ShowMessageAsync("刷新主页", "主页已重新加载。");
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception exception)
        {
            PclHomepageView.ShowStandaloneError(exception.Message);
            if (showSuccess)
            {
                await ShowMessageAsync("刷新主页失败", exception.Message, isWarning: true);
            }
        }
    }

    private static async Task<string> LoadLocalHomepageAsync(
        string applicationDataDirectory,
        CancellationToken cancellationToken)
    {
        var directory = Path.Combine(applicationDataDirectory, "PCL");
        var path = Path.Combine(directory, "Custom.xaml");
        Directory.CreateDirectory(directory);
        if (!File.Exists(path))
        {
            await File.WriteAllTextAsync(path, DefaultCustomHomepage, cancellationToken);
        }

        if (new FileInfo(path).Length > MaximumHomepageContentLength)
        {
            throw new InvalidDataException("Custom.xaml 超过 2 MB，无法安全加载。");
        }

        return await File.ReadAllTextAsync(path, cancellationToken);
    }

    private async Task<string> DownloadHomepageAsync(Uri uri, CancellationToken cancellationToken)
    {
        using var response = await toolboxHttpClient.GetAsync(
            uri,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);
        response.EnsureSuccessStatusCode();
        if (response.Content.Headers.ContentLength > MaximumHomepageContentLength)
        {
            throw new InvalidDataException("联网主页超过 2 MB，无法安全加载。");
        }

        var content = await response.Content.ReadAsStringAsync(cancellationToken);
        return content.Length <= MaximumHomepageContentLength
            ? content
            : throw new InvalidDataException("联网主页超过 2 MB，无法安全加载。");
    }

    private async Task<string> BuildMinecraftNewsHomepageAsync(CancellationToken cancellationToken)
    {
        var json = await DownloadHomepageAsync(MinecraftNewsApiUri, cancellationToken);
        using var document = JsonDocument.Parse(json);
        if (!document.RootElement.TryGetProperty("result", out var result) ||
            !result.TryGetProperty("results", out var items) ||
            items.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidDataException("Minecraft 官方信息流返回了无法识别的数据。");
        }

        var content = new StringBuilder("<local:MyCard Title=\"Minecraft 官方信息流\" Margin=\"0,0,0,15\"><StackPanel Margin=\"18,8,18,15\">");
        foreach (var item in items.EnumerateArray().Take(24))
        {
            var title = GetJsonString(item, "title", "未命名资讯");
            var description = System.Net.WebUtility.HtmlDecode(GetJsonString(item, "description", string.Empty));
            var link = GetJsonString(item, "readMoreLink", string.Empty);
            if (!IsAllowedMinecraftNewsUri(link))
            {
                continue;
            }

            content.Append("<local:MyListItem Title=\"")
                .Append(System.Net.WebUtility.HtmlEncode(title))
                .Append("\" Info=\"")
                .Append(System.Net.WebUtility.HtmlEncode(description))
                .Append("\" EventType=\"打开网页\" EventData=\"")
                .Append(System.Net.WebUtility.HtmlEncode(link))
                .Append("\" />");
        }
        content.Append("</StackPanel></local:MyCard>");
        return content.ToString();
    }

    private static string BuildTriviaHomepage()
    {
        var hint = HomepageHints[Random.Shared.Next(HomepageHints.Length)];
        return $"""
            <local:MyCard Title="你知道吗？" Margin="0,0,0,15">
                <StackPanel Margin="18,8,18,15" Spacing="10">
                    <TextBlock FontSize="13.5" TextWrapping="Wrap" Text="{System.Net.WebUtility.HtmlEncode(hint)}" />
                    <local:MyButton Text="换一条" EventType="刷新主页" EventData="/" />
                </StackPanel>
            </local:MyCard>
            """;
    }

    private static Uri ParseHomepageUri(string value)
    {
        if (!Uri.TryCreate(value.Trim(), UriKind.Absolute, out var uri) ||
            uri.Scheme is not ("http" or "https"))
        {
            throw new InvalidOperationException("请输入有效的 HTTP 或 HTTPS 主页地址。");
        }

        return uri;
    }

    private static Uri GetPresetHomepageUri(int presetIndex) => presetIndex switch
    {
        1 => new Uri("https://news.bugjump.net"),
        2 => new Uri("https://pclsub.sodamc.com/"),
        3 => new Uri("https://forgepixel.com/pcl_sub_file"),
        4 => new Uri("https://pcl-bmcl.milu.ink/"),
        5 => new Uri("https://raw.gitcode.com/WForst-Breeze/WhatsNewPCL/raw/main/Custom.xaml"),
        6 => new Uri("https://pclhomeplazaoss.lingyunawa.top:26994/d/Homepages/Ext1nguisher/Custom.xaml"),
        7 => new Uri("https://ddf.pcl-community.org/Custom.xaml"),
        8 => new Uri("https://raw.gitcode.com/ENC_Euphony/PCL-AI-Summary-HomePage/raw/master/Custom.xaml"),
        9 => new Uri("https://pcl.wyc-w.top/index.xaml"),
        10 => new Uri("https://www.xxag.top/mkss"),
        11 => new Uri("https://qawsedrftgyhujiko.fun/pcl2/Custom.xaml"),
        12 => new Uri("https://bangumi.p.kaphia.qzz.io"),
        13 => new Uri("https://s3.pysio.online/pcl2-ce/apiv2/pages/announce.xaml"),
        _ => throw new ArgumentOutOfRangeException(nameof(presetIndex), "未知的主页预设。"),
    };

    private static string GetJsonString(JsonElement element, string propertyName, string fallback) =>
        element.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? fallback
            : fallback;

    private static bool IsAllowedMinecraftNewsUri(string value)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https"))
        {
            return false;
        }

        return uri.IdnHost.Equals("minecraft.net", StringComparison.OrdinalIgnoreCase) ||
               uri.IdnHost.EndsWith(".minecraft.net", StringComparison.OrdinalIgnoreCase) ||
               uri.IdnHost.Equals("minecraft-services.net", StringComparison.OrdinalIgnoreCase) ||
               uri.IdnHost.EndsWith(".minecraft-services.net", StringComparison.OrdinalIgnoreCase) ||
               uri.IdnHost.Equals("microsoft.com", StringComparison.OrdinalIgnoreCase) ||
               uri.IdnHost.EndsWith(".microsoft.com", StringComparison.OrdinalIgnoreCase);
    }

    private const string DefaultCustomHomepage = """
        <local:MyCard Title="自定义主页" Margin="0,0,0,15">
            <StackPanel Margin="18,8,18,15" Spacing="8">
                <TextBlock FontSize="13.5" TextWrapping="Wrap" Text="这是 PCL Aurora 的 Custom.xaml 主页。" />
                <TextBlock FontSize="12" TextWrapping="Wrap" Text="编辑应用数据目录 PCL/Custom.xaml 后，回到设置点击刷新主页即可重新加载。" />
            </StackPanel>
        </local:MyCard>
        """;

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

        if (section == "update" && DataContext is ViewModels.MainViewModel updateViewModel)
        {
            await updateViewModel.CheckForUpdatesCommand.ExecuteAsync(null);
        }

        if (section == "feedback" && DataContext is ViewModels.MainViewModel feedbackViewModel)
        {
            await feedbackViewModel.RefreshFeedbackAsync();
        }

        if (section == "log" && DataContext is ViewModels.MainViewModel logViewModel)
        {
            await logViewModel.RefreshLauncherLogsAsync();
        }
    }

    private async void UpdateChangelogClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is ViewModels.MainViewModel viewModel)
        {
            await ShowMessageAsync("更新日志", viewModel.UpdateChangelog);
        }
    }

    private async void SubmitFeedbackClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not ViewModels.MainViewModel viewModel)
        {
            return;
        }

        var result = await MessageDialogHost.ShowAsync(new PclMessageDialogOptions(
            Title: "反馈",
            Message: "提交前请先搜索是否已有相同反馈，并确认问题仍能在当前版本中复现。反馈内容应包含复现步骤、实际结果和必要的日志。",
            PrimaryButtonText: "提交新反馈",
            SecondaryButtonText: "查看反馈列表",
            TertiaryButtonText: "取消"));
        if (result == 1)
        {
            await viewModel.OpenNewFeedbackAsync();
        }
        else if (result == 2)
        {
            await viewModel.OpenProjectPageCommand.ExecuteAsync("issues");
        }
    }

    private async void FeedbackIssueClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: ViewModels.FeedbackIssueItemViewModel item } ||
            DataContext is not ViewModels.MainViewModel viewModel)
        {
            return;
        }

        var issue = item.Issue;
        var typeName = string.IsNullOrWhiteSpace(issue.TypeName) ? "未分类" : issue.TypeName;
        var labels = issue.Labels.Count == 0 ? "无" : string.Join("、", issue.Labels);
        var message = $"由 {issue.Author} 提交于 {issue.CreatedAt.LocalDateTime:yyyy/M/d HH:mm}\n" +
                      $"类型：{typeName}\n标签：{labels}\n\n{issue.Body}";
        var result = await MessageDialogHost.ShowAsync(new PclMessageDialogOptions(
            Title: $"#{issue.Number} {issue.Title}",
            Message: message,
            PrimaryButtonText: "确定",
            SecondaryButtonText: "查看详情"));
        if (result == 2)
        {
            await viewModel.OpenFeedbackIssueAsync(issue);
        }
    }

    private async void ExportCurrentLogClick(object? sender, RoutedEventArgs e) =>
        await ExportLauncherLogsAsync(exportAll: false);

    private async void ExportAllLogsClick(object? sender, RoutedEventArgs e) =>
        await ExportLauncherLogsAsync(exportAll: true);

    private async Task ExportLauncherLogsAsync(bool exportAll)
    {
        if (DataContext is not ViewModels.MainViewModel viewModel)
        {
            return;
        }

        var zipType = new FilePickerFileType("ZIP 压缩文件")
        {
            Patterns = ["*.zip"],
            MimeTypes = ["application/zip"],
            AppleUniformTypeIdentifiers = ["public.zip-archive"],
        };
        var destination = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "选择日志保存位置",
            SuggestedFileName = $"PCL_Aurora_Logs_{DateTime.Now:yyyyMMddHHmmss}.zip",
            DefaultExtension = "zip",
            FileTypeChoices = [zipType],
            SuggestedFileType = zipType,
            ShowOverwritePrompt = true,
        });
        var destinationPath = destination?.TryGetLocalPath();
        if (string.IsNullOrWhiteSpace(destinationPath))
        {
            return;
        }

        try
        {
            await viewModel.ExportLauncherLogsAsync(destinationPath, exportAll);
            await ShowMessageAsync("导出日志", exportAll ? "全部日志已导出。" : "当前日志已导出。");
        }
        catch (Exception exception)
        {
            await ShowMessageAsync("导出失败", exception.Message, isWarning: true);
        }
    }

    private async void OpenLogDirectoryClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not ViewModels.MainViewModel viewModel)
        {
            return;
        }

        try
        {
            await viewModel.OpenLauncherLogDirectoryAsync();
        }
        catch (Exception exception)
        {
            await ShowMessageAsync("打开失败", exception.Message, isWarning: true);
        }
    }

    private async void ClearLogHistoryClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not ViewModels.MainViewModel viewModel ||
            !await ShowConfirmationAsync(
                "清理历史日志",
                "即将删除除当前日志之外的所有 PCL Aurora 历史日志。此操作不可撤销，是否继续？"))
        {
            return;
        }

        try
        {
            var deleted = await viewModel.ClearLauncherLogHistoryAsync();
            await ShowMessageAsync("清理历史日志", $"已清理 {deleted} 个历史日志文件。");
        }
        catch (Exception exception)
        {
            await ShowMessageAsync("清理失败", exception.Message, isWarning: true);
        }
    }

    private async void LauncherLogFileClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: ViewModels.LauncherLogFileItemViewModel item } ||
            DataContext is not ViewModels.MainViewModel viewModel)
        {
            return;
        }

        try
        {
            await viewModel.OpenLauncherLogFileAsync(item.File);
        }
        catch (Exception exception)
        {
            await ShowMessageAsync("打开失败", exception.Message, isWarning: true);
        }
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

    private async void RefreshMusicContentClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is ViewModels.MainViewModel viewModel)
        {
            await viewModel.RefreshBackgroundMusicAsync(startAccordingToSettings: true);
        }
    }

    private async void ClearMusicContentClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not ViewModels.MainViewModel viewModel ||
            !await ShowConfirmationAsync("清空背景音乐", "即将删除背景音乐文件夹中的所有文件。此操作不可撤销，是否确定？"))
        {
            return;
        }

        await viewModel.StopBackgroundMusicAsync();
        ClearDirectoryFiles(viewModel.GetInterfaceContentDirectory("music"));
        await viewModel.RefreshBackgroundMusicAsync();
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
        else if (section == "feedback")
        {
            await viewModel.RefreshFeedbackAsync();
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
