// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using Avalonia.VisualTree;

using Froststrap.Backend;
using Froststrap.UI.Elements.Overlay.Controls;

using System.Runtime.InteropServices;
using System.Text;

namespace Froststrap.Integrations.Overlay.Platform.Linux
{
    internal sealed class LinuxOverlaySurface : IDisposable
    {
        private readonly Window _window;
        private readonly Control _root;
        private readonly Pointer _pointer = new Pointer(Pointer.GetNextFreeId(), PointerType.Mouse, true);

        private RenderTargetBitmap? _bitmap;
        private IntPtr _buffer;
        private int _bufferSize;
        private int _width;
        private int _height;

        private IInputElement? _focused;
        private IInputElement? _pressed;
        private Button? _pressedButton;
        private Point _lastPointer;

        public LinuxOverlaySurface(int id, Window window, Control root)
        {
            Id = id;
            _window = window;
            _root = root;
        }

        public int Id { get; }

        public Window Window => _window;

        public bool Tracked { get; init; }

        public bool PreferTextInput { get; init; }

        public Action<LinuxOverlaySurface>? FocusRequested { get; set; }

        public void Render(int width, int height)
        {
            if (width <= 0 || height <= 0)
                return;

            if (_bitmap is null || width != _width || height != _height)
                Resize(width, height);

            _root.InvalidateMeasure();
            _root.Measure(new Size(width, height));
            _root.Arrange(new Rect(0, 0, width, height));

            foreach (OverlayPanel panel in _root.GetVisualDescendants().OfType<OverlayPanel>())
            {
                if (!panel.IsVisible || double.IsNaN(panel.Width) || double.IsNaN(panel.Height))
                    continue;

                panel.InvalidateMeasure();
                panel.Measure(new Size(panel.Width, panel.Height));
                panel.Arrange(new Rect(Canvas.GetLeft(panel), Canvas.GetTop(panel), panel.Width, panel.Height));
            }

            if (_bitmap is null || _buffer == IntPtr.Zero)
                return;

            _bitmap.Render(_root);
            _bitmap.CopyPixels(new PixelRect(0, 0, _width, _height), _buffer, _bufferSize, _width * 4);
            LinuxOverlay.Submit(Id, _buffer, _width, _height, _width * 4);
        }

        private void Resize(int width, int height)
        {
            _bitmap?.Dispose();

            if (_buffer != IntPtr.Zero)
                Marshal.FreeHGlobal(_buffer);

            _width = width;
            _height = height;
            _bufferSize = width * height * 4;
            _buffer = Marshal.AllocHGlobal(_bufferSize);
            _bitmap = new RenderTargetBitmap(new PixelSize(width, height), new Vector(96, 96));
        }

        public void Handle(LinuxOverlay.InputEvent e)
        {
            var point = new Point(e.X, e.Y);

            switch (e.Kind)
            {
                case 2:
                    Motion(point, e.Modifiers);
                    break;

                case 3:
                    Press(point, e.Code, e.Modifiers);
                    break;

                case 4:
                    Release(point, e.Code, e.Modifiers);
                    break;

                case 5:
                    Wheel(point, e.Dx, e.Dy, e.Modifiers);
                    break;

                case 6:
                    KeyEvent(e.Code, e.Modifiers, true);
                    break;

                case 7:
                    KeyEvent(e.Code, e.Modifiers, false);
                    break;

                case 8:
                    Text(Decode(e.Text));
                    break;
            }
        }

        private void Motion(Point point, uint modifiers)
        {
            _lastPointer = point;

            if (HitTest(point) is not { } target)
                return;

            var properties = new PointerPointProperties(RawInputModifiers.None, PointerUpdateKind.Other);
            target.RaiseEvent(new PointerEventArgs(
                InputElement.PointerMovedEvent,
                target,
                _pointer,
                _root,
                point,
                (ulong)Environment.TickCount64,
                properties,
                Modifiers(modifiers)));
        }

        private void Press(Point point, uint button, uint modifiers)
        {
            if (_pressed is not null)
                return;

            if (!IsLeft(button))
                return;

            if (HitTest(point) is not { } target)
                return;

            _lastPointer = point;
            _pressed = target;
            _pressedButton = AncestorButton(target);

            if (_pressedButton is not null)
                return;

            Focus(target);

            var properties = new PointerPointProperties(RawInputModifiers.LeftMouseButton, PointerUpdateKind.LeftButtonPressed);
            target.RaiseEvent(new PointerPressedEventArgs(
                target,
                _pointer,
                _root,
                point,
                (ulong)Environment.TickCount64,
                properties,
                Modifiers(modifiers),
                1));
        }

        private void Release(Point point, uint button, uint modifiers)
        {
            if (_pressed is null)
                return;

            if (!IsLeft(button))
                return;

            _lastPointer = point;

            var target = _pointer.Captured ?? _pressed;
            Button? pressedButton = _pressedButton;

            _pressed = null;
            _pressedButton = null;

            if (pressedButton is not null)
            {
                _pointer.Capture(null);

                if (ReferenceEquals(AncestorButton(HitTest(point)), pressedButton))
                    Invoke(pressedButton);

                return;
            }

            var properties = new PointerPointProperties(RawInputModifiers.None, PointerUpdateKind.LeftButtonReleased);
            target.RaiseEvent(new PointerReleasedEventArgs(
                target,
                _pointer,
                _root,
                point,
                (ulong)Environment.TickCount64,
                properties,
                Modifiers(modifiers),
                MouseButton.Left));

            _pointer.Capture(null);
        }

        private void Wheel(Point point, float dx, float dy, uint modifiers)
        {
            if (dx == 0 && dy == 0)
                return;

            var target = HitTest(point) ?? _root;
            var properties = new PointerPointProperties(RawInputModifiers.None, PointerUpdateKind.Other);

            target.RaiseEvent(new PointerWheelEventArgs(
                target,
                _pointer,
                _root,
                point,
                (ulong)Environment.TickCount64,
                properties,
                Modifiers(modifiers),
                new Vector(dx, -dy)));
        }

        private void KeyEvent(uint code, uint modifiers, bool down)
        {
            var target = ResolveFocus();

            if (target is null)
                return;

            var routed = down ? InputElement.KeyDownEvent : InputElement.KeyUpEvent;
            var key = MapKey(code);

            if (key == Key.None)
                return;

            target.RaiseEvent(new KeyEventArgs
            {
                RoutedEvent = routed,
                Route = routed.RoutingStrategies,
                Source = target,
                Key = key,
                KeyModifiers = Modifiers(modifiers),
                PhysicalKey = PhysicalKey.None,
                KeyDeviceType = KeyDeviceType.Keyboard
            });
        }

        private void Text(string text)
        {
            if (text.Length == 0)
                return;

            var target = ResolveFocus();

            if (target is null)
                return;

            target.RaiseEvent(new TextInputEventArgs
            {
                RoutedEvent = InputElement.TextInputEvent,
                Route = InputElement.TextInputEvent.RoutingStrategies,
                Source = target,
                Text = text
            });
        }

        private void Focus(IInputElement target)
        {
            _focused = target;

            if (target is InputElement element)
            {
                try
                {
                    element.Focus();
                }
                catch (Exception ex)
                {
                    App.Logger.Warn($"Failed to focus an overlay control: {ex.Message}");
                }
            }

            FocusRequested?.Invoke(this);
        }

        private IInputElement? ResolveFocus()
        {
            if (PreferTextInput)
            {
                if (TopLevel.GetTopLevel(_root)?.FocusManager?.GetFocusedElement() is TextBox active && active.IsVisible)
                    return active;

                TextBox? box = _root.GetVisualDescendants().OfType<TextBox>().FirstOrDefault(text => text.IsVisible);

                if (box is not null)
                {
                    Focus(box);
                    return box;
                }
            }

            if (_focused is InputElement element && element.IsEffectivelyEnabled)
                return element;

            if (TopLevel.GetTopLevel(_root)?.FocusManager?.GetFocusedElement() is IInputElement current)
            {
                _focused = current;
                return current;
            }

            return _root;
        }

        private Control? HitTest(Point point) => Probe(_root, point);

        private Control? Probe(Visual visual, Point point)
        {
            if (visual is not Control control || !control.IsVisible || !control.IsHitTestVisible || !control.IsEffectivelyEnabled)
                return null;

            if (control.TranslatePoint(default, _root) is not { } origin)
                return null;

            var local = point - origin;
            bool inside = new Rect(control.Bounds.Size).Contains(local);

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

        private static Button? AncestorButton(IInputElement? element) =>
            (element as Visual)?.GetSelfAndVisualAncestors().OfType<Button>().FirstOrDefault();

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

        private static bool IsLeft(uint button) => button is 0 or 272;

        private static KeyModifiers Modifiers(uint modifiers)
        {
            var result = KeyModifiers.None;

            if ((modifiers & 1) != 0)
                result |= KeyModifiers.Alt;

            if ((modifiers & 2) != 0)
                result |= KeyModifiers.Control;

            if ((modifiers & 4) != 0)
                result |= KeyModifiers.Shift;

            if ((modifiers & 8) != 0)
                result |= KeyModifiers.Meta;

            return result;
        }

        private static Key MapKey(uint code) => code switch
        {
            0xFF1B => Key.Escape,
            0xFF0D => Key.Enter,
            0xFF09 => Key.Tab,
            0xFF08 => Key.Back,
            0xFF51 => Key.Left,
            0xFF52 => Key.Up,
            0xFF53 => Key.Right,
            0xFF54 => Key.Down,
            0xFF50 => Key.Home,
            0xFF57 => Key.End,
            0xFF55 => Key.PageUp,
            0xFF56 => Key.PageDown,
            0xFFFF => Key.Delete,
            0xFF63 => Key.Insert,
            0x0020 => Key.Space,
            _ when code is >= (uint)'a' and <= (uint)'z' => (Key)((int)Key.A + (int)(code - 'a')),
            _ when code is >= (uint)'A' and <= (uint)'Z' => (Key)((int)Key.A + (int)(code - 'A')),
            _ when code is >= (uint)'0' and <= (uint)'9' => (Key)((int)Key.D0 + (int)(code - '0')),
            _ => Key.None
        };

        private static string Decode(byte[]? bytes)
        {
            if (bytes is null)
                return String.Empty;

            int length = Array.IndexOf(bytes, (byte)0);

            if (length < 0)
                length = bytes.Length;

            return length == 0 ? String.Empty : Encoding.UTF8.GetString(bytes, 0, length);
        }

        public void Dispose()
        {
            _bitmap?.Dispose();
            _bitmap = null;

            if (_buffer != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(_buffer);
                _buffer = IntPtr.Zero;
            }

            _pointer.Dispose();
        }
    }
}