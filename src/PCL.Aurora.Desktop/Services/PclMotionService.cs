using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace PCL.Aurora.Desktop.Services;

// Timings and scale targets follow PCL-CE MyButton, MyListItem and MyPageRight.
internal static class PclMotionService
{
    private static readonly TimeSpan PressDuration = TimeSpan.FromMilliseconds(80);
    private static readonly TimeSpan ReleaseDuration = TimeSpan.FromMilliseconds(300);
    private static readonly TimeSpan PageOpacityDuration = TimeSpan.FromMilliseconds(140);
    private static readonly TimeSpan PageTranslationDuration = TimeSpan.FromMilliseconds(300);
    private static readonly ConditionalWeakTable<TopLevel, RootMotionState> AttachedRoots = new();
    private static readonly ConditionalWeakTable<Control, SectionMotionState> SectionStates = new();

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

    public static void AnimateSectionIn(Control section)
    {
        if (!section.IsVisible)
        {
            return;
        }

        var state = SectionStates.GetValue(section, static control => CreateSectionState(control));
        var generation = ++state.Generation;

        section.Transitions = null;
        state.Translate.Transitions = null;
        section.Opacity = 0.25;
        state.Translate.X = 10;

        section.Transitions = CreateOpacityTransitions();
        state.Translate.Transitions = CreateTranslateTransitions();
        Dispatcher.UIThread.Post(() =>
        {
            if (state.Generation != generation || !section.IsVisible)
            {
                return;
            }

            section.Opacity = 1;
            state.Translate.X = 0;
        }, DispatcherPriority.Render);
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
        scale.Transitions = CreateScaleTransitions(PressDuration, new CubicEaseOut());
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

        scale.Transitions = CreateScaleTransitions(ReleaseDuration, new QuadraticEaseOut());
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

    private static SectionMotionState CreateSectionState(Control control)
    {
        var translate = control.RenderTransform as TranslateTransform ?? new TranslateTransform();
        control.RenderTransform = translate;
        return new SectionMotionState(translate);
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

    private static Transitions CreateOpacityTransitions() =>
    [
        new DoubleTransition
        {
            Property = Visual.OpacityProperty,
            Duration = PageOpacityDuration,
            Easing = new CubicEaseOut(),
        },
    ];

    private static Transitions CreateTranslateTransitions() =>
    [
        new DoubleTransition
        {
            Property = TranslateTransform.XProperty,
            Duration = PageTranslationDuration,
            Easing = new BackEaseOut(),
        },
    ];

    private sealed class RootMotionState
    {
        public Control? PressedControl { get; set; }
    }

    private sealed class SectionMotionState(TranslateTransform translate)
    {
        public TranslateTransform Translate { get; } = translate;

        public int Generation { get; set; }
    }
}
