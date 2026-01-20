using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using System.Windows.Input;

namespace Froststrap.UI.Elements.Controls
{
    class Hyperlink : InlineUIContainer
    {
        private static readonly IBrush DefaultForeground = Brushes.LightBlue;
        private static readonly IBrush HoverForeground = new SolidColorBrush(Color.FromRgb(173, 216, 230));

        private readonly TextBlock _linkTextBlock;
        private readonly Button _button;

        public static readonly StyledProperty<string> UrlProperty =
            AvaloniaProperty.Register<Hyperlink, string>(nameof(Url));

        public static readonly StyledProperty<string> TextProperty =
            AvaloniaProperty.Register<Hyperlink, string>(nameof(Text));

        public static readonly StyledProperty<IBrush> LinkForegroundProperty =
            AvaloniaProperty.Register<Hyperlink, IBrush>(nameof(LinkForeground), DefaultForeground);

        public static readonly StyledProperty<ICommand?> CommandProperty =
            AvaloniaProperty.Register<Hyperlink, ICommand?>(nameof(Command));

        public static readonly StyledProperty<object?> CommandParameterProperty =
            AvaloniaProperty.Register<Hyperlink, object?>(nameof(CommandParameter));

        public string Url
        {
            get => GetValue(UrlProperty);
            set => SetValue(UrlProperty, value);
        }

        public string Text
        {
            get => GetValue(TextProperty);
            set => SetValue(TextProperty, value);
        }

        public IBrush LinkForeground
        {
            get => GetValue(LinkForegroundProperty);
            set => SetValue(LinkForegroundProperty, value);
        }

        public ICommand? Command
        {
            get => GetValue(CommandProperty);
            set => SetValue(CommandProperty, value);
        }

        public object? CommandParameter
        {
            get => GetValue(CommandParameterProperty);
            set => SetValue(CommandParameterProperty, value);
        }

        public Hyperlink()
        {
            _linkTextBlock = new TextBlock();
            _button = new Button
            {
                Content = _linkTextBlock,
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Padding = new Thickness(0),
                Margin = new Thickness(0),
                Cursor = new Cursor(StandardCursorType.Hand),
                HorizontalContentAlignment = HorizontalAlignment.Left,
                VerticalContentAlignment = VerticalAlignment.Center
            };

            _button.PointerEntered += OnPointerEntered;
            _button.PointerExited += OnPointerExited;
            _button.Click += OnClick;

            _linkTextBlock.Bind(TextBlock.TextProperty, this.GetObservable(TextProperty));
            _linkTextBlock.Bind(TextBlock.ForegroundProperty, this.GetObservable(LinkForegroundProperty));
            _button.Bind(Button.CommandProperty, this.GetObservable(CommandProperty));
            _button.Bind(Button.CommandParameterProperty, this.GetObservable(CommandParameterProperty));

            var underline = new TextDecoration
            {
                Location = TextDecorationLocation.Underline
            };

            var textDecorationCollection = new TextDecorationCollection();
            textDecorationCollection.Add(underline);
            _linkTextBlock.TextDecorations = textDecorationCollection;

            Child = _button;
        }

        public Hyperlink(string text, string url) : this()
        {
            Text = text;
            Url = url;
            ToolTip.SetTip(_button, url);
        }

        private void OnPointerEntered(object? sender, PointerEventArgs e)
        {
            _linkTextBlock.Foreground = HoverForeground;
        }

        private void OnPointerExited(object? sender, PointerEventArgs e)
        {
            _linkTextBlock.Foreground = LinkForeground;
        }

        private void OnClick(object? sender, RoutedEventArgs e)
        {
            if (Command != null && Command.CanExecute(CommandParameter))
            {
                Command.Execute(CommandParameter);
            }
            else if (!string.IsNullOrEmpty(Url))
            {
                Utilities.ShellExecute(Url);
                e.Handled = true;
            }
        }

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);

            if (change.Property == UrlProperty)
            {
                ToolTip.SetTip(_button, change.NewValue as string ?? "");
            }
            else if (change.Property == TextProperty)
            {
                if (string.IsNullOrEmpty(ToolTip.GetTip(_button) as string) && !string.IsNullOrEmpty(Url))
                {
                    ToolTip.SetTip(_button, Url);
                }
            }
        }
    }
}