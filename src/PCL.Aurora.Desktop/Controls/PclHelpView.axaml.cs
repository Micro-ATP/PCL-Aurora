using System.Globalization;
using System.Xml.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using PCL.Aurora.Desktop.Models;
using PCL.Aurora.Desktop.Services;

namespace PCL.Aurora.Desktop.Controls;

// Catalog/search/detail structure adapts PCL2 PageOtherHelp and PageOtherHelpDetail.
public partial class PclHelpView : UserControl
{
    public static readonly StyledProperty<bool> IsStandaloneProperty =
        AvaloniaProperty.Register<PclHelpView, bool>(nameof(IsStandalone));

    private static readonly IBrush PrimaryTextBrush = new SolidColorBrush(Color.Parse("#35404C"));
    private static readonly IBrush MutedTextBrush = new SolidColorBrush(Color.Parse("#87929D"));
    private static readonly IBrush ThemeBrush = new SolidColorBrush(Color.Parse("#127AE1"));
    private static readonly IBrush CardBrush = new SolidColorBrush(Color.Parse("#FBFCFE"));
    private static readonly IBrush WarningBrush = new SolidColorBrush(Color.Parse("#FFF0F0"));
    private static readonly IBrush InformationBrush = new SolidColorBrush(Color.Parse("#EAF4FE"));

    private IReadOnlyList<PclHelpEntry> entries = [];
    private Dictionary<string, PclHelpEntry> entriesByPath = new(StringComparer.OrdinalIgnoreCase);
    private readonly Stack<PclHelpEntry> detailHistory = new();
    private PclHelpEntry? currentEntry;

    internal event Action<PclHelpEntry>? DetailOpened;
    internal event Action? DetailClosed;
    internal event Action<PclHelpAction>? ActionRequested;

    public bool IsStandalone
    {
        get => GetValue(IsStandaloneProperty);
        set => SetValue(IsStandaloneProperty, value);
    }

    public PclHelpView()
    {
        InitializeComponent();
        AttachedToVisualTree += async (_, _) =>
        {
            if (!IsStandalone && entries.Count == 0)
            {
                await ReloadAsync();
            }
        };
    }

    internal void ShowStandaloneLoading(string text = "正在加载主页")
    {
        LoadingPanel.Children.Clear();
        LoadingPanel.Children.Add(new PclLoadingIndicator { Text = text });
        LoadingPanel.IsVisible = true;
        HomePanel.IsVisible = false;
        DetailPanel.IsVisible = false;
        ErrorPanel.IsVisible = false;
    }

    internal void ShowStandaloneContent(string content)
    {
        ArgumentNullException.ThrowIfNull(content);
        LoadingPanel.IsVisible = false;
        ErrorPanel.IsVisible = false;
        currentEntry = null;
        detailHistory.Clear();
        RenderDetail(new PclHelpEntry(
            "Homepage/Custom.xaml",
            "主页",
            string.Empty,
            string.Empty,
            [],
            null,
            false,
            true,
            true,
            false,
            null,
            null,
            content), rememberCurrent: false);
    }

    internal void ShowStandaloneError(string message)
    {
        LoadingPanel.IsVisible = false;
        HomePanel.IsVisible = false;
        DetailPanel.IsVisible = false;
        ErrorText.Text = message;
        ErrorPanel.IsVisible = true;
    }

    internal async Task ReloadAsync()
    {
        LoadingPanel.IsVisible = true;
        HomePanel.IsVisible = false;
        DetailPanel.IsVisible = false;
        ErrorPanel.IsVisible = false;
        await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Background);

        try
        {
            entries = await Task.Run(PclHelpCatalog.Load);
            entriesByPath = entries.ToDictionary(entry => NormalizePath(entry.Path), StringComparer.OrdinalIgnoreCase);
            BuildCategoryView();
            currentEntry = null;
            detailHistory.Clear();
            SearchBox.Text = string.Empty;
            LoadingPanel.IsVisible = false;
            HomePanel.IsVisible = true;
        }
        catch (Exception exception)
        {
            LoadingPanel.IsVisible = false;
            ErrorPanel.IsVisible = true;
            ErrorText.Text = exception.Message;
        }
    }

    internal bool CloseDetail()
    {
        if (currentEntry is null)
        {
            return false;
        }

        if (detailHistory.TryPop(out var previous))
        {
            RenderDetail(previous, rememberCurrent: false);
            return true;
        }

        return ResetToHome();
    }

    internal bool ResetToHome()
    {
        if (currentEntry is null)
        {
            return false;
        }

        currentEntry = null;
        detailHistory.Clear();
        DetailPanel.Children.Clear();
        DetailPanel.IsVisible = false;
        HomePanel.IsVisible = true;
        DetailClosed?.Invoke();
        return true;
    }

    private void BuildCategoryView()
    {
        CategoryPanel.Children.Clear();
        var categories = entries
            .Where(entry => entry.ShowInSnapshot)
            .SelectMany(entry => entry.Categories)
            .Distinct(StringComparer.Ordinal)
            .ToList();
        if (categories.Remove("指南")) categories.Insert(0, "指南");

        foreach (var category in categories)
        {
            var body = new StackPanel { Margin = new Thickness(14, 0, 14, 14) };
            foreach (var entry in entries.Where(entry => entry.ShowInSnapshot && entry.Categories.Contains(category)))
            {
                body.Children.Add(CreateEntryButton(entry));
            }

            CategoryPanel.Children.Add(new PclCollapsibleCard
            {
                Title = category,
                IsExpanded = category == "指南",
                Body = body,
            });
        }
    }

    private Button CreateEntryButton(PclHelpEntry entry)
    {
        var image = new Image
        {
            Width = 30,
            Height = 30,
            VerticalAlignment = VerticalAlignment.Center,
            Stretch = Stretch.Uniform,
            Source = LoadEntryIcon(entry),
        };
        var text = new StackPanel
        {
            VerticalAlignment = VerticalAlignment.Center,
            Spacing = 0,
            Children =
            {
                new TextBlock
                {
                    FontSize = 13,
                    Foreground = PrimaryTextBrush,
                    Text = entry.Title,
                    TextTrimming = TextTrimming.CharacterEllipsis,
                },
                new TextBlock
                {
                    FontSize = 11,
                    Foreground = MutedTextBrush,
                    Text = entry.Description,
                    TextTrimming = TextTrimming.CharacterEllipsis,
                },
            },
        };
        Grid.SetColumn(text, 1);
        var content = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("34,*"),
            ColumnSpacing = 4,
            Children = { image, text },
        };
        var button = new Button { Content = content, Tag = entry };
        button.Classes.Add("help-entry");
        button.Click += EntryClick;
        ToolTip.SetTip(button, string.IsNullOrWhiteSpace(entry.Description) ? entry.Title : entry.Description);
        return button;
    }

    private static Bitmap? LoadEntryIcon(PclHelpEntry entry)
    {
        var name = entry.Logo switch
        {
            { } logo when logo.Contains("CommandBlock", StringComparison.OrdinalIgnoreCase) => "Pcl2CommandBlock.png",
            { } logo when logo.Contains("GrassPath", StringComparison.OrdinalIgnoreCase) => "PclCeOptiFine.png",
            _ when entry.IsEvent && !string.Equals(entry.EventType, "弹出窗口", StringComparison.Ordinal) => "Pcl2CommandBlock.png",
            _ => "Pcl2Grass.png",
        };
        try
        {
            using var stream = AssetLoader.Open(new Uri($"avares://PCL.Aurora.Desktop/Assets/Loaders/{name}"));
            return new Bitmap(stream);
        }
        catch
        {
            return null;
        }
    }

    private void SearchBoxTextChanged(object? sender, TextChangedEventArgs e)
    {
        var query = SearchBox.Text?.Trim() ?? string.Empty;
        if (query.Length == 0)
        {
            SearchPanel.IsVisible = false;
            CategoryPanel.IsVisible = true;
            return;
        }

        CategoryPanel.IsVisible = false;
        SearchPanel.IsVisible = true;
        SearchPanel.Children.Clear();
        var results = entries
            .Where(entry => entry.ShowInSnapshot && entry.ShowInSearch)
            .Select(entry => (Entry: entry, Score: GetSearchScore(entry, query)))
            .Where(result => result.Score > 0)
            .OrderByDescending(result => result.Score)
            .ThenBy(result => result.Entry.Title, StringComparer.Ordinal)
            .ToArray();

        var body = new StackPanel { Margin = new Thickness(14, 0, 14, 14) };
        foreach (var result in results)
        {
            body.Children.Add(CreateEntryButton(result.Entry));
        }

        SearchPanel.Children.Add(new PclCollapsibleCard
        {
            Title = results.Length == 0 ? "无搜索结果" : "搜索结果",
            IsExpanded = results.Length > 0,
            Body = body,
        });
    }

    private static double GetSearchScore(PclHelpEntry entry, string query)
    {
        var terms = query.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var score = 0d;
        foreach (var term in terms)
        {
            if (entry.Keywords.Contains(term, StringComparison.OrdinalIgnoreCase)) score += 3;
            if (entry.Title.Contains(term, StringComparison.OrdinalIgnoreCase)) score += 2;
            if (entry.Description.Contains(term, StringComparison.OrdinalIgnoreCase)) score += 1;
        }
        return score;
    }

    private void EntryClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: PclHelpEntry entry })
        {
            return;
        }

        if (entry.IsEvent)
        {
            RequestAction(entry.EventType, entry.EventData);
            return;
        }

        OpenDetail(entry);
    }

    private void OpenDetail(PclHelpEntry entry) => RenderDetail(entry, rememberCurrent: true);

    private void RenderDetail(PclHelpEntry entry, bool rememberCurrent)
    {
        try
        {
            if (rememberCurrent && currentEntry is not null && !ReferenceEquals(currentEntry, entry))
            {
                detailHistory.Push(currentEntry);
            }
            DetailPanel.Children.Clear();
            if (NeedsPlatformNotice(entry))
            {
                DetailPanel.Children.Add(CreateHint(
                    "这篇内容来自 PCL2，涉及 Windows 的步骤仅适用于 Windows。macOS 与 Linux 请以 PCL Aurora 当前界面和系统工具为准。",
                    isWarning: false,
                    null));
            }

            foreach (var child in ParseContent(entry.Content ?? string.Empty))
            {
                DetailPanel.Children.Add(child);
            }

            currentEntry = entry;
            HomePanel.IsVisible = false;
            DetailPanel.IsVisible = true;
            DetailOpened?.Invoke(entry);
        }
        catch (Exception exception)
        {
            ActionRequested?.Invoke(new PclHelpAction("弹出窗口", $"帮助页面加载失败|{exception.Message}"));
        }
    }

    private IEnumerable<Control> ParseContent(string content)
    {
        var wrapped = $"<Root xmlns:local=\"urn:pcl\" xmlns:x=\"urn:x\">{content}</Root>";
        var root = XElement.Parse(wrapped, LoadOptions.PreserveWhitespace);
        foreach (var element in root.Elements())
        {
            if (BuildElement(element) is { } control)
            {
                yield return control;
            }
        }
    }

    private Control? BuildElement(XElement element)
    {
        var name = element.Name.LocalName;
        if (name.Contains('.', StringComparison.Ordinal))
        {
            return null;
        }

        Control? control = name switch
        {
            "TextBlock" => BuildTextBlock(element),
            "Label" => BuildTextBlock(element),
            "StackPanel" => BuildStackPanel(element),
            "Grid" => BuildGrid(element),
            "MyCard" => BuildCard(element),
            "MyHint" => CreateHint(GetAttribute(element, "Text"), GetBoolean(element, "IsWarn", true), GetAction(element)),
            "MyButton" or "MyTextButton" or "MyIconTextButton" or "MyIconButton" => BuildActionButton(element),
            "MyListItem" => BuildListItem(element),
            "MyImage" => BuildImage(element),
            "Path" => BuildPath(element),
            _ => BuildContainerFallback(element),
        };

        if (control is not null)
        {
            ApplyCommonProperties(control, element);
        }
        return control;
    }

    private Control BuildTextBlock(XElement element)
    {
        var block = new TextBlock
        {
            Text = GetAttribute(element, "Text", element.Value),
            TextWrapping = GetAttribute(element, "TextWrapping") == "NoWrap" ? TextWrapping.NoWrap : TextWrapping.Wrap,
            Foreground = PrimaryTextBrush,
            FontSize = GetDouble(element, "FontSize", 13),
            LineHeight = GetDouble(element, "LineHeight", double.NaN),
        };
        if (Enum.TryParse<FontWeight>(GetAttribute(element, "FontWeight"), true, out var weight))
        {
            block.FontWeight = weight;
        }
        if (TryParseColor(GetAttribute(element, "Foreground"), out var color))
        {
            block.Foreground = new SolidColorBrush(color);
        }
        return block;
    }

    private Control BuildStackPanel(XElement element)
    {
        var panel = new StackPanel
        {
            Orientation = GetAttribute(element, "Orientation") == "Horizontal" ? Orientation.Horizontal : Orientation.Vertical,
            Spacing = GetDouble(element, "Spacing", 0),
        };
        AddChildren(panel.Children, element);
        return panel;
    }

    private Control BuildGrid(XElement element)
    {
        var grid = new Grid();
        var rows = element.Elements().FirstOrDefault(child => child.Name.LocalName == "Grid.RowDefinitions");
        var columns = element.Elements().FirstOrDefault(child => child.Name.LocalName == "Grid.ColumnDefinitions");
        if (rows is not null)
        {
            foreach (var row in rows.Elements()) grid.RowDefinitions.Add(new RowDefinition(ParseGridLength(GetAttribute(row, "Height", "*"))));
        }
        if (columns is not null)
        {
            foreach (var column in columns.Elements()) grid.ColumnDefinitions.Add(new ColumnDefinition(ParseGridLength(GetAttribute(column, "Width", "*"))));
        }

        foreach (var childElement in element.Elements().Where(child => !child.Name.LocalName.Contains('.', StringComparison.Ordinal)))
        {
            if (BuildElement(childElement) is not { } child) continue;
            Grid.SetRow(child, GetInt(childElement, "Grid.Row", 0));
            Grid.SetColumn(child, GetInt(childElement, "Grid.Column", 0));
            Grid.SetRowSpan(child, GetInt(childElement, "Grid.RowSpan", 1));
            Grid.SetColumnSpan(child, GetInt(childElement, "Grid.ColumnSpan", 1));
            grid.Children.Add(child);
        }
        return grid;
    }

    private Control BuildCard(XElement element)
    {
        var body = new StackPanel();
        AddChildren(body.Children, element);
        if (body.Children.FirstOrDefault() is Control first && first.Margin.Top >= 35)
        {
            first.Margin = new Thickness(first.Margin.Left, Math.Max(0, first.Margin.Top - 40), first.Margin.Right, first.Margin.Bottom);
        }
        return new PclCollapsibleCard
        {
            Title = GetAttribute(element, "Title"),
            IsExpanded = !GetBoolean(element, "IsSwapped", false),
            Body = body,
            Margin = new Thickness(0, 0, 0, 15),
        };
    }

    private Control BuildActionButton(XElement element)
    {
        var button = new Button
        {
            Content = GetAttribute(element, "Text", GetAttribute(element, "ToolTip", "打开")),
            Tag = GetAction(element),
        };
        button.Classes.Add("help-detail-action");
        button.Click += ActionButtonClick;
        return button;
    }

    private Control BuildListItem(XElement element)
    {
        var title = GetAttribute(element, "Title", GetAttribute(element, "Text", "继续阅读"));
        var info = GetAttribute(element, "Info");
        var action = GetAction(element);
        PclHelpEntry? linked = null;
        if (action?.Type == "打开帮助")
        {
            entriesByPath.TryGetValue(NormalizePath(action.Data), out linked);
        }
        var entry = linked ?? new PclHelpEntry(
            string.Empty,
            title,
            info,
            string.Empty,
            [],
            GetAttribute(element, "Logo"),
            false,
            true,
            true,
            action is not null && action.Type != "打开帮助",
            action?.Type,
            action?.Data,
            null);
        var button = CreateEntryButton(entry);
        button.Tag = linked ?? (object?)action;
        button.Click -= EntryClick;
        button.Click += DetailListItemClick;
        return button;
    }

    private Control BuildImage(XElement element)
    {
        var source = GetAttribute(element, "Source");
        if (source.StartsWith("http", StringComparison.OrdinalIgnoreCase))
        {
            return new PclRemoteImage(source);
        }
        return new Border
        {
            MinHeight = 42,
            Background = InformationBrush,
            CornerRadius = new CornerRadius(4),
            Child = new TextBlock
            {
                Margin = new Thickness(12),
                HorizontalAlignment = HorizontalAlignment.Center,
                Foreground = MutedTextBrush,
                Text = "图片资源",
            },
        };
    }

    private Control BuildPath(XElement element)
    {
        var path = new Avalonia.Controls.Shapes.Path
        {
            Width = GetDouble(element, "Width", 18),
            Height = GetDouble(element, "Height", 18),
            Stretch = Stretch.Uniform,
            Fill = ThemeBrush,
        };
        try { path.Data = Geometry.Parse(GetAttribute(element, "Data")); } catch { }
        return path;
    }

    private Control? BuildContainerFallback(XElement element)
    {
        var panel = new StackPanel();
        AddChildren(panel.Children, element);
        return panel.Children.Count == 0 ? null : panel;
    }

    private Border CreateHint(string text, bool isWarning, PclHelpAction? action)
    {
        var border = new Border
        {
            Margin = new Thickness(0, 0, 0, 15),
            Padding = new Thickness(12, 10),
            Background = isWarning ? WarningBrush : InformationBrush,
            BorderBrush = new SolidColorBrush(Color.Parse(isWarning ? "#F0BABA" : "#B9DCF7")),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(4),
            Child = new TextBlock
            {
                Foreground = new SolidColorBrush(Color.Parse(isWarning ? "#A94747" : "#3E6D93")),
                FontSize = 12,
                Text = text,
                TextWrapping = TextWrapping.Wrap,
            },
        };
        if (action is not null)
        {
            border.Cursor = new Avalonia.Input.Cursor(Avalonia.Input.StandardCursorType.Hand);
            border.Tag = action;
            border.PointerPressed += (_, _) => RequestAction(action.Type, action.Data);
        }
        return border;
    }

    private void AddChildren(Avalonia.Controls.Controls children, XElement element)
    {
        foreach (var childElement in element.Elements().Where(child => !child.Name.LocalName.Contains('.', StringComparison.Ordinal)))
        {
            if (BuildElement(childElement) is { } child) children.Add(child);
        }
    }

    private void DetailListItemClick(object? sender, RoutedEventArgs e)
    {
        switch (sender)
        {
            case Button { Tag: PclHelpEntry entry }:
                OpenDetail(entry);
                break;
            case Button { Tag: PclHelpAction action }:
                RequestAction(action.Type, action.Data);
                break;
        }
    }

    private void ActionButtonClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: PclHelpAction action }) RequestAction(action.Type, action.Data);
    }

    private void RequestAction(string? type, string? data)
    {
        if (string.IsNullOrWhiteSpace(type)) return;
        if (type == "打开帮助" && entriesByPath.TryGetValue(NormalizePath(data ?? string.Empty), out var entry))
        {
            OpenDetail(entry);
            return;
        }
        ActionRequested?.Invoke(new PclHelpAction(type, data ?? string.Empty));
    }

    private void ApplyCommonProperties(Control control, XElement element)
    {
        if (TryParseThickness(GetAttribute(element, "Margin"), out var margin)) control.Margin = margin;
        control.Width = GetDouble(element, "Width", control.Width);
        control.Height = GetDouble(element, "Height", control.Height);
        control.MinWidth = GetDouble(element, "MinWidth", control.MinWidth);
        control.MinHeight = GetDouble(element, "MinHeight", control.MinHeight);
        control.MaxWidth = GetDouble(element, "MaxWidth", control.MaxWidth);
        control.MaxHeight = GetDouble(element, "MaxHeight", control.MaxHeight);
        control.IsEnabled = GetBoolean(element, "IsEnabled", control.IsEnabled);
        if (Enum.TryParse<HorizontalAlignment>(GetAttribute(element, "HorizontalAlignment"), true, out var horizontal)) control.HorizontalAlignment = horizontal;
        if (Enum.TryParse<VerticalAlignment>(GetAttribute(element, "VerticalAlignment"), true, out var vertical)) control.VerticalAlignment = vertical;
        var toolTip = GetAttribute(element, "ToolTip");
        if (!string.IsNullOrWhiteSpace(toolTip)) ToolTip.SetTip(control, toolTip);
    }

    private static PclHelpAction? GetAction(XElement element)
    {
        var type = GetAttribute(element, "EventType");
        return string.IsNullOrWhiteSpace(type) ? null : new PclHelpAction(type, GetAttribute(element, "EventData"));
    }

    private static bool NeedsPlatformNotice(PclHelpEntry entry) =>
        !OperatingSystem.IsWindows() &&
        (entry.Path.Contains("Microsoft Defender", StringComparison.OrdinalIgnoreCase) ||
         entry.Path.Contains("备份设置", StringComparison.Ordinal) ||
         entry.Content?.Contains("Windows", StringComparison.OrdinalIgnoreCase) == true);

    private static string NormalizePath(string path) => path.Replace('\\', '/').TrimStart('/');
    private static string GetAttribute(XElement element, string name, string fallback = "") =>
        element.Attributes().FirstOrDefault(attribute => attribute.Name.LocalName == name)?.Value ?? fallback;
    private static bool GetBoolean(XElement element, string name, bool fallback) =>
        bool.TryParse(GetAttribute(element, name), out var value) ? value : fallback;
    private static int GetInt(XElement element, string name, int fallback) =>
        int.TryParse(GetAttribute(element, name), NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) ? value : fallback;
    private static double GetDouble(XElement element, string name, double fallback) =>
        double.TryParse(GetAttribute(element, name), NumberStyles.Float, CultureInfo.InvariantCulture, out var value) ? value : fallback;

    private static bool TryParseThickness(string value, out Thickness thickness)
    {
        thickness = default;
        if (string.IsNullOrWhiteSpace(value)) return false;
        var parts = value.Split(',').Select(part => double.TryParse(part.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var number) ? number : 0).ToArray();
        thickness = parts.Length switch
        {
            1 => new Thickness(parts[0]),
            2 => new Thickness(parts[0], parts[1]),
            4 => new Thickness(parts[0], parts[1], parts[2], parts[3]),
            _ => default,
        };
        return parts.Length is 1 or 2 or 4;
    }

    private static GridLength ParseGridLength(string value)
    {
        if (value.Equals("Auto", StringComparison.OrdinalIgnoreCase)) return GridLength.Auto;
        if (value.EndsWith('*'))
        {
            var amount = value.Length == 1 ? 1 : double.TryParse(value[..^1], NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed) ? parsed : 1;
            return new GridLength(amount, GridUnitType.Star);
        }
        return double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var pixels) ? new GridLength(pixels) : GridLength.Auto;
    }

    private static bool TryParseColor(string value, out Color color)
    {
        try { color = Color.Parse(value); return !string.IsNullOrWhiteSpace(value); }
        catch { color = default; return false; }
    }

    private async void RetryClick(object? sender, RoutedEventArgs e)
    {
        if (IsStandalone)
        {
            RequestAction("刷新主页", "/");
            return;
        }

        await ReloadAsync();
    }
}
