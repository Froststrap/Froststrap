using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Froststrap.UI.Elements.Overlay.Controls;
using System.Runtime.InteropServices;

namespace Froststrap.Integrations.Overlay.Platform.Linux
{
    internal sealed class LinuxOverlayRenderer : IDisposable
    {
        private const int MaxEvents = 64;
        private static readonly int EventSize = Marshal.SizeOf<LinuxOverlay.InputEvent>();
        private static readonly TimeSpan MinFrame = TimeSpan.FromMilliseconds(16);
        private static readonly TimeSpan IdleFrame = TimeSpan.FromMilliseconds(250);

        private readonly Window _window;
        private readonly Control _root;
        private readonly int _surface;
        private readonly DispatcherTimer _timer;
        private readonly IntPtr _eventBuffer = Marshal.AllocHGlobal(EventSize * MaxEvents);
        private readonly Pointer _pointer = new(Pointer.GetNextFreeId(), PointerType.Mouse, true);

        private RenderTargetBitmap? _bitmap;
        private IntPtr _buffer;
        private int _bufferSize;
        private int _width;
        private int _height;
        private int _scale = 1;
        private string _regionSignature = "";
        private bool _dirty = true;
        private DateTime _lastRender = DateTime.MinValue;

        private IInputElement? _pressed;
        private Button? _pressedButton;
        private IInputElement? _focused;

        internal LinuxOverlayRenderer(Window window, Control content, int surface)
        {
            _window = window;
            _root = content;
            _surface = surface;
            _timer = new DispatcherTimer(TimeSpan.FromMilliseconds(8), DispatcherPriority.Input, (_, _) => Tick());
        }

        internal void Start()
        {
            _timer.Start();
            Tick();
        }

        public void Dispose()
        {
            _timer.Stop();
            _bitmap?.Dispose();
            _bitmap = null;

            if (_buffer != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(_buffer);
                _buffer = IntPtr.Zero;
            }

            Marshal.FreeHGlobal(_eventBuffer);
            _pointer.Dispose();
        }

        private void Tick()
        {
            PollInput();

            var (w, h) = LinuxOverlay.Size(_surface);

            if (w <= 0 || h <= 0)
                return;

            var now = DateTime.UtcNow;
            var sinceRender = now - _lastRender;

            int scale = Math.Max(1, LinuxOverlay.Scale(_surface));

            if (w != _width || h != _height || scale != _scale)
            {
                Resize(w, h, scale);
                _dirty = true;
            }

            if (!(_dirty && sinceRender >= MinFrame) && sinceRender < IdleFrame)
                return;

            _dirty = false;
            _lastRender = now;
            Render();
            UpdateRegions();
        }

        private void Resize(int w, int h, int scale)
        {
            _bitmap?.Dispose();

            if (_buffer != IntPtr.Zero)
                Marshal.FreeHGlobal(_buffer);

            _width = w;
            _height = h;
            _scale = scale;

            int pixelWidth = w * scale;
            int pixelHeight = h * scale;

            _bufferSize = pixelWidth * pixelHeight * 4;
            _buffer = Marshal.AllocHGlobal(_bufferSize);
            _bitmap = new RenderTargetBitmap(
                new PixelSize(pixelWidth, pixelHeight),
                new Vector(96 * scale, 96 * scale));
        }

        private void Render()
        {
            if (_bitmap is null)
                return;

            _root.InvalidateMeasure();
            _root.Measure(new Size(_width, _height));
            _root.Arrange(new Rect(0, 0, _width, _height));

            foreach (var panel in Panels())
            {
                if (!panel.IsVisible || double.IsNaN(panel.Width) || double.IsNaN(panel.Height))
                    continue;

                panel.InvalidateMeasure();
                panel.Measure(new Size(panel.Width, panel.Height));
                panel.Arrange(new Rect(Canvas.GetLeft(panel), Canvas.GetTop(panel), panel.Width, panel.Height));
            }

            _bitmap.Render(_root);

            int pixelWidth = _width * _scale;
            int pixelHeight = _height * _scale;
            var stride = pixelWidth * 4;
            _bitmap.CopyPixels(new PixelRect(0, 0, pixelWidth, pixelHeight), _buffer, _bufferSize, stride);
            LinuxOverlay.Submit(_surface, _buffer, pixelWidth, pixelHeight, stride);
        }

        private IEnumerable<OverlayPanel> Panels() => _root.GetVisualDescendants().OfType<OverlayPanel>();

        private void UpdateRegions()
        {
            if (_surface != LinuxOverlay.SurfaceGame)
                return;

            List<int> rects = [];

            foreach (var control in _root.GetVisualDescendants().OfType<Control>())
            {
                if (control.Name != "ChromeDock" && control is not OverlayPanel)
                    continue;

                if (!Shown(control) || control.Bounds.Width <= 0 || control.Bounds.Height <= 0)
                    continue;

                if (control.TranslatePoint(default, _root) is not { } origin)
                    continue;

                rects.Add((int)origin.X);
                rects.Add((int)origin.Y);
                rects.Add((int)control.Bounds.Width);
                rects.Add((int)control.Bounds.Height);
            }

            var signature = string.Join(',', rects);

            if (signature == _regionSignature)
                return;

            _regionSignature = signature;
            LinuxOverlay.SetInputRegion(_surface, [.. rects]);
        }

        private bool Shown(Visual visual)
        {
            for (Visual? current = visual; current is not null; current = current.GetVisualParent())
            {
                if (!current.IsVisible)
                    return false;

                if (ReferenceEquals(current, _root))
                    return true;
            }

            return true;
        }

        private void PollInput()
        {
            int count = LinuxOverlay.PollInput(_eventBuffer, MaxEvents);

            if (count <= 0)
                return;

            LinuxOverlay.InputEvent? lastMove = null;

            for (int i = 0; i < count; i++)
            {
                var e = Marshal.PtrToStructure<LinuxOverlay.InputEvent>(_eventBuffer + i * EventSize);

                if (e.Kind == LinuxOverlay.KindMotion)
                {
                    lastMove = e;
                    continue;
                }

                if (lastMove is { } move)
                {
                    Move(move);
                    lastMove = null;
                }

                switch (e.Kind)
                {
                    case LinuxOverlay.KindButtonDown: Press(e); break;
                    case LinuxOverlay.KindButtonUp: Release(e); break;
                    case LinuxOverlay.KindKeyDown: HandleKey(e); break;
                    case LinuxOverlay.KindText: HandleText(e); break;
                }
            }

            if (lastMove is { } tail)
                Move(tail);

            _dirty = true;
        }

        internal void HandleKey(LinuxOverlay.InputEvent e)
        {
            if (e.Kind != LinuxOverlay.KindKeyDown)
                return;

            var target = _focused ?? _pressed;

            if (target is null)
                return;

            Key key = KeysymToKey(e.Keysym);

            if (key == Key.None)
                return;

            var args = new KeyEventArgs
            {
                RoutedEvent = InputElement.KeyDownEvent,
                Key = key,
                Source = target
            };

            (target as InputElement)?.RaiseEvent(args);
            _dirty = true;
        }

        internal void HandleText(LinuxOverlay.InputEvent e)
        {
            if (e.Text is null)
                return;

            int len = Array.IndexOf(e.Text, (byte)0);

            if (len <= 0)
                len = e.Text.Length;

            string text = System.Text.Encoding.UTF8.GetString(e.Text, 0, len);

            if (string.IsNullOrEmpty(text))
                return;

            var target = _focused ?? _pressed;

            if (target is null)
                return;

            var args = new TextInputEventArgs
            {
                RoutedEvent = InputElement.TextInputEvent,
                Text = text,
                Source = target
            };

            (target as InputElement)?.RaiseEvent(args);
            _dirty = true;
        }

        private static Key KeysymToKey(uint keysym) => keysym switch
        {
            0xFF08 => Key.Back, 0xFF09 => Key.Tab, 0xFF0D => Key.Enter,
            0xFF1B => Key.Escape, 0xFF51 => Key.Left, 0xFF52 => Key.Up,
            0xFF53 => Key.Right, 0xFF54 => Key.Down, 0xFF50 => Key.Home,
            0xFF57 => Key.End, 0xFFFF => Key.Delete, 0x20 => Key.Space,
            _ => Key.None
        };

        private static ulong Timestamp() => (ulong)Environment.TickCount64;

        private void Move(LinuxOverlay.InputEvent e)
        {
            var point = new Point(e.X, e.Y);
            var target = _pointer.Captured ?? _pressed ?? HitTest(point);

            if (target is null)
                return;

            var props = new PointerPointProperties(RawInputModifiers.None, PointerUpdateKind.Other);
            target.RaiseEvent(new PointerEventArgs(
                InputElement.PointerMovedEvent, target, _pointer, _root,
                point, Timestamp(), props, KeyModifiers.None));
        }

        private void Press(LinuxOverlay.InputEvent e)
        {
            if (_pressed is not null)
                return;

            var point = new Point(e.X, e.Y);

            if (HitTest(point) is not { } target)
                return;

            _pressed = target;
            _focused = target;
            _pressedButton = AncestorButton(target);

            if (_pressedButton is not null)
                return;

            (target as InputElement)?.Focus();

            var props = new PointerPointProperties(RawInputModifiers.LeftMouseButton, PointerUpdateKind.LeftButtonPressed);
            target.RaiseEvent(new PointerPressedEventArgs(
                target, _pointer, _root, point, Timestamp(), props, KeyModifiers.None));
        }

        private void Release(LinuxOverlay.InputEvent e)
        {
            if (_pressed is null)
                return;

            var point = new Point(e.X, e.Y);
            var target = _pointer.Captured ?? _pressed;
            var button = _pressedButton;

            _pressed = null;
            _pressedButton = null;

            if (button is not null)
            {
                _pointer.Capture(null);

                if (AncestorButton(HitTest(point)) == button)
                    Invoke(button);

                return;
            }

            var props = new PointerPointProperties(RawInputModifiers.None, PointerUpdateKind.LeftButtonReleased);
            target.RaiseEvent(new PointerReleasedEventArgs(
                target, _pointer, _root, point, Timestamp(), props, KeyModifiers.None, MouseButton.Left));
            _pointer.Capture(null);
        }

        private static void Invoke(Button button)
        {
            if (!button.IsEffectivelyEnabled)
                return;

            if (button.Command is { } command)
            {
                if (command.CanExecute(button.CommandParameter))
                    command.Execute(button.CommandParameter);

                return;
            }

            button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        }

        private static Button? AncestorButton(IInputElement? element) =>
            (element as Visual)?.GetSelfAndVisualAncestors().OfType<Button>().FirstOrDefault();

        private Control? HitTest(Point point) => Probe(_root, point);

        private Control? Probe(Visual visual, Point point)
        {
            if (visual is not Control control || !control.IsVisible || !control.IsHitTestVisible || !control.IsEffectivelyEnabled)
                return null;

            if (control.TranslatePoint(default, _root) is not { } origin)
                return null;

            var local = point - origin;
            var inside = new Rect(control.Bounds.Size).Contains(new Point(local.X, local.Y));

            if (!inside && control.ClipToBounds)
                return null;

            foreach (var child in control.GetVisualChildren().Reverse())
            {
                if (Probe(child, point) is { } hit)
                    return hit;
            }

            return inside && Solid(control) ? control : null;
        }

        private static bool Solid(Control control) => control switch
        {
            Panel panel => panel.Background is not null,
            Border border => border.Background is not null,
            Avalonia.Controls.Presenters.ContentPresenter presenter => presenter.Background is not null,
            _ => true
        };
    }
}