using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Froststrap.Backend;
using Froststrap.UI.Elements.Overlay.Controls;
using System.Runtime.InteropServices;

namespace Froststrap.Integrations.Overlay.Platform
{
    internal sealed class LinuxOverlayRenderer : IDisposable
    {
        private const int MaxEvents = 32;
        private static readonly int EventSize = Marshal.SizeOf<LinuxOverlay.InputEvent>();
        private static readonly TimeSpan MinFrame = TimeSpan.FromMilliseconds(16);
        private static readonly TimeSpan IdleFrame = TimeSpan.FromMilliseconds(250);

        private readonly Control root;
        private readonly DispatcherTimer timer;
        private readonly IntPtr eventBuffer = Marshal.AllocHGlobal(EventSize * MaxEvents);
        private readonly Pointer pointer = new(Pointer.GetNextFreeId(), PointerType.Mouse, true);

        private RenderTargetBitmap? bitmap;
        private IntPtr buffer;
        private int bufferSize;
        private int width;
        private int height;
        private string regionSignature = "";
        private bool dirty = true;
        private DateTime lastRender = DateTime.MinValue;

        private IInputElement? pressed;
        private Button? pressedButton;

        public LinuxOverlayRenderer(Control content)
        {
            root = content;
            timer = new DispatcherTimer(TimeSpan.FromMilliseconds(8), DispatcherPriority.Input, (_, _) => Tick());
        }

        public void Start()
        {
            timer.Start();
            Tick();
        }

        public void Dispose()
        {
            timer.Stop();
            bitmap?.Dispose();
            bitmap = null;

            if (buffer != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(buffer);
                buffer = IntPtr.Zero;
            }

            Marshal.FreeHGlobal(eventBuffer);
            pointer.Dispose();
        }

        private void Tick()
        {
            PollInput();

            var (w, h) = LinuxOverlay.Size();
            if (w <= 0 || h <= 0)
            {
                return;
            }

            var now = DateTime.UtcNow;
            var sinceRender = now - lastRender;

            if (w != width || h != height)
            {
                Resize(w, h);
                dirty = true;
            }

            if (!(dirty && sinceRender >= MinFrame) && sinceRender < IdleFrame)
            {
                return;
            }

            dirty = false;
            lastRender = now;
            Render();
            UpdateRegions();
        }

        private void Resize(int w, int h)
        {
            bitmap?.Dispose();

            if (buffer != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(buffer);
            }

            width = w;
            height = h;
            bufferSize = w * h * 4;
            buffer = Marshal.AllocHGlobal(bufferSize);
            bitmap = new RenderTargetBitmap(new PixelSize(w, h), new Vector(96, 96));
        }

        private void Render()
        {
            if (bitmap is null)
            {
                return;
            }

            root.InvalidateMeasure();
            root.Measure(new Size(width, height));
            root.Arrange(new Rect(0, 0, width, height));

            foreach (var panel in Panels())
            {
                if (!panel.IsVisible || double.IsNaN(panel.Width) || double.IsNaN(panel.Height))
                {
                    continue;
                }

                panel.InvalidateMeasure();
                panel.Measure(new Size(panel.Width, panel.Height));
                panel.Arrange(new Rect(Canvas.GetLeft(panel), Canvas.GetTop(panel), panel.Width, panel.Height));
            }

            bitmap.Render(root);

            var stride = width * 4;
            bitmap.CopyPixels(new PixelRect(0, 0, width, height), buffer, bufferSize, stride);
            LinuxOverlay.Submit(buffer, width, height, stride);
        }

        private IEnumerable<OverlayPanel> Panels() => root.GetVisualDescendants().OfType<OverlayPanel>();

        private void UpdateRegions()
        {
            List<int> rects = [];

            foreach (var control in root.GetVisualDescendants().OfType<Control>())
            {
                if (control.Name != "ChromeDock" && control is not OverlayPanel)
                {
                    continue;
                }

                if (!Shown(control) || control.Bounds.Width <= 0 || control.Bounds.Height <= 0)
                {
                    continue;
                }

                if (control.TranslatePoint(default, root) is not { } origin)
                {
                    continue;
                }

                rects.Add((int)origin.X);
                rects.Add((int)origin.Y);
                rects.Add((int)control.Bounds.Width);
                rects.Add((int)control.Bounds.Height);
            }

            var signature = string.Join(',', rects);
            if (signature == regionSignature)
            {
                return;
            }

            regionSignature = signature;
            LinuxOverlay.SetRegions([.. rects]);
        }

        private bool Shown(Visual visual)
        {
            for (Visual? current = visual; current is not null; current = current.GetVisualParent())
            {
                if (!current.IsVisible)
                {
                    return false;
                }

                if (ReferenceEquals(current, root))
                {
                    return true;
                }
            }

            return true;
        }

        private void PollInput()
        {
            int count = LinuxOverlay.PollInput(eventBuffer, MaxEvents);
            if (count <= 0)
            {
                return;
            }

            LinuxOverlay.InputEvent? lastMove = null;

            for (int i = 0; i < count; i++)
            {
                var e = Marshal.PtrToStructure<LinuxOverlay.InputEvent>(eventBuffer + i * EventSize);

                if (e.Kind == 2)
                {
                    lastMove = e;
                    continue;
                }

                if (lastMove is { } move)
                {
                    Move(move);
                    lastMove = null;
                }

                if (e.Kind == 3)
                {
                    Press(e);
                }
                else if (e.Kind == 4)
                {
                    Release(e);
                }
            }

            if (lastMove is { } tail)
            {
                Move(tail);
            }

            dirty = true;
        }

        private static ulong Timestamp() => (ulong)Environment.TickCount64;

        private void Move(LinuxOverlay.InputEvent e)
        {
            var point = new Point(e.X, e.Y);
            var target = pointer.Captured ?? pressed ?? HitTest(point);
            if (target is null)
            {
                return;
            }

            var props = new PointerPointProperties(RawInputModifiers.None, PointerUpdateKind.Other);
            target.RaiseEvent(new PointerEventArgs(InputElement.PointerMovedEvent, target, pointer, root, point, Timestamp(), props, KeyModifiers.None));
        }

        private void Press(LinuxOverlay.InputEvent e)
        {
            if (pressed is not null)
            {
                return;
            }

            var point = new Point(e.X, e.Y);
            if (HitTest(point) is not { } target)
            {
                return;
            }

            pressed = target;
            pressedButton = AncestorButton(target);

            if (pressedButton is not null)
            {
                return;
            }

            (target as InputElement)?.Focus();

            var props = new PointerPointProperties(RawInputModifiers.LeftMouseButton, PointerUpdateKind.LeftButtonPressed);
            target.RaiseEvent(new PointerPressedEventArgs(target, pointer, root, point, Timestamp(), props, KeyModifiers.None));
        }

        private void Release(LinuxOverlay.InputEvent e)
        {
            if (pressed is null)
            {
                return;
            }

            var point = new Point(e.X, e.Y);
            var target = pointer.Captured ?? pressed;
            var button = pressedButton;

            pressed = null;
            pressedButton = null;

            if (button is not null)
            {
                pointer.Capture(null);

                if (AncestorButton(HitTest(point)) == button)
                {
                    Invoke(button);
                }

                return;
            }

            var props = new PointerPointProperties(RawInputModifiers.None, PointerUpdateKind.LeftButtonReleased);
            target.RaiseEvent(new PointerReleasedEventArgs(target, pointer, root, point, Timestamp(), props, KeyModifiers.None, MouseButton.Left));
            pointer.Capture(null);
        }

        private static void Invoke(Button button)
        {
            if (!button.IsEffectivelyEnabled)
            {
                return;
            }

            if (button.Command is { } command)
            {
                if (command.CanExecute(button.CommandParameter))
                {
                    command.Execute(button.CommandParameter);
                }

                return;
            }

            button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        }

        private static Button? AncestorButton(IInputElement? element) =>
            (element as Visual)?.GetSelfAndVisualAncestors().OfType<Button>().FirstOrDefault();

        private Control? HitTest(Point point) => Probe(root, point);

        private Control? Probe(Visual visual, Point point)
        {
            if (visual is not Control control || !control.IsVisible || !control.IsHitTestVisible || !control.IsEffectivelyEnabled)
            {
                return null;
            }

            if (control.TranslatePoint(default, root) is not { } origin)
            {
                return null;
            }

            var local = point - origin;
            var inside = new Rect(control.Bounds.Size).Contains(new Point(local.X, local.Y));

            if (!inside && control.ClipToBounds)
            {
                return null;
            }

            foreach (var child in control.GetVisualChildren().Reverse())
            {
                if (Probe(child, point) is { } hit)
                {
                    return hit;
                }
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
