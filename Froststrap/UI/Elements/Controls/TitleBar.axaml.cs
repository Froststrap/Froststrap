using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Media;
using Avalonia.Metadata;
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

        public string? Title { get => GetValue(TitleProperty); set => SetValue(TitleProperty, value); }
        public bool ShowMinimize { get => GetValue(ShowMinimizeProperty); set => SetValue(ShowMinimizeProperty, value); }
        public bool ShowMaximize { get => GetValue(ShowMaximizeProperty); set => SetValue(ShowMaximizeProperty, value); }
        public bool ShowClose { get => GetValue(ShowCloseProperty); set => SetValue(ShowCloseProperty, value); }
        public IImage? Icon { get => GetValue(IconProperty); set => SetValue(IconProperty, value); }
        public WindowState WindowState { get => GetValue(WindowStateProperty); set => SetValue(WindowStateProperty, value); }

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
                ctrl?.IsVisible = !OperatingSystem.IsMacOS();
            }

            _window.PropertyChanged += OnWindowPropertyChanged;

            _minBtn = e.NameScope.Find<IconButton>("PART_MinimizeButton");
            _maxBtn = e.NameScope.Find<IconButton>("PART_MaximizeButton");
            _closeBtn = e.NameScope.Find<IconButton>("PART_CloseButton");

            _minBtn?.Click += OnMinimizeClick;
            _maxBtn?.Click += OnMaximizeClick;
            _closeBtn?.Click += OnCloseClick;

            UpdateMaximizeIcon();
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

        private void OnMinimizeClick(object? sender, EventArgs e)
        {
            _window?.WindowState = WindowState.Minimized;
        }

        private void OnMaximizeClick(object? sender, EventArgs e)
        {
            if (_window == null) return;
            _window.WindowState = _window.WindowState == WindowState.Maximized
                ? WindowState.Normal
                : WindowState.Maximized;
        }

        private void OnCloseClick(object? sender, EventArgs e)
        {
            _window?.Close();
        }

        protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
        {
            base.OnDetachedFromVisualTree(e);

            _window?.PropertyChanged -= OnWindowPropertyChanged;
            _minBtn?.Click -= OnMinimizeClick;
            _maxBtn?.Click -= OnMaximizeClick;
            _closeBtn?.Click -= OnCloseClick;
        }
    }
}