using System.Collections.ObjectModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Media;
using Avalonia.VisualTree;

namespace PCL.Aurora.Desktop.Controls;

public sealed record PclFontOption(string DisplayName, string Tag, FontFamily Font);

/// <summary>
/// 跨平台系统字体选择器，加载、排序与默认项语义适配自 PCL-CE FontSelector。
/// </summary>
public sealed class PclFontSelector : ComboBox
{
    private static readonly FontFamily DefaultFont = FontFamily.Parse(
        "avares://PCL.Aurora.Desktop/Fonts/HarmonyOS_Sans_SC#HarmonyOS Sans SC");

    public static readonly StyledProperty<string> SelectedFontTagProperty =
        AvaloniaProperty.Register<PclFontSelector, string>(
            nameof(SelectedFontTag),
            string.Empty,
            defaultBindingMode: BindingMode.TwoWay);

    private readonly ObservableCollection<PclFontOption> fonts = [];
    private bool isSynchronizing;
    private bool isLoaded;

    protected override Type StyleKeyOverride => typeof(ComboBox);

    public PclFontSelector()
    {
        ItemsSource = fonts;
        MaxDropDownHeight = 300;
        SelectionChanged += (_, _) => SynchronizeTagFromSelection();
    }

    public string SelectedFontTag
    {
        get => GetValue(SelectedFontTagProperty);
        set => SetValue(SelectedFontTagProperty, value);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == SelectedFontTagProperty && !isSynchronizing)
        {
            SynchronizeSelectionFromTag();
        }
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        LoadFonts();
    }

    private void LoadFonts()
    {
        if (isLoaded)
        {
            return;
        }

        isLoaded = true;
        fonts.Add(new PclFontOption("默认", string.Empty, DefaultFont));
        foreach (var family in FontManager.Current.SystemFonts
                     .Where(font => !string.IsNullOrWhiteSpace(font.Name))
                     .GroupBy(font => font.Name, StringComparer.OrdinalIgnoreCase)
                     .Select(group => group.First())
                     .OrderBy(font => font.Name, StringComparer.Ordinal))
        {
            fonts.Add(new PclFontOption(family.Name, family.Name, family));
        }

        SynchronizeSelectionFromTag();
    }

    private void SynchronizeSelectionFromTag()
    {
        if (fonts.Count == 0)
        {
            return;
        }

        isSynchronizing = true;
        SelectedItem = fonts.FirstOrDefault(option =>
                           string.Equals(option.Tag, SelectedFontTag, StringComparison.OrdinalIgnoreCase))
                       ?? fonts[0];
        isSynchronizing = false;
    }

    private void SynchronizeTagFromSelection()
    {
        if (isSynchronizing || SelectedItem is not PclFontOption option)
        {
            return;
        }

        isSynchronizing = true;
        SetCurrentValue(SelectedFontTagProperty, option.Tag);
        isSynchronizing = false;
    }
}
