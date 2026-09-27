using Avalonia;
using Avalonia.Animation;
using Avalonia.Controls;
using Avalonia.Threading;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.WindowsAndMessaging;

namespace Froststrap.UI.Elements.Overlay
{
    internal partial class OverlayToast : Window
    {
        private const int WS_EX_TRANSPARENT = 0x00000020;
        private const int WS_EX_TOOLWINDOW = 0x00000080;
        private const int WS_EX_NOACTIVATE = 0x08000000;

        private const double Inset = 12;

        private static readonly TimeSpan Dwell = TimeSpan.FromSeconds(6);
        private static readonly TimeSpan FadeIn = TimeSpan.FromMilliseconds(150);
        private static readonly TimeSpan FadeOut = TimeSpan.FromMilliseconds(300);

        private readonly DispatcherTimer _timer;

        private HWND _hwnd;

        public IntPtr Handle => _hwnd;

        public OverlayToast()
        {
            InitializeComponent();

            Transitions =
            [
                new DoubleTransition
                {
                    Property = OpacityProperty,
                    Duration = FadeIn
                }
            ];

            _timer = new DispatcherTimer { Interval = Dwell };
            _timer.Tick += (_, _) => Dismiss();
        }

        public void Present(string title, string message, Rect gameBounds)
        {
            ToastTitle.Text = title;
            ToastMessage.Text = message;

            _timer.Stop();

            SetFadeDuration(TimeSpan.Zero);
            Opacity = 0;

            Card.Measure(Size.Infinity);

            double widthDip = Card.DesiredSize.Width;
            double heightDip = Card.DesiredSize.Height;

            Width = widthDip;
            Height = heightDip;

            if (!IsVisible)
                Show();

            Place(gameBounds, widthDip, heightDip);

            SetFadeDuration(FadeIn);
            Opacity = 1;

            _timer.Start();
        }

        public async void Dismiss()
        {
            _timer.Stop();

            SetFadeDuration(FadeOut);
            Opacity = 0;

            await Task.Delay(FadeOut);

            if (Opacity == 0)
                Hide();
        }

        private void SetFadeDuration(TimeSpan duration)
        {
            if (Transitions?.OfType<DoubleTransition>().FirstOrDefault(t => t.Property == OpacityProperty) is { } transition)
                transition.Duration = duration;
        }

        private void Place(Rect gameBounds, double widthDip, double heightDip)
        {
            double scaling = RenderScaling;

            int pixelWidth = (int)(widthDip * scaling);
            int pixelHeight = (int)(heightDip * scaling);
            int insetPixels = (int)(Inset * scaling);

            Position = new PixelPoint(
                (int)gameBounds.Right - pixelWidth - insetPixels,
                (int)gameBounds.Bottom - pixelHeight - insetPixels);
        }

        protected override void OnOpened(EventArgs e)
        {
            base.OnOpened(e);

            if (!OperatingSystem.IsWindows())
                return;

            var handle = TryGetPlatformHandle();
            if (handle is null)
                return;

            _hwnd = new HWND(handle.Handle);

            int exStyle = PInvoke.GetWindowLong(_hwnd, WINDOW_LONG_PTR_INDEX.GWL_EXSTYLE);

            _ = PInvoke.SetWindowLong(_hwnd, WINDOW_LONG_PTR_INDEX.GWL_EXSTYLE,
                exStyle | WS_EX_TRANSPARENT | WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE);
        }
    }
}