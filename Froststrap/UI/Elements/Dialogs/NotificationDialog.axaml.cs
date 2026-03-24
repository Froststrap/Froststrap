using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
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
        private readonly Action? _onClickAction;
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

        public NotificationDialog(string title, string message, string imagePath, Action? onClick = null, int timeoutMs = 5000) : this()
        {
            _onClickAction = onClick;

            IBrush bg = Brushes.Black;
            IBrush border = Brushes.White;
            if (Application.Current?.TryFindResource("SystemControlBackgroundAltHighBrush", out var res1) == true) bg = (IBrush)res1!;
            if (Application.Current?.TryFindResource("SystemControlForegroundBaseLowBrush", out var res2) == true) border = (IBrush)res2!;

            _iconPresenter = new Image { Width = 40, Height = 40, VerticalAlignment = VerticalAlignment.Center };

            var mainBorder = new Border
            {
                Background = bg,
                CornerRadius = new CornerRadius(8),
                BorderBrush = border,
                BorderThickness = new Thickness(1),
                BoxShadow = new BoxShadows(new BoxShadow { Blur = 10, Color = Color.Parse("#50000000"), OffsetY = 2 }),
                Padding = new Thickness(12),
                Child = new Grid
                {
                    ColumnDefinitions = new ColumnDefinitions("50, *, Auto"),
                    Children =
                    {
                        _iconPresenter,
                        new StackPanel
                        {
                            [Grid.ColumnProperty] = 1,
                            VerticalAlignment = VerticalAlignment.Center,
                            Margin = new Thickness(12, 0),
                            Children =
                            {
                                new TextBlock { Text = title, FontWeight = FontWeight.SemiBold, FontSize = 13, TextTrimming = TextTrimming.CharacterEllipsis },
                                new TextBlock { Text = message, FontSize = 11, Opacity = 0.8, TextWrapping = TextWrapping.Wrap, MaxLines = 2 }
                            }
                        },
                        new Button
                        {
                            [Grid.ColumnProperty] = 2,
                            VerticalAlignment = VerticalAlignment.Top,
                            Background = Brushes.Transparent,
                            BorderThickness = new Thickness(0),
                            Content = new PackIconMaterial { Kind = PackIconMaterialKind.Close, Width = 12, Height = 12 },
                            Command = new RelayCommand(() => { _cts.Cancel(); Close(); })
                        }
                    }
                }
            };

            if (_onClickAction != null)
            {
                mainBorder.Cursor = new Cursor(StandardCursorType.Hand);
                mainBorder.PointerPressed += (s, e) => { _cts.Cancel(); _onClickAction.Invoke(); Close(); };
            }

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