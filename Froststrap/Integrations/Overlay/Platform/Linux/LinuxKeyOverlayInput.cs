using Froststrap.Enums;
using Froststrap.Models.Overlay;

namespace Froststrap.Integrations.Overlay.Platform.Linux
{
    internal sealed class LinuxKeyOverlayInput : IKeyOverlayInput
    {
        private const uint SpaceKeycode = 65;
        private const uint MouseLeft = 1;
        private const uint MouseRight = 3;
        private const double CpsTimeConstantSeconds = 1.0;

        private readonly uint _keyUp, _keyLeft, _keyDown, _keyRight;

        private volatile bool _up, _left, _down, _right, _space, _mouseLeft, _mouseRight;

        private readonly object _scoreLock = new();
        private double _leftScore, _rightScore;
        private long _scoreTimestamp;
        private bool _isDisposed;

        internal LinuxKeyOverlayInput(int robloxPid, SoftKeyProfile profile)
        {
            _ = robloxPid;
            (_keyUp, _keyLeft, _keyDown, _keyRight) = ResolveKeycodes(profile);
        }

        public KeyOverlaySnapshot GetSnapshot()
        {
            long now = Environment.TickCount64;
            double leftCps;
            double rightCps;

            lock (_scoreLock)
            {
                DecayScores(now);
                leftCps = _leftScore / CpsTimeConstantSeconds;
                rightCps = _rightScore / CpsTimeConstantSeconds;
            }

            return new KeyOverlaySnapshot(
                _up, _left, _down, _right, _space,
                _mouseLeft, _mouseRight,
                leftCps, rightCps);
        }

        internal void Handle(LinuxOverlay.InputEvent e)
        {
            if (_isDisposed)
                return;

            switch (e.Kind)
            {
                case LinuxOverlay.KindKeyDown:
                    UpdateKey(e.Code, true);
                    break;
                case LinuxOverlay.KindKeyUp:
                    UpdateKey(e.Code, false);
                    break;
                case LinuxOverlay.KindButtonDown:
                    if (e.Code == MouseLeft) { _mouseLeft = true; AddClick(true); }
                    else if (e.Code == MouseRight) { _mouseRight = true; AddClick(false); }
                    break;
                case LinuxOverlay.KindButtonUp:
                    if (e.Code == MouseLeft) _mouseLeft = false;
                    else if (e.Code == MouseRight) _mouseRight = false;
                    break;
            }
        }

        private void UpdateKey(uint keycode, bool pressed)
        {
            if (keycode == _keyUp) _up = pressed;
            else if (keycode == _keyDown) _down = pressed;
            else if (keycode == _keyLeft) _left = pressed;
            else if (keycode == _keyRight) _right = pressed;
            else if (keycode == SpaceKeycode) _space = pressed;
        }

        private void DecayScores(long now)
        {
            if (_scoreTimestamp == 0)
            {
                _scoreTimestamp = now;
                return;
            }

            double elapsed = (now - _scoreTimestamp) / 1000.0;

            if (elapsed <= 0)
                return;

            double decay = Math.Exp(-elapsed / CpsTimeConstantSeconds);

            _leftScore *= decay;
            _rightScore *= decay;
            _scoreTimestamp = now;
        }

        private void AddClick(bool left)
        {
            long now = Environment.TickCount64;

            lock (_scoreLock)
            {
                DecayScores(now);

                if (left) _leftScore += 1;
                else _rightScore += 1;
            }
        }

        public void Dispose()
        {
            if (_isDisposed)
                return;

            _isDisposed = true;

            _up = _left = _down = _right = _space = _mouseLeft = _mouseRight = false;

            lock (_scoreLock)
            {
                _leftScore = 0;
                _rightScore = 0;
                _scoreTimestamp = 0;
            }

            GC.SuppressFinalize(this);
        }

        private static (uint Up, uint Left, uint Down, uint Right) ResolveKeycodes(SoftKeyProfile profile) => profile switch
        {
            SoftKeyProfile.AZERTY => (25, 38, 39, 40),
            SoftKeyProfile.ESDF => (26, 39, 40, 41),
            SoftKeyProfile.ArrowKeys => (111, 113, 116, 114),
            _ => (25, 38, 39, 40)
        };
    }
}