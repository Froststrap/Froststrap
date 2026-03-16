using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace Froststrap.UI.Elements.Controls
{
    public class Hyperlink : ContentControl
    {
        public static readonly StyledProperty<string?> UrlProperty =
            AvaloniaProperty.Register<Hyperlink, string?>(nameof(Url));

        public static readonly StyledProperty<string?> TextProperty =
            AvaloniaProperty.Register<Hyperlink, string?>(nameof(Text));

        public static readonly StyledProperty<IBrush> LinkForegroundProperty =
            AvaloniaProperty.Register<Hyperlink, IBrush>(nameof(LinkForeground), Brushes.LightBlue);

        public string? Url
        {
            get => GetValue(UrlProperty);
            set => SetValue(UrlProperty, value);
        }

        public string? Text
        {
            get => GetValue(TextProperty);
            set => SetValue(TextProperty, value);
        }

        public IBrush LinkForeground
        {
            get => GetValue(LinkForegroundProperty);
            set => SetValue(LinkForegroundProperty, value);
        }

        public Hyperlink() { }

        public Hyperlink(string text, string url)
        {
            Text = text;
            Url = url;
        }

        protected override void OnPointerPressed(Avalonia.Input.PointerPressedEventArgs e)
        {
            base.OnPointerPressed(e);
            if (!string.IsNullOrEmpty(Url))
            {
                Utilities.ShellExecute(Url);
                e.Handled = true;
            }
        }
    }
}