using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace PCL.Aurora.Desktop.Controls;

public sealed class PclNavigationButton : Button
{
    public static readonly StyledProperty<string> LabelProperty =
        AvaloniaProperty.Register<PclNavigationButton, string>(nameof(Label), string.Empty);

    public static readonly StyledProperty<Geometry?> IconDataProperty =
        AvaloniaProperty.Register<PclNavigationButton, Geometry?>(nameof(IconData));

    public string Label
    {
        get => GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    public Geometry? IconData
    {
        get => GetValue(IconDataProperty);
        set => SetValue(IconDataProperty, value);
    }
}
