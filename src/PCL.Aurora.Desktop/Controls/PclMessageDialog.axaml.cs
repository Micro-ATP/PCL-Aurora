using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using PCL.Aurora.Desktop.Services;

namespace PCL.Aurora.Desktop.Controls;

public sealed record PclMessageDialogOptions(
    string Title,
    string Message,
    string PrimaryButtonText = "确定",
    string? SecondaryButtonText = "取消",
    string? TertiaryButtonText = null,
    bool IsWarning = false,
    Control? Content = null,
    Control? InitialFocus = null,
    bool EnterConfirms = true);

public partial class PclMessageDialog : UserControl
{
    private readonly SemaphoreSlim dialogGate = new(1, 1);
    private TaskCompletionSource<int>? completion;
    private PclMessageDialogOptions? currentOptions;
    private IInputElement? previousFocus;
    private bool isClosing;

    private TransformGroup DialogTransforms => (TransformGroup)DialogSurface.RenderTransform!;

    private RotateTransform DialogRotateTransform => (RotateTransform)DialogTransforms.Children[0];

    private TranslateTransform DialogTranslateTransform => (TranslateTransform)DialogTransforms.Children[1];

    public PclMessageDialog()
    {
        InitializeComponent();
        AddHandler(KeyDownEvent, DialogKeyDown, RoutingStrategies.Tunnel, handledEventsToo: true);
    }

    public bool IsDialogOpen => IsVisible && completion is not null;

    public async Task<int> ShowAsync(PclMessageDialogOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        await dialogGate.WaitAsync();
        try
        {
            currentOptions = options;
            completion = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
            isClosing = false;
            previousFocus = TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement();
            Configure(options);
            PrepareOpenState();
            IsVisible = true;
            FocusInitialElement(options);
            await AnimateOpenAsync();
            return await completion.Task;
        }
        finally
        {
            completion = null;
            currentOptions = null;
            dialogGate.Release();
        }
    }

    private void Configure(PclMessageDialogOptions options)
    {
        DialogTitle.Text = options.Title;
        DialogMessage.Text = options.Message;
        DialogMessage.IsVisible = !string.IsNullOrWhiteSpace(options.Message);
        DialogContent.Content = options.Content;
        DialogContent.IsVisible = options.Content is not null;

        ConfigureButton(PrimaryButton, options.PrimaryButtonText);
        ConfigureButton(SecondaryButton, options.SecondaryButtonText);
        ConfigureButton(TertiaryButton, options.TertiaryButtonText);
        PrimaryButton.Classes.Set("danger", options.IsWarning);

        var accent = Brush.Parse(options.IsWarning ? "#FF4C4C" : "#0B5BCB");
        DialogTitle.Foreground = accent;
        TitleDivider.Background = accent;
        Scrim.Background = Brush.Parse(options.IsWarning ? "#8C500000" : "#5A000000");
    }

    private static void ConfigureButton(Button button, string? text)
    {
        button.Content = text;
        button.IsVisible = !string.IsNullOrEmpty(text);
    }

    private void PrepareOpenState()
    {
        Scrim.Opacity = 0;
        DialogSurface.Opacity = 0;
        DialogTranslateTransform.Transitions = null;
        DialogRotateTransform.Transitions = null;
        DialogTranslateTransform.Y = 40;
        DialogRotateTransform.Angle = -4;
    }

    private async Task AnimateOpenAsync()
    {
        var cancellationToken = CancellationToken.None;
        await Task.WhenAll(
            AnimateAsync(Scrim, Visual.OpacityProperty, 0, 1, 200, 0, new CubicEaseOut(), cancellationToken),
            AnimateAsync(DialogSurface, Visual.OpacityProperty, 0, 1, 120, 60, new CubicEaseOut(), cancellationToken),
            TransitionAsync(DialogTranslateTransform, TranslateTransform.YProperty, 40, 0, 300, 60, new PclMessageBackOutEasing(), cancellationToken),
            TransitionAsync(DialogRotateTransform, RotateTransform.AngleProperty, -4, 0, 300, 60, new CubicEaseOut(), cancellationToken));
    }

    private async Task CloseAsync(int result)
    {
        if (isClosing || completion is null)
        {
            return;
        }

        isClosing = true;
        var completionToResolve = completion;
        var cancellationToken = CancellationToken.None;
        await Task.WhenAll(
            AnimateAsync(Scrim, Visual.OpacityProperty, Scrim.Opacity, 0, 200, 30, new CubicEaseOut(), cancellationToken),
            AnimateAsync(DialogSurface, Visual.OpacityProperty, DialogSurface.Opacity, 0, 80, 20, new LinearEasing(), cancellationToken),
            TransitionAsync(DialogTranslateTransform, TranslateTransform.YProperty, DialogTranslateTransform.Y, 20, 150, 0, new CubicEaseOut(), cancellationToken),
            TransitionAsync(DialogRotateTransform, RotateTransform.AngleProperty, DialogRotateTransform.Angle, 6, 150, 0, new CubicEaseIn(), cancellationToken));

        IsVisible = false;
        DialogContent.Content = null;
        previousFocus?.Focus();
        previousFocus = null;
        completionToResolve.TrySetResult(result);
    }

    private void FocusInitialElement(PclMessageDialogOptions options)
    {
        var target = options.InitialFocus ?? PrimaryButton;
        Dispatcher.UIThread.Post(() =>
        {
            target.Focus(NavigationMethod.Tab, KeyModifiers.None);
            if (target is TextBox textBox)
            {
                textBox.SelectionStart = textBox.Text?.Length ?? 0;
                textBox.SelectionEnd = textBox.SelectionStart;
            }
        }, DispatcherPriority.Input);
    }

    private void DialogKeyDown(object? sender, KeyEventArgs e)
    {
        if (!IsDialogOpen || isClosing || currentOptions is null || e.KeyModifiers != KeyModifiers.None)
        {
            return;
        }

        if (e.Key == Key.Enter && currentOptions.EnterConfirms)
        {
            e.Handled = true;
            _ = CloseAsync(1);
            return;
        }

        if (e.Key == Key.Escape)
        {
            e.Handled = true;
            _ = CloseAsync(TertiaryButton.IsVisible ? 3 : SecondaryButton.IsVisible ? 2 : 1);
        }
    }

    private async void PrimaryButtonClick(object? sender, RoutedEventArgs e) => await CloseAsync(1);

    private async void SecondaryButtonClick(object? sender, RoutedEventArgs e) => await CloseAsync(2);

    private async void TertiaryButtonClick(object? sender, RoutedEventArgs e) => await CloseAsync(3);

    private static Task AnimateAsync(
        Animatable target,
        AvaloniaProperty<double> property,
        double from,
        double to,
        double durationMilliseconds,
        double delayMilliseconds,
        Easing easing,
        CancellationToken cancellationToken)
    {
        var duration = PclMotionSettings.Scale(TimeSpan.FromMilliseconds(durationMilliseconds));
        var delay = PclMotionSettings.Scale(TimeSpan.FromMilliseconds(delayMilliseconds));
        if (duration <= TimeSpan.Zero)
        {
            target.SetValue(property, to);
            return Task.CompletedTask;
        }

        var animation = new Animation
        {
            Duration = duration,
            Delay = delay,
            Easing = easing,
            FillMode = FillMode.Both,
            PlaybackBehavior = PlaybackBehavior.Always,
            Children =
            {
                new KeyFrame
                {
                    Cue = new Cue(0),
                    Setters = { new Setter(property, from) },
                },
                new KeyFrame
                {
                    Cue = new Cue(1),
                    Setters = { new Setter(property, to) },
                },
            },
        };
        return animation.RunAsync(target, cancellationToken);
    }

    private static async Task TransitionAsync(
        Animatable target,
        AvaloniaProperty<double> property,
        double from,
        double to,
        double durationMilliseconds,
        double delayMilliseconds,
        Easing easing,
        CancellationToken cancellationToken)
    {
        var duration = PclMotionSettings.Scale(TimeSpan.FromMilliseconds(durationMilliseconds));
        var delay = PclMotionSettings.Scale(TimeSpan.FromMilliseconds(delayMilliseconds));
        target.Transitions = null;
        target.SetValue(property, from);
        if (duration <= TimeSpan.Zero)
        {
            target.SetValue(property, to);
            return;
        }

        if (delay > TimeSpan.Zero)
        {
            await Task.Delay(delay, cancellationToken);
        }

        target.Transitions =
        [
            new DoubleTransition
            {
                Property = property,
                Duration = duration,
                Easing = easing,
            },
        ];
        await Dispatcher.UIThread.InvokeAsync(
            () => target.SetValue(property, to),
            DispatcherPriority.Render);
        await Task.Delay(duration, cancellationToken);
    }

    private sealed class PclMessageBackOutEasing : Easing
    {
        public override double Ease(double progress)
        {
            var value = Math.Clamp(progress, 0, 1) - 1;
            const double overshoot = 0.55;
            return 1 + (overshoot + 1) * value * value * value + overshoot * value * value;
        }
    }
}
