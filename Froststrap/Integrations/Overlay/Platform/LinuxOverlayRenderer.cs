// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
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
        private bool dirty = true;

        public LinuxOverlayRenderer(Control? content = null)
        {
            root = content ?? CreateTestContent();
            timer = new DispatcherTimer(
                TimeSpan.FromMilliseconds(250),
                DispatcherPriority.Background,
                (_, _) => tick());
        }

        public void Start()
        {
            timer.Start();
            tick();
        }

        public void Invalidate()
        {
            dirty = true;
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

        private void tick()
        {
            var (w, h) = LinuxOverlay.Size();
            if (w <= 0 || h <= 0)
            {
                return;
            }

            if (w != width || h != height)
            {
                resize(w, h);
            }

            if (!dirty)
            {
                return;
            }

            dirty = false;
            render();
        }

        private void resize(int w, int h)
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
            dirty = true;
        }

        private void render()
        {
            if (bitmap is null)
            {
                return;
            }

            root.Measure(new Size(width, height));
            root.Arrange(new Rect(0, 0, width, height));
            bitmap.Render(root);

            var stride = width * 4;
            bitmap.CopyPixels(new PixelRect(0, 0, width, height), buffer, bufferSize, stride);
            LinuxOverlay.Submit(buffer, width, height, stride);
        }
    }
}
