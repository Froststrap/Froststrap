using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Markup.Xaml.MarkupExtensions;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.Input;
using Froststrap.UI.Elements.Base;
using IconPacks.Avalonia.Material;

namespace Froststrap.UI.Elements.Dialogs
{
    public partial class NotificationDialog : AvaloniaWindow
    {
        private readonly CancellationTokenSource _cts = new();
        private readonly Image? _iconPresenter;

        public NotificationDialog()
        {
            InitializeComponent();

            Width = 320;
            Height = 80;
            SystemDecorations = SystemDecorations.None;
            Topmost = true;
            ShowInTaskbar = false;
            ShowActivated = false;
            Focusable = false;
        }

        protected override void OnOpened(EventArgs e)
        {
            base.OnOpened(e);

            var screen = Screens.Primary?.WorkingArea ?? new PixelRect(0, 0, 1920, 1080);
            double scaling = Screens.Primary?.Scaling ?? 1.0;

            Position = new PixelPoint(
                screen.Right - (int)(Width * scaling) - 20,
                screen.Bottom - (int)(Height * scaling) - 20
            );
        }

        public NotificationDialog(string title, string message, string imagePath, int timeoutMs = 5000) : this()
        {
            _iconPresenter = new Image { Width = 40, Height = 40, VerticalAlignment = VerticalAlignment.Center };

            var titleBlock = new TextBlock
            {
                Text = title,
                FontWeight = FontWeight.SemiBold,
                FontSize = 13,
                TextTrimming = TextTrimming.CharacterEllipsis
            };
            titleBlock.Bind(TextBlock.ForegroundProperty, new DynamicResourceExtension("TextFillColorPrimaryBrush"));

            var messageBlock = new TextBlock
            {
                Text = message,
                FontSize = 11,
                TextWrapping = TextWrapping.Wrap,
                MaxLines = 2
            };
            messageBlock.Bind(TextBlock.ForegroundProperty, new DynamicResourceExtension("TextFillColorSecondaryBrush"));

            var mainBorder = new Border
            {
                CornerRadius = new CornerRadius(8),
                BorderThickness = new Thickness(1),
                BoxShadow = new BoxShadows(new BoxShadow { Blur = 10, Color = Color.Parse("#40000000"), OffsetY = 2 }),
                Padding = new Thickness(12)
            };

            mainBorder.Bind(Border.BackgroundProperty, new DynamicResourceExtension("SolidBackgroundFillColorBase"));
            mainBorder.Bind(Border.BorderBrushProperty, new DynamicResourceExtension("CardStrokeColorDefaultBrush"));

            var closeButton = new Button
            {
                [Grid.ColumnProperty] = 2,
                VerticalAlignment = VerticalAlignment.Top,
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Content = new PackIconMaterial { Kind = PackIconMaterialKind.Close, Width = 12, Height = 12 },
                Command = new RelayCommand(() => { _cts.Cancel(); Close(); })
            };

            closeButton.Bind(Button.ForegroundProperty, new DynamicResourceExtension("TextFillColorSecondaryBrush"));

            mainBorder.Child = new Grid
            {
                ColumnDefinitions = new ColumnDefinitions("40, *, Auto"),
                Children =
                {
                    _iconPresenter,
                    new StackPanel
                    {
                        [Grid.ColumnProperty] = 1,
                        VerticalAlignment = VerticalAlignment.Center,
                        Margin = new Thickness(12, 0),
                        Children = { titleBlock, messageBlock }
                    },
                    closeButton
                }
            };

            Content = mainBorder;

            Opened += (s, e) =>
            {
                var screen = Screens.Primary?.WorkingArea ?? new PixelRect(0, 0, 1920, 1080);
                double scaling = Screens.Primary?.Scaling ?? 1.0;

                Position = new PixelPoint(
                    screen.Right - (int)(Width * scaling) - 20,
                    screen.Bottom - (int)(Height * scaling) - 20
                );

                if (timeoutMs > 0) StartExpiryTimer(timeoutMs);
            };

            LoadImageAsync(imagePath);
        }

        private async void LoadImageAsync(string path)
        {
            try
            {
                Bitmap? bitmap = null;
                if (path.StartsWith("http", StringComparison.OrdinalIgnoreCase))
                {
                    var data = await App.HttpClient.GetByteArrayAsync(path, _cts.Token);
                    using var ms = new MemoryStream(data);
                    bitmap = new Bitmap(ms);
                }
                else if (!string.IsNullOrEmpty(path))
                {
                    bitmap = new Bitmap(AssetLoader.Open(new Uri(path)));
                }

                if (bitmap != null) Dispatcher.UIThread.Post(() => _iconPresenter!.Source = bitmap);
            }
            catch { /* Fail silently */ }
        }

        private async void StartExpiryTimer(int delay)
        {
            try
            {
                await Task.Delay(delay, _cts.Token);
                await Dispatcher.UIThread.InvokeAsync(() => { if (IsVisible) Close(); }, DispatcherPriority.MaxValue);
            }
            catch (TaskCanceledException) { }
        }
    }
}