// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Froststrap.Backend;
using System.Runtime.InteropServices;

namespace Froststrap.Integrations.Overlay.Platform
{
    internal sealed class LinuxOverlayRenderer : IDisposable
    {
        private readonly Control root;
        private readonly DispatcherTimer timer;
        private RenderTargetBitmap? bitmap;
        private IntPtr buffer;
        private int bufferSize;
        private int width;
        private int height;
        private string regionSignature = "";
        private readonly IntPtr eventBuffer = Marshal.AllocHGlobal(24 * 32);
        private readonly Pointer pointer = new(Pointer.GetNextFreeId(), PointerType.Mouse, true);
        private IInputElement? pressed;

        public LinuxOverlayRenderer(Control? content = null)
        {
            root = content ?? CreateTestContent();
            timer = new DispatcherTimer(
                TimeSpan.FromMilliseconds(50),
                DispatcherPriority.Background,
                (_, _) => Tick());
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

        private static Grid CreateTestContent()
        {
            var dock = new Border
            {
                Background = new SolidColorBrush(Color.Parse("#E61E1E2E")),
                CornerRadius = new CornerRadius(16),
                Padding = new Thickness(24, 14),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Bottom,
                Margin = new Thickness(0, 0, 0, 48),
                Child = new TextBlock
                {
                    Text = "Froststrap overlay",
                    Foreground = Brushes.White,
                    FontSize = 22
                }
            };

            return new Grid
            {
                Background = new SolidColorBrush(Color.Parse("#33000000")),
                Children = { dock }
            };
        }

        private void Tick()
        {
            var (w, h) = LinuxOverlay.Size();
            if (w <= 0 || h <= 0)
            {
                return;
            }

            if (w != width || h != height)
            {
                Resize(w, h);
            }

            Render();
            UpdateRegions();
            PollInput();
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

            if (root is Avalonia.Controls.Primitives.TemplatedControl templated)
            {
                templated.ApplyTemplate();
            }

            root.Measure(new Size(width, height));
            root.Arrange(new Rect(0, 0, width, height));
            bitmap.Render(root);

            var stride = width * 4;
            bitmap.CopyPixels(new PixelRect(0, 0, width, height), buffer, bufferSize, stride);
            LinuxOverlay.Submit(buffer, width, height, stride);
        }

        private void UpdateRegions()
        {
            var dock = root.GetVisualDescendants().OfType<Control>().FirstOrDefault(c => c.Name == "ChromeDock");
            if (dock is null || !dock.IsVisible)
            {
                return;
            }

            var origin = dock.TranslatePoint(new Point(0, 0), root);
            if (origin is null)
            {
                return;
            }

            int[] rects = [(int)origin.Value.X, (int)origin.Value.Y, (int)dock.Bounds.Width, (int)dock.Bounds.Height];
            var signature = string.Join(',', rects);
            if (signature == regionSignature)
            {
                return;
            }

            regionSignature = signature;
            LinuxOverlay.SetRegions(rects);
            App.Logger.Info($"LinuxOverlayRenderer: regions={signature}");
        }

        private void PollInput()
        {
            int count = LinuxOverlay.PollInput(eventBuffer, 32);
            for (int i = 0; i < count; i++)
            {
                var e = Marshal.PtrToStructure<LinuxOverlay.InputEvent>(eventBuffer + i * 24);
                Dispatch(e);
            }
        }

        private void Dispatch(LinuxOverlay.InputEvent e)
        {
            if (e.Kind < 2 || e.Kind > 4)
            {
                return;
            }

            var point = new Point(e.X, e.Y);
            var target = HitTest(point);
            var props = new PointerPointProperties(
                e.Kind == 3 ? RawInputModifiers.LeftMouseButton : RawInputModifiers.None,
                e.Kind == 3 ? PointerUpdateKind.LeftButtonPressed : PointerUpdateKind.LeftButtonReleased);

            if (e.Kind == 3 && target is not null)
            {
                pressed = target;
                pointer.Capture(target);
                var args = new PointerPressedEventArgs(target, pointer, root, point, (ulong)Environment.TickCount64, props, KeyModifiers.None);
                target.RaiseEvent(args);
                App.Logger.Info($"LinuxOverlayRenderer: pressed target={target.GetType().Name}");
            }
            else if (e.Kind == 4 && pressed is not null)
            {
                var releaseTarget = pressed;
                pointer.Capture(null);
                pressed = null;

                var button = (releaseTarget as Visual)?.GetSelfAndVisualAncestors().OfType<Button>().FirstOrDefault();
                if (button?.Command is { } command && command.CanExecute(button.CommandParameter))
                {
                    command.Execute(button.CommandParameter);
                    App.Logger.Info($"LinuxOverlayRenderer: invoked button={button.GetType().Name} param={button.CommandParameter}");
                }
            }
        }

        private IInputElement? HitTest(Point point)
        {
            IInputElement? best = null;
            foreach (var visual in root.GetVisualDescendants())
            {
                if (visual is not Control control || !control.IsVisible || !control.IsHitTestVisible || !control.IsEffectivelyEnabled)
                {
                    continue;
                }

                var origin = control.TranslatePoint(new Point(0, 0), root);
                if (origin is null)
                {
                    continue;
                }

                var rect = new Rect(origin.Value, control.Bounds.Size);
                if (rect.Contains(point))
                {
                    best = control;
                }
            }

            return best;
        }
    }
}
