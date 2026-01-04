using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;

namespace Froststrap.UI.Utility
{
    public static class WindowScaling
    {
        private static Window? _mainWindow;

        public static void SetMainWindow(Window window)
        {
            _mainWindow = window;
        }

        public static double ScaleFactor
        {
            get
            {
                try
                {
                    if (_mainWindow == null)
                    {
                        var app = Application.Current;
                        if (app?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
                        {
                            _mainWindow = desktop.MainWindow;
                        }
                    }

                    if (_mainWindow == null) return 1.0;

                    var screen = _mainWindow.Screens.ScreenFromPoint(_mainWindow.Position);
                    return screen?.Scaling ?? 1.0;
                }
                catch
                {
                    return 1.0;
                }
            }
        }

        public static int GetScaledNumber(int number)
        {
            return (int)Math.Ceiling(number * ScaleFactor);
        }

        public static Size GetScaledSize(Size size)
        {
            return new Size(GetScaledNumber((int)size.Width), GetScaledNumber((int)size.Height));
        }

        public static PixelPoint GetScaledPoint(PixelPoint point)
        {
            return new PixelPoint(GetScaledNumber(point.X), GetScaledNumber(point.Y));
        }

        public static Thickness GetScaledThickness(Thickness thickness)
        {
            return new Thickness(
                GetScaledNumber((int)thickness.Left),
                GetScaledNumber((int)thickness.Top),
                GetScaledNumber((int)thickness.Right),
                GetScaledNumber((int)thickness.Bottom)
            );
        }

        public static System.Drawing.Size GetScaledDrawingSize(System.Drawing.Size size)
        {
            return new System.Drawing.Size(GetScaledNumber(size.Width), GetScaledNumber(size.Height));
        }

        public static System.Drawing.Point GetScaledDrawingPoint(System.Drawing.Point point)
        {
            return new System.Drawing.Point(GetScaledNumber(point.X), GetScaledNumber(point.Y));
        }
    }
}