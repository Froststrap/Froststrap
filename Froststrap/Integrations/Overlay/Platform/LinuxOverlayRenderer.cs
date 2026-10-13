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
using System.Linq;
using System.Collections.Generic;
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

        private void LogPanel(string name)
        {
            var panel = root.GetVisualDescendants().OfType<Control>().FirstOrDefault(c => c.Name == name);
            if (panel is null)
            {
                App.Logger.Info($"LinuxOverlayRenderer: panel={name} found=False");
                return;
            }

            App.Logger.Info($"LinuxOverlayRenderer: panel={name} visible={panel.IsVisible} tag={panel.Tag} w={panel.Width} h={panel.Height} left={Canvas.GetLeft(panel)} top={Canvas.GetTop(panel)} desired={panel.DesiredSize} measureValid={panel.IsMeasureValid} attached={panel.IsAttachedToVisualTree()} parentAttached={panel.GetVisualParent()?.IsAttachedToVisualTree()}");
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

            root.InvalidateMeasure();
            root.InvalidateArrange();
            root.Measure(new Size(width, height));
            root.Arrange(new Rect(0, 0, width, height));

            foreach (var panel in root.GetVisualDescendants().OfType<Froststrap.UI.Elements.Overlay.Controls.OverlayPanel>())
            {
                if (!panel.IsVisible)
                {
                    continue;
                }

                panel.Measure(new Size(panel.Width, panel.Height));
                panel.Arrange(new Rect(Canvas.GetLeft(panel), Canvas.GetTop(panel), panel.Width, panel.Height));
            }

            bitmap.Render(root);

            var stride = width * 4;
            bitmap.CopyPixels(new PixelRect(0, 0, width, height), buffer, bufferSize, stride);
            LinuxOverlay.Submit(buffer, width, height, stride);
        }

        private void UpdateRegions()
        {
            var rects = new List<int>();

            foreach (var control in root.GetVisualDescendants().OfType<Control>())
            {
                bool isDock = control.Name == "ChromeDock";
                bool isPanel = control is Froststrap.UI.Elements.Overlay.Controls.OverlayPanel;
                if (!(isDock || isPanel) || !control.IsVisible || control.Bounds.Width <= 0 || control.Bounds.Height <= 0)
                {
                    continue;
                }

                var origin = control.TranslatePoint(new Point(0, 0), root);
                if (origin is null)
                {
                    continue;
                }

                rects.Add((int)origin.Value.X);
                rects.Add((int)origin.Value.Y);
                rects.Add((int)control.Bounds.Width);
                rects.Add((int)control.Bounds.Height);
            }

            var signature = string.Join(',', rects);
            if (signature == regionSignature)
            {
                return;
            }

            regionSignature = signature;
            LinuxOverlay.SetRegions(rects.ToArray());
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
            var timestamp = (ulong)Environment.TickCount64;

            if (e.Kind == 2)
            {
                var moveTarget = pointer.Captured ?? pressed ?? HitTest(point);
                if (moveTarget is not null)
                {
                    var moveProps = new PointerPointProperties(RawInputModifiers.None, PointerUpdateKind.Other);
                    moveTarget.RaiseEvent(new PointerEventArgs(InputElement.PointerMovedEvent, moveTarget, pointer, root, point, timestamp, moveProps, KeyModifiers.None));
                }

                return;
            }

            if (e.Kind == 3)
            {
                if (pressed is not null)
                {
                    return;
                }

                var target = HitTest(point);
                if (target is null)
                {
                    return;
                }

                pressed = target;
                var props = new PointerPointProperties(RawInputModifiers.LeftMouseButton, PointerUpdateKind.LeftButtonPressed);

                var isButton = (target as Visual)?.GetSelfAndVisualAncestors().OfType<Button>().Any() == true;
                if (!isButton)
                {
                    (target as InputElement)?.Focus();
                    target.RaiseEvent(new PointerPressedEventArgs(target, pointer, root, point, timestamp, props, KeyModifiers.None));
                }

                App.Logger.Info($"LinuxOverlayRenderer: pressed target={target.GetType().Name} button={isButton}");
                return;
            }

            if (pressed is null)
            {
                return;
            }

            var releaseTarget = pointer.Captured ?? pressed;
            pressed = null;

            var button = (releaseTarget as Visual)?.GetSelfAndVisualAncestors().OfType<Button>().FirstOrDefault();
            if (button is not null)
            {
                pointer.Capture(null);
                if (button.Command is { } command && command.CanExecute(button.CommandParameter))
                {
                    command.Execute(button.CommandParameter);
                    App.Logger.Info($"LinuxOverlayRenderer: invoked button={button.GetType().Name} param={button.CommandParameter}");
                }

                return;
            }

            var releaseProps = new PointerPointProperties(RawInputModifiers.None, PointerUpdateKind.LeftButtonReleased);
            releaseTarget.RaiseEvent(new PointerReleasedEventArgs(releaseTarget, pointer, root, point, timestamp, releaseProps, KeyModifiers.None, MouseButton.Left));
            pointer.Capture(null);
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
