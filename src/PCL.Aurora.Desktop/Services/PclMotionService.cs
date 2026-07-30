using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace PCL.Aurora.Desktop.Services;

// Motion structure, timings and easing formulas follow PCL-CE MyButton, MyListItem,
// MyPageRight and ModAnimation. The WPF animation engine is replaced by Avalonia animations.
internal static class PclMotionService
{
    private static readonly TimeSpan PressDuration = TimeSpan.FromMilliseconds(80);
    private static readonly TimeSpan ReleaseDuration = TimeSpan.FromMilliseconds(300);
    private static readonly TimeSpan PageExitDuration = TimeSpan.FromMilliseconds(70);
    private static readonly TimeSpan PageEnterOpacityDuration = TimeSpan.FromMilliseconds(100);
    private static readonly TimeSpan PageEnterTranslationDuration = TimeSpan.FromMilliseconds(350);
    private static readonly TimeSpan PageSwapDelay = TimeSpan.FromMilliseconds(30);
    private static readonly ConditionalWeakTable<TopLevel, RootMotionState> AttachedRoots = new();
    private static readonly ConditionalWeakTable<Control, SectionSwitchState> SectionSwitchStates = new();

    public static void Attach(TopLevel root)
    {
        if (AttachedRoots.TryGetValue(root, out _))
        {
            return;
        }

        var state = new RootMotionState();
        AttachedRoots.Add(root, state);
        root.AddHandler(
            InputElement.PointerPressedEvent,
            (_, args) => PressButton(state, args),
            RoutingStrategies.Tunnel,
            handledEventsToo: true);
        root.AddHandler(
            InputElement.PointerReleasedEvent,
            (_, _) => ReleasePressedControl(state),
            RoutingStrategies.Tunnel,
            handledEventsToo: true);
        if (root is Window window)
        {
            window.Deactivated += (_, _) => ReleasePressedControl(state);
        }
    }

    public static Task SwitchSectionsAsync(
        Control scope,
        IReadOnlyList<Control> sections,
        Control target,
        Action? applyTargetState = null,
        bool force = false)
    {
        var state = SectionSwitchStates.GetValue(scope, static _ => new SectionSwitchState());
        var previousTask = state.ActiveTask;
        state.Cancellation?.Cancel();

        var cancellation = new CancellationTokenSource();
        state.Cancellation = cancellation;
        var generation = ++state.Generation;
        var task = RunSectionSwitchAsync(
            state,
            previousTask,
            sections,
            target,
            applyTargetState,
            force,
            generation,
            cancellation.Token);
        state.ActiveTask = task;
        return task;
    }

    private static async Task RunSectionSwitchAsync(
        SectionSwitchState state,
        Task previousTask,
        IReadOnlyList<Control> sections,
        Control target,
        Action? applyTargetState,
        bool force,
        int generation,
        CancellationToken cancellationToken)
    {
        try
        {
            await ObserveCancellationAsync(previousTask);
            cancellationToken.ThrowIfCancellationRequested();
            NormalizeAnimatedControls(state);

            var current = sections.FirstOrDefault(section => section.IsVisible);
            if (!force && ReferenceEquals(current, target))
            {
                applyTargetState?.Invoke();
                return;
            }

            if (current is not null)
            {
                await AnimateSectionExitAsync(state, current, cancellationToken);
            }

            cancellationToken.ThrowIfCancellationRequested();
            foreach (var section in sections)
            {
                section.IsVisible = false;
            }

            applyTargetState?.Invoke();
            target.IsVisible = true;
            var enteringControls = PrepareSectionEnter(state, target);
            var swapDelay = PclMotionSettings.Scale(PageSwapDelay);
            if (swapDelay > TimeSpan.Zero)
            {
                await Task.Delay(swapDelay, cancellationToken);
            }
            await AnimateSectionEnterAsync(enteringControls, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // A newer navigation request owns normalization and the next visual state.
        }
        finally
        {
            if (state.Generation == generation)
            {
                NormalizeAnimatedControls(state);
            }
        }
    }

    private static async Task AnimateSectionExitAsync(
        SectionSwitchState state,
        Control section,
        CancellationToken cancellationToken)
    {
        var controls = GetAnimationControls(section);
        var animations = new List<Task>(controls.Count * 2);
        for (var index = 0; index < controls.Count; index++)
        {
            var control = controls[index];
            var transform = TrackAnimatedControl(state, control);
            var delay = TimeSpan.FromMilliseconds(Math.Min(index * 15, 40));
            var translationProperty = IsLeftPageSurface(control)
                ? TranslateTransform.XProperty
                : TranslateTransform.YProperty;
            var startOpacity = control.Opacity;
            animations.Add(RunAnimationAsync(
                control,
                Visual.OpacityProperty,
                startOpacity,
                0,
                PageExitDuration,
                delay,
                new LinearEasing(),
                cancellationToken));
            animations.Add(RunTranslationAsync(
                transform,
                translationProperty,
                -6,
                PageExitDuration,
                delay,
                new LinearEasing(),
                cancellationToken));
        }

        await Task.WhenAll(animations);
    }

    private static IReadOnlyList<Control> PrepareSectionEnter(
        SectionSwitchState state,
        Control section)
    {
        var controls = GetAnimationControls(section);
        foreach (var control in controls)
        {
            var transform = TrackAnimatedControl(state, control);
            control.Opacity = 0;
            transform.Transitions = null;
            if (IsLeftPageSurface(control))
            {
                transform.X = -25;
                transform.Y = 0;
            }
            else
            {
                transform.X = 0;
                transform.Y = -16;
            }
        }

        return controls;
    }

    private static async Task AnimateSectionEnterAsync(
        IReadOnlyList<Control> controls,
        CancellationToken cancellationToken)
    {
        var animations = new List<Task>(controls.Count * 2);
        for (var index = 0; index < controls.Count; index++)
        {
            var control = controls[index];
            var transform = (TranslateTransform)control.RenderTransform!;
            var delay = TimeSpan.FromMilliseconds(Math.Min(index * 25, 150));
            var isLeftPageSurface = IsLeftPageSurface(control);
            animations.Add(RunAnimationAsync(
                control,
                Visual.OpacityProperty,
                0,
                1,
                PageEnterOpacityDuration,
                delay,
                new PclFluentOutEasing(2),
                cancellationToken));
            animations.Add(RunTranslationAsync(
                transform,
                isLeftPageSurface ? TranslateTransform.XProperty : TranslateTransform.YProperty,
                0,
                isLeftPageSurface ? TimeSpan.FromMilliseconds(300) : PageEnterTranslationDuration,
                delay,
                isLeftPageSurface ? new PclPageLeftEnterEasing() : new PclPageEnterEasing(),
                cancellationToken));
        }

        await Task.WhenAll(animations);
    }

    private static Task RunAnimationAsync(
        Animatable target,
        AvaloniaProperty<double> property,
        double from,
        double to,
        TimeSpan duration,
        TimeSpan delay,
        Easing easing,
        CancellationToken cancellationToken)
    {
        duration = PclMotionSettings.Scale(duration);
        delay = PclMotionSettings.Scale(delay);
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

    private static async Task RunTranslationAsync(
        TranslateTransform transform,
        AvaloniaProperty<double> property,
        double to,
        TimeSpan duration,
        TimeSpan delay,
        Easing easing,
        CancellationToken cancellationToken)
    {
        duration = PclMotionSettings.Scale(duration);
        delay = PclMotionSettings.Scale(delay);
        transform.Transitions = null;
        if (duration <= TimeSpan.Zero)
        {
            transform.SetValue(property, to);
            return;
        }
        if (delay > TimeSpan.Zero)
        {
            await Task.Delay(delay, cancellationToken);
        }

        cancellationToken.ThrowIfCancellationRequested();
        transform.Transitions =
        [
            new DoubleTransition
            {
                Property = property,
                Duration = duration,
                Easing = easing,
            },
        ];

        await Dispatcher.UIThread.InvokeAsync(() => transform.SetValue(property, to), DispatcherPriority.Render);
        await Task.Delay(duration, cancellationToken);
    }

    private static IReadOnlyList<Control> GetAnimationControls(Control section)
    {
        if (IsMotionSurface(section))
        {
            return [section];
        }

        var controls = new List<Control>();
        CollectAnimationControls(section, controls, depth: 0);
        return controls.Count == 0 ? [section] : controls;
    }

    private static void CollectAnimationControls(Control control, List<Control> controls, int depth)
    {
        if (!control.IsVisible || controls.Count >= 48)
        {
            return;
        }

        if (depth > 0 && (IsMotionSurface(control) || control is TextBlock && depth <= 3))
        {
            controls.Add(control);
            return;
        }

        if (depth >= 2 && control is Border or ScrollViewer)
        {
            controls.Add(control);
            return;
        }

        switch (control)
        {
            case Panel panel:
                foreach (var child in panel.Children.OfType<Control>())
                {
                    CollectAnimationControls(child, controls, depth + 1);
                }

                break;
            case Decorator { Child: Control child }:
                CollectAnimationControls(child, controls, depth + 1);
                break;
            case ContentControl { Content: Control content }:
                CollectAnimationControls(content, controls, depth + 1);
                break;
        }
    }

    private static bool IsMotionSurface(Control control) =>
        control.Classes.Contains("pcl-card") ||
        control.Classes.Contains("setup-card") ||
        control.Classes.Contains("setup-hint") ||
        control.Classes.Contains("pcl-notice") ||
        control.Classes.Contains("version-category-card") ||
        control.Classes.Contains("download-loading-card");

    private static bool IsLeftPageSurface(Control control) =>
        control.Classes.Contains("pcl-page-left");

    private static TranslateTransform TrackAnimatedControl(SectionSwitchState state, Control control)
    {
        state.AnimatedControls.Add(control);
        if (control.RenderTransform is TranslateTransform transform)
        {
            return transform;
        }

        transform = new TranslateTransform();
        control.RenderTransform = transform;
        return transform;
    }

    private static void NormalizeAnimatedControls(SectionSwitchState state)
    {
        foreach (var control in state.AnimatedControls)
        {
            control.Opacity = 1;
            if (control.RenderTransform is TranslateTransform transform)
            {
                transform.Transitions = null;
                transform.X = 0;
                transform.Y = 0;
            }
        }

        state.AnimatedControls.Clear();
    }

    private static async Task ObserveCancellationAsync(Task task)
    {
        try
        {
            await task;
        }
        catch (OperationCanceledException)
        {
        }
    }

    private static void PressButton(RootMotionState state, PointerPressedEventArgs args)
    {
        if (!args.GetCurrentPoint(null).Properties.IsLeftButtonPressed ||
            FindInteractiveControl(args.Source) is not { IsEffectivelyEnabled: true } control)
        {
            return;
        }

        if (!ReferenceEquals(state.PressedControl, control))
        {
            ReleasePressedControl(state);
        }

        state.PressedControl = control;
        var scale = EnsureScale(control);
        scale.Transitions = CreateScaleTransitions(PclMotionSettings.Scale(PressDuration), new CubicEaseOut());
        var pressedScale = control is ListBoxItem || control.Bounds.Width >= 240 ? 0.98 : 0.955;
        scale.ScaleX = pressedScale;
        scale.ScaleY = pressedScale;
    }

    private static void ReleasePressedControl(RootMotionState state)
    {
        if (state.PressedControl is not { } control)
        {
            return;
        }

        state.PressedControl = null;
        if (control.RenderTransform is not ScaleTransform scale)
        {
            return;
        }

        scale.Transitions = CreateScaleTransitions(PclMotionSettings.Scale(ReleaseDuration), new QuadraticEaseOut());
        scale.ScaleX = 1;
        scale.ScaleY = 1;
    }

    private static Control? FindInteractiveControl(object? source)
    {
        if (source is Button or ListBoxItem)
        {
            return (Control)source;
        }

        return source is Visual visual
            ? visual.GetVisualAncestors().OfType<Control>()
                .FirstOrDefault(control => control is Button or ListBoxItem)
            : null;
    }

    private static ScaleTransform EnsureScale(Control control)
    {
        if (control.RenderTransform is ScaleTransform scale)
        {
            return scale;
        }

        scale = new ScaleTransform(1, 1);
        control.RenderTransformOrigin = RelativePoint.Center;
        control.RenderTransform = scale;
        return scale;
    }

    private static Transitions CreateScaleTransitions(TimeSpan duration, Easing easing) =>
    [
        new DoubleTransition
        {
            Property = ScaleTransform.ScaleXProperty,
            Duration = duration,
            Easing = easing,
        },
        new DoubleTransition
        {
            Property = ScaleTransform.ScaleYProperty,
            Duration = duration,
            Easing = easing,
        },
    ];

    private sealed class RootMotionState
    {
        public Control? PressedControl { get; set; }
    }

    private sealed class SectionSwitchState
    {
        public CancellationTokenSource? Cancellation { get; set; }

        public Task ActiveTask { get; set; } = Task.CompletedTask;

        public int Generation { get; set; }

        public HashSet<Control> AnimatedControls { get; } = [];
    }

    private sealed class PclFluentOutEasing(double power) : Easing
    {
        public override double Ease(double progress)
        {
            var value = Math.Clamp(progress, 0, 1);
            return 1 - Math.Pow(1 - value, power);
        }
    }

    private sealed class PclPageEnterEasing : Easing
    {
        public override double Ease(double progress)
        {
            var value = Math.Clamp(progress, 0, 1);
            var fluentProgress = Math.Clamp(value * 350d / 250d, 0, 1);
            var fluent = 1 - Math.Pow(1 - fluentProgress, 3);
            var back = 1 - Math.Pow(1 - value, 1.5) * Math.Cos(1.5 * Math.PI * value);
            return (5 * fluent + 11 * back) / 16;
        }
    }

    private sealed class PclPageLeftEnterEasing : Easing
    {
        public override double Ease(double progress)
        {
            var value = Math.Clamp(progress, 0, 1);
            var fluentProgress = Math.Clamp(value * 300d / 200d, 0, 1);
            var fluent = 1 - Math.Pow(1 - fluentProgress, 3);
            var back = 1 - Math.Pow(1 - value, 1.5) * Math.Cos(1.5 * Math.PI * value);
            return (5 * fluent + 20 * back) / 25;
        }
    }
}
