using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace PCL.Aurora.Desktop.Controls;

public partial class PclLoadingIndicator : UserControl
{
    private const double AnimationDurationMilliseconds = 2150;

    private static readonly (double Cue, double Angle)[] AnimationKeyFrames =
    [
        (0, 25),
        (0.16, -20),
        (0.58, 50),
        (0.76, 28),
        (0.9, 34),
        (1, 25),
    ];

    public static readonly StyledProperty<string> TextProperty =
        AvaloniaProperty.Register<PclLoadingIndicator, string>(nameof(Text), "正在加载…");

    private readonly DispatcherTimer animationTimer = new()
    {
        Interval = TimeSpan.FromMilliseconds(16),
    };

    private readonly Stopwatch animationStopwatch = new();

    public PclLoadingIndicator()
    {
        InitializeComponent();
        animationTimer.Tick += (_, _) => UpdateAnimationFrame();
        AttachedToVisualTree += (_, _) => StartAnimation();
        DetachedFromVisualTree += (_, _) => StopAnimation();
        PropertyChanged += (_, args) =>
        {
            if (args.Property == IsVisibleProperty)
            {
                if (IsVisible)
                {
                    StartAnimation();
                }
                else
                {
                    StopAnimation();
                }
            }
        };
    }

    public string Text
    {
        get => GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    private void StartAnimation()
    {
        if (!IsVisible || !this.IsAttachedToVisualTree() || animationTimer.IsEnabled)
        {
            return;
        }

        animationStopwatch.Restart();
        animationTimer.Start();
        UpdateAnimationFrame();
    }

    private void StopAnimation()
    {
        animationTimer.Stop();
        animationStopwatch.Reset();

        if (PickaxePath.RenderTransform is RotateTransform transform)
        {
            transform.Angle = AnimationKeyFrames[0].Angle;
        }
    }

    private void UpdateAnimationFrame()
    {
        if (PickaxePath.RenderTransform is not RotateTransform transform)
        {
            return;
        }

        var progress = animationStopwatch.Elapsed.TotalMilliseconds % AnimationDurationMilliseconds
            / AnimationDurationMilliseconds;

        for (var index = 1; index < AnimationKeyFrames.Length; index++)
        {
            var current = AnimationKeyFrames[index];
            if (progress > current.Cue)
            {
                continue;
            }

            var previous = AnimationKeyFrames[index - 1];
            var segmentProgress = (progress - previous.Cue) / (current.Cue - previous.Cue);
            var easedProgress = segmentProgress * segmentProgress * (3 - (2 * segmentProgress));
            transform.Angle = previous.Angle + ((current.Angle - previous.Angle) * easedProgress);
            return;
        }

        transform.Angle = AnimationKeyFrames[^1].Angle;
    }
}
