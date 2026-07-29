using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace PCL.Aurora.Desktop.Controls;

// Folding geometry and motion timings adapt PCL-CE MyCard.cs. Avalonia performs
// the layout animation locally so rapid repeated clicks remain interruptible.
public partial class PclCollapsibleCard : UserControl
{
    private const double CollapsedHeight = 40;
    private const double ArrowDurationMilliseconds = 250;

    public static readonly StyledProperty<string> TitleProperty =
        AvaloniaProperty.Register<PclCollapsibleCard, string>(nameof(Title), string.Empty);

    public static readonly StyledProperty<object?> BodyProperty =
        AvaloniaProperty.Register<PclCollapsibleCard, object?>(nameof(Body));

    public static readonly StyledProperty<bool> IsExpandedProperty =
        AvaloniaProperty.Register<PclCollapsibleCard, bool>(nameof(IsExpanded), true);

    private readonly DispatcherTimer animationTimer = new()
    {
        Interval = TimeSpan.FromMilliseconds(16),
    };

    private readonly Stopwatch animationStopwatch = new();
    private HeightAnimationProfile heightProfile;
    private double startHeight;
    private double startAngle;
    private double targetAngle;
    private bool initialized;

    private RotateTransform ArrowTransform => (RotateTransform)ArrowPath.RenderTransform!;

    public PclCollapsibleCard()
    {
        InitializeComponent();
        animationTimer.Tick += (_, _) => UpdateAnimationFrame();
        AttachedToVisualTree += (_, _) => ApplyStateImmediately();
        DetachedFromVisualTree += (_, _) => StopAnimation();
        initialized = true;
    }

    public string Title
    {
        get => GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public object? Body
    {
        get => GetValue(BodyProperty);
        set => SetValue(BodyProperty, value);
    }

    public bool IsExpanded
    {
        get => GetValue(IsExpandedProperty);
        set => SetValue(IsExpandedProperty, value);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (!initialized)
        {
            return;
        }

        if (change.Property == IsExpandedProperty)
        {
            if (this.IsAttachedToVisualTree())
            {
                Dispatcher.UIThread.Post(BeginStateAnimation, DispatcherPriority.Render);
            }
            else
            {
                ApplyStateImmediately();
            }
        }
        else if (change.Property == BodyProperty && IsExpanded && this.IsAttachedToVisualTree())
        {
            Dispatcher.UIThread.Post(ApplyStateImmediately, DispatcherPriority.Render);
        }
    }

    private void HeaderButtonClick(object? sender, RoutedEventArgs e)
    {
        IsExpanded = !IsExpanded;
    }

    private void BeginStateAnimation()
    {
        if (!this.IsAttachedToVisualTree())
        {
            ApplyStateImmediately();
            return;
        }

        var currentHeight = Math.Max(CollapsedHeight, Bounds.Height);
        var currentAngle = ArrowTransform.Angle;
        StopAnimation();

        if (IsExpanded)
        {
            BodyPresenter.IsVisible = true;
        }

        BodyPresenter.Measure(new Size(Math.Max(1, Bounds.Width - 2), double.PositiveInfinity));
        var targetHeight = IsExpanded
            ? Math.Max(CollapsedHeight, CollapsedHeight + BodyPresenter.DesiredSize.Height)
            : CollapsedHeight;

        startHeight = currentHeight;
        startAngle = currentAngle;
        targetAngle = IsExpanded ? 180 : 0;
        heightProfile = HeightAnimationProfile.Create(targetHeight - startHeight);
        Height = startHeight;

        animationStopwatch.Restart();
        animationTimer.Start();
        UpdateAnimationFrame();
    }

    private void UpdateAnimationFrame()
    {
        var elapsed = animationStopwatch.Elapsed.TotalMilliseconds;
        Height = startHeight + heightProfile.GetDistance(elapsed);

        var arrowProgress = Math.Clamp(elapsed / ArrowDurationMilliseconds, 0, 1);
        var arrowEased = 1 - Math.Pow(1 - arrowProgress, 5);
        ArrowTransform.Angle = startAngle + ((targetAngle - startAngle) * arrowEased);

        if (elapsed < Math.Max(heightProfile.DurationMilliseconds, ArrowDurationMilliseconds))
        {
            return;
        }

        CompleteAnimation();
    }

    private void CompleteAnimation()
    {
        StopAnimation();
        ArrowTransform.Angle = IsExpanded ? 180 : 0;
        BodyPresenter.IsVisible = IsExpanded;
        Height = IsExpanded ? double.NaN : CollapsedHeight;
    }

    private void ApplyStateImmediately()
    {
        StopAnimation();
        BodyPresenter.IsVisible = IsExpanded;
        ArrowTransform.Angle = IsExpanded ? 180 : 0;
        Height = IsExpanded ? double.NaN : CollapsedHeight;
    }

    private void StopAnimation()
    {
        animationTimer.Stop();
        animationStopwatch.Reset();
    }

    private readonly record struct HeightAnimationProfile(
        double Delta,
        double UniformDistance,
        double UniformDurationMilliseconds,
        double EaseDistance,
        double EaseDurationMilliseconds,
        double InitialSpeed)
    {
        public double DurationMilliseconds => UniformDurationMilliseconds + EaseDurationMilliseconds;

        public static HeightAnimationProfile Create(double delta)
        {
            var distance = Math.Abs(delta);
            if (distance <= 800)
            {
                return new(delta, 0, 0, distance, 150, 0);
            }

            double easeDistance;
            double easeDuration;
            double initialSpeed;
            if (delta < 0 && distance > 500)
            {
                easeDistance = 200;
                easeDuration = 150;
                initialSpeed = (distance - easeDistance) / 0.1;
            }
            else if (delta > 0 && distance > 3000)
            {
                initialSpeed = 5000;
                easeDistance = distance - (initialSpeed * 0.3);
                easeDuration = 400;
            }
            else
            {
                easeDistance = 150;
                easeDuration = 200;
                initialSpeed = 4000;
            }

            var uniformDistance = Math.Max(0, distance - easeDistance);
            var uniformDuration = uniformDistance / initialSpeed * 1000;
            return new(delta, uniformDistance, uniformDuration, easeDistance, easeDuration, initialSpeed);
        }

        public double GetDistance(double elapsedMilliseconds)
        {
            var direction = Math.Sign(Delta);
            var absoluteDistance = Math.Abs(Delta);
            if (absoluteDistance == 0)
            {
                return 0;
            }

            if (UniformDurationMilliseconds == 0)
            {
                var progress = Math.Clamp(elapsedMilliseconds / EaseDurationMilliseconds, 0, 1);
                return direction * EaseDistance * (1 - Math.Pow(1 - progress, 5));
            }

            if (elapsedMilliseconds <= UniformDurationMilliseconds)
            {
                return direction * UniformDistance * (elapsedMilliseconds / UniformDurationMilliseconds);
            }

            var easeProgress = Math.Clamp(
                (elapsedMilliseconds - UniformDurationMilliseconds) / EaseDurationMilliseconds,
                0,
                1);
            var normalizedInitialSpeed = InitialSpeed * (EaseDurationMilliseconds / 1000) / EaseDistance;
            var alpha = Math.Max(0, normalizedInitialSpeed - 1);
            var eased = alpha == 0
                ? easeProgress
                : (alpha + 1) * easeProgress / (1 + (alpha * easeProgress));
            return direction * (UniformDistance + (EaseDistance * eased));
        }
    }
}
