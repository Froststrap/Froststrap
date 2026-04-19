using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Metadata;
using WindowState = Avalonia.Controls.WindowState;
using Avalonia.Media;
using FluentIcons.Common;

namespace Froststrap.UI.Elements.Controls
{
    public class TitleBar : TemplatedControl
    {
        public static readonly StyledProperty<string?> TitleProperty =
            AvaloniaProperty.Register<TitleBar, string?>(nameof(Title));

        public static readonly StyledProperty<bool> ShowMinimizeProperty =
            AvaloniaProperty.Register<TitleBar, bool>(nameof(ShowMinimize), true);

        public static readonly StyledProperty<bool> ShowMaximizeProperty =
            AvaloniaProperty.Register<TitleBar, bool>(nameof(ShowMaximize), true);

        public static readonly StyledProperty<bool> ShowCloseProperty =
            AvaloniaProperty.Register<TitleBar, bool>(nameof(ShowClose), true);

        public static readonly StyledProperty<IImage?> IconProperty =
            AvaloniaProperty.Register<TitleBar, IImage?>(nameof(Icon), defaultValue: null);

        public static readonly StyledProperty<object?> ContentProperty =
            AvaloniaProperty.Register<TitleBar, object?>(nameof(Content));

        public static readonly StyledProperty<WindowState> WindowStateProperty =
            AvaloniaProperty.Register<TitleBar, WindowState>(nameof(WindowState), defaultValue: WindowState.Normal);

        public string? Title { get => GetValue(TitleProperty); set => SetValue(TitleProperty, value); }
        public bool ShowMinimize { get => GetValue(ShowMinimizeProperty); set => SetValue(ShowMinimizeProperty, value); }
        public bool ShowMaximize { get => GetValue(ShowMaximizeProperty); set => SetValue(ShowMaximizeProperty, value); }
        public bool ShowClose { get => GetValue(ShowCloseProperty); set => SetValue(ShowCloseProperty, value); }
        public IImage? Icon { get => GetValue(IconProperty); set => SetValue(IconProperty, value); }
        public WindowState WindowState { get => GetValue(WindowStateProperty); set => SetValue(WindowStateProperty, value); }

        [Content]
        public object? Content { get => GetValue(ContentProperty); set => SetValue(ContentProperty, value); }

        protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
        {
            base.OnApplyTemplate(e);

            if (VisualRoot is not Window window) return;

            // hides title and close stuff for macOS.
            foreach (var it in new[] { "PART_LeftPanel", "PART_RightPanel" })
            {
                var ctrl = e.NameScope.Find<StackPanel>(it);
                if (ctrl != null) ctrl!.IsVisible = !OperatingSystem.IsMacOS();
            }

            window.PropertyChanged += (s, ev) =>
            {
                if (ev.Property.Name == nameof(Window.WindowState))
                {
                    var maxBtn = e.NameScope.Find<IconButton>("PART_MaximizeButton");
                    if (maxBtn != null)
                    {
                        maxBtn.Icon = window.WindowState == WindowState.Maximized 
                            ? Symbol.FullScreenMinimize 
                            : Symbol.FullScreenMaximize;
                    }
                    SetValue(WindowStateProperty, window.WindowState);
                }
            };

            var dragLayer = e.NameScope.Find<Control>("PART_DragLayer");
            if (dragLayer != null)
            {
                dragLayer.PointerPressed += (s, ev) =>
                {
                    if (ev.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
                    {
                        if (ev.ClickCount == 2 && ShowMaximize)
                        {
                            window.WindowState = window.WindowState == WindowState.Maximized
                                ? WindowState.Normal
                                : WindowState.Maximized;
                        }
                        else
                        {
                            window.BeginMoveDrag(ev);
                        }
                    }
                };
            }

            var minBtn = e.NameScope.Find<IconButton>("PART_MinimizeButton");
            if (minBtn != null)
                minBtn.Click += (s, ev) => window.WindowState = WindowState.Minimized;

            var maxBtn = e.NameScope.Find<IconButton>("PART_MaximizeButton");
            if (maxBtn != null)
            {
                maxBtn.Click += (s, ev) =>
                    window.WindowState = window.WindowState == WindowState.Maximized
                        ? WindowState.Normal
                        : WindowState.Maximized;
            }

            var closeBtn = e.NameScope.Find<IconButton>("PART_CloseButton");
            if (closeBtn != null)
                closeBtn.Click += (s, ev) => window.Close();
        }
    }
}
