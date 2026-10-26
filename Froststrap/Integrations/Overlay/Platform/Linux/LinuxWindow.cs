using Avalonia;
using Avalonia.Controls;
using Froststrap.Models.Overlay;

namespace Froststrap.Integrations.Overlay.Platform.Linux
{
    internal sealed class LinuxWindow
    {
        internal enum WindowKind { Game, KeyOverlay, SearchBar }

        private readonly Window _window;
        private readonly WindowKind _kind;
        private ulong _xid;
        private int _surface;
        private bool _cloaked;
        private bool _clickThrough;
        private IReadOnlyList<OverlayRegion> _regions = [];

        internal LinuxWindow(Window window, WindowKind kind)
        {
            _window = window;
            _kind = kind;
            _surface = kind switch
            {
                WindowKind.KeyOverlay => LinuxOverlay.SurfaceKey,
                WindowKind.SearchBar => LinuxOverlay.SurfaceSearch,
                _ => LinuxOverlay.SurfaceGame
            };
        }

        internal int Surface => _surface;

        internal bool Attach()
        {
            if (!LinuxOverlay.UsesNativeWindow)
            {
                _window.ShowInTaskbar = false;
                _window.WindowDecorations = WindowDecorations.None;
                _window.CanResize = false;
                _window.Topmost = false;

                _clickThrough = _kind == WindowKind.KeyOverlay;
                LinuxOverlay.SetVisible(_surface, !_cloaked);

                return true;
            }

            var handle = _window.TryGetPlatformHandle();

            if (handle is null || handle.Handle == IntPtr.Zero)
                return false;

            _xid = (ulong)handle.Handle.ToInt64();

            if (_kind == WindowKind.KeyOverlay)
            {
                _clickThrough = true;
                ApplyInputRegion();
            }
            else
            {
                LinuxOverlay.ClearInputRegion((int)_xid);
            }

            return true;
        }

        internal void SyncGeometry(PixelPoint position, Size size)
        {
            if (LinuxOverlay.UsesNativeWindow)
                return;

            LinuxOverlay.SetSurfaceRect(
                _surface,
                position.X,
                position.Y,
                (int)Math.Ceiling(size.Width),
                (int)Math.Ceiling(size.Height));
        }

        internal void SetClickThrough(bool clickThrough)
        {
            if (_kind != WindowKind.KeyOverlay || _clickThrough == clickThrough)
                return;

            _clickThrough = clickThrough;
            ApplyInputRegion();
        }

        internal void SetCloaked(bool cloaked)
        {
            if (_cloaked == cloaked)
                return;

            _cloaked = cloaked;
            ApplyInputRegion();
            LinuxOverlay.SetVisible(_surface, !cloaked);
        }

        internal void SetRegion(IReadOnlyList<OverlayRegion> regions)
        {
            _regions = regions;
            ApplyInputRegion();
        }

        internal void ClearRegion()
        {
            _regions = [];
            ApplyInputRegion();
        }

        internal bool IsForeground() =>
            !_cloaked && _window.IsVisible && LinuxOverlay.IsForeground(Environment.ProcessId);

        internal void Raise()
        {
            if (_window.IsVisible)
                _window.Activate();
        }

        internal void Release()
        {
            if (_xid != 0 && LinuxOverlay.UsesNativeWindow)
                LinuxOverlay.ClearInputRegion((int)_xid);

            _xid = 0;
            _cloaked = false;
            _clickThrough = false;
            _regions = [];
        }

        private void ApplyInputRegion()
        {
            if (_xid == 0 && LinuxOverlay.UsesNativeWindow)
                return;

            int target = LinuxOverlay.UsesNativeWindow ? (int)_xid : _surface;

            if (_cloaked || _clickThrough)
            {
                LinuxOverlay.SetInputRegion(target, []);
                return;
            }

            if (_regions.Count == 0)
            {
                LinuxOverlay.ClearInputRegion(target);
                return;
            }

            var flat = new int[_regions.Count * 4];

            for (int i = 0; i < _regions.Count; i++)
            {
                OverlayRegion region = _regions[i];
                flat[i * 4] = region.Left;
                flat[i * 4 + 1] = region.Top;
                flat[i * 4 + 2] = region.Right - region.Left;
                flat[i * 4 + 3] = region.Bottom - region.Top;
            }

            LinuxOverlay.SetInputRegion(target, flat);
        }
    }
}