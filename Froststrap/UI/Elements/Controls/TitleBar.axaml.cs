using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Metadata;
using Avalonia.VisualTree;
using LucideAvalonia.Enum;
using WindowState = Avalonia.Controls.WindowState;

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

        public static readonly StyledProperty<object?> LeftContentProperty =
            AvaloniaProperty.Register<TitleBar, object?>(nameof(LeftContent));

        public string? Title { get => GetValue(TitleProperty); set => SetValue(TitleProperty, value); }
        public bool ShowMinimize { get => GetValue(ShowMinimizeProperty); set => SetValue(ShowMinimizeProperty, value); }
        public bool ShowMaximize { get => GetValue(ShowMaximizeProperty); set => SetValue(ShowMaximizeProperty, value); }
        public bool ShowClose { get => GetValue(ShowCloseProperty); set => SetValue(ShowCloseProperty, value); }
        public IImage? Icon { get => GetValue(IconProperty); set => SetValue(IconProperty, value); }
        public WindowState WindowState { get => GetValue(WindowStateProperty); set => SetValue(WindowStateProperty, value); }
        public object? LeftContent { get => GetValue(LeftContentProperty); set => SetValue(LeftContentProperty, value); }

        [Content]
        public object? Content { get => GetValue(ContentProperty); set => SetValue(ContentProperty, value); }

        private Window? _window;
        private IconButton? _minBtn;
        private IconButton? _maxBtn;
        private IconButton? _closeBtn;

        protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
        {
            base.OnApplyTemplate(e);

            _window = TopLevel.GetTopLevel(this) as Window;
            if (_window == null) return;

            foreach (var it in new[] { "PART_LeftPanel", "PART_RightPanel" })
            {
                var ctrl = e.NameScope.Find<StackPanel>(it);
                if (ctrl != null)
                    ctrl.IsVisible = !OperatingSystem.IsMacOS();
            }

            _window.PropertyChanged += OnWindowPropertyChanged;

            _minBtn = e.NameScope.Find<IconButton>("PART_MinimizeButton");
            _maxBtn = e.NameScope.Find<IconButton>("PART_MaximizeButton");
            _closeBtn = e.NameScope.Find<IconButton>("PART_CloseButton");

            if (_minBtn != null) _minBtn.Click += OnMinimizeClick;
            if (_maxBtn != null) _maxBtn.Click += OnMaximizeClick;
            if (_closeBtn != null) _closeBtn.Click += OnCloseClick;

            UpdateMaximizeIcon();
        }

        protected override void OnPointerPressed(PointerPressedEventArgs e)
        {
            base.OnPointerPressed(e);

            if (e.Handled) return;
            if (e.GetCurrentPoint(this).Properties.PointerUpdateKind != PointerUpdateKind.LeftButtonPressed) return;

            if (IsInteractiveSource(e.Source)) return;

            _window?.BeginMoveDrag(e);
        }

        protected override void OnDoubleTapped(TappedEventArgs e)
        {
            base.OnDoubleTapped(e);

            if (e.Handled) return;
            if (_window == null) return;

            if (IsInteractiveSource(e.Source)) return;

            _window.WindowState = _window.WindowState == WindowState.Maximized
                ? WindowState.Normal
                : WindowState.Maximized;
        }

        private static bool IsInteractiveSource(object? source)
        {
            if (source is not Visual visual) return false;

            return visual.FindAncestorOfType<TextBox>(includeSelf: true) != null
                || visual.FindAncestorOfType<Button>(includeSelf: true) != null
                || visual.FindAncestorOfType<UserControl>(includeSelf: true) != null;
        }

        private void OnWindowPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
        {
            if (e.Property.Name == nameof(Window.WindowState))
            {
                SetValue(WindowStateProperty, _window!.WindowState);
                UpdateMaximizeIcon();
            }
        }

        private void UpdateMaximizeIcon()
        {
            if (_maxBtn != null && _window != null)
            {
                _maxBtn.Icon = _window.WindowState == WindowState.Maximized
                    ? LucideIconNames.Minimize
                    : LucideIconNames.Maximize;
            }
        }

        private void OnMinimizeClick(object? sender, RoutedEventArgs e)
        {
            if (_window != null)
                _window.WindowState = WindowState.Minimized;
        }

        private void OnMaximizeClick(object? sender, RoutedEventArgs e)
        {
            if (_window == null) return;
            _window.WindowState = _window.WindowState == WindowState.Maximized
                ? WindowState.Normal
                : WindowState.Maximized;
        }

        private void OnCloseClick(object? sender, RoutedEventArgs e)
        {
            _window?.Close();
        }

        protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
        {
            base.OnDetachedFromVisualTree(e);

            if (_window != null) _window.PropertyChanged -= OnWindowPropertyChanged;
            if (_minBtn != null) _minBtn.Click -= OnMinimizeClick;
            if (_maxBtn != null) _maxBtn.Click -= OnMaximizeClick;
            if (_closeBtn != null) _closeBtn.Click -= OnCloseClick;
        }
    }
}
