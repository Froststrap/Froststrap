using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;

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

        public static readonly StyledProperty<bool> CanMaximizeProperty =
            AvaloniaProperty.Register<TitleBar, bool>(nameof(CanMaximize), false);

        public static readonly StyledProperty<bool> ShowCloseProperty =
            AvaloniaProperty.Register<TitleBar, bool>(nameof(ShowClose), true);

        public static readonly StyledProperty<Avalonia.Media.IImage?> IconProperty =
            AvaloniaProperty.Register<TitleBar, Avalonia.Media.IImage?>(nameof(Icon));

        public string? Title { get => GetValue(TitleProperty); set => SetValue(TitleProperty, value); }
        public bool ShowMinimize { get => GetValue(ShowMinimizeProperty); set => SetValue(ShowMinimizeProperty, value); }
        public bool ShowMaximize { get => GetValue(ShowMaximizeProperty); set => SetValue(ShowMaximizeProperty, value); }
        public bool CanMaximize { get => GetValue(CanMaximizeProperty); set => SetValue(CanMaximizeProperty, value); }
        public bool ShowClose { get => GetValue(ShowCloseProperty); set => SetValue(ShowCloseProperty, value); }
        public Avalonia.Media.IImage? Icon { get => GetValue(IconProperty); set => SetValue(IconProperty, value); }

        protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
        {
            base.OnApplyTemplate(e);

            var window = VisualRoot as Window;
            if (window == null) return;

            var dragLayer = e.NameScope.Find<Control>("PART_DragLayer");
            if (dragLayer != null)
                dragLayer.PointerPressed += (s, ev) => window.BeginMoveDrag(ev);

            var minBtn = e.NameScope.Find<Button>("PART_MinimizeButton");
            if (minBtn != null)
                minBtn.Click += (s, ev) => window.WindowState = Avalonia.Controls.WindowState.Minimized;

            var maxBtn = e.NameScope.Find<Button>("PART_MaximizeButton");
            if (maxBtn != null)
                maxBtn.Click += (s, ev) =>
                    window.WindowState = window.WindowState == Avalonia.Controls.WindowState.Maximized ? Avalonia.Controls.WindowState.Normal : Avalonia.Controls.WindowState.Maximized;

            var closeBtn = e.NameScope.Find<Button>("PART_CloseButton");
            if (closeBtn != null)
                closeBtn.Click += (s, ev) => window.Close();
        }
    }
}