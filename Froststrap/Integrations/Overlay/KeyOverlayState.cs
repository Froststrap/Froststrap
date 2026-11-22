// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

using Froststrap.Models.Overlay;

namespace Froststrap.Integrations.Overlay
{
    internal sealed class KeyOverlayState
    {
        private const double CpsTimeConstantSeconds = 1.0;

        private readonly uint _upCode;
        private readonly uint _leftCode;
        private readonly uint _downCode;
        private readonly uint _rightCode;
        private readonly uint _spaceCode;

        private readonly object _scoreLock = new();

        private volatile bool _up;
        private volatile bool _left;
        private volatile bool _down;
        private volatile bool _right;
        private volatile bool _space;
        private volatile bool _mouseLeft;
        private volatile bool _mouseRight;

        private double _leftScore;
        private double _rightScore;
        private long _scoreTimestamp;

        public KeyOverlayState(uint up, uint left, uint down, uint right, uint space)
        {
            _upCode = up;
            _leftCode = left;
            _downCode = down;
            _rightCode = right;
            _spaceCode = space;
        }

        public bool Tracks(uint code) =>
            code == _upCode || code == _downCode || code == _leftCode || code == _rightCode || code == _spaceCode;

        public void SetKey(uint code, bool pressed)
        {
            if (code == _upCode) _up = pressed;
            else if (code == _downCode) _down = pressed;
            else if (code == _leftCode) _left = pressed;
            else if (code == _rightCode) _right = pressed;
            else if (code == _spaceCode) _space = pressed;
        }

        public void SetMouse(bool left, bool pressed)
        {
            if (left) _mouseLeft = pressed;
            else _mouseRight = pressed;

            if (pressed)
                AddClick(left);
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
                _up,
                _left,
                _down,
                _right,
                _space,
                _mouseLeft,
                _mouseRight,
                leftCps,
                rightCps);
        }

        public void Clear()
        {
            _up = false;
            _left = false;
            _down = false;
            _right = false;
            _space = false;
            _mouseLeft = false;
            _mouseRight = false;

            lock (_scoreLock)
            {
                _leftScore = 0;
                _rightScore = 0;
                _scoreTimestamp = 0;
            }
        }

        private void AddClick(bool left)
        {
            long now = Environment.TickCount64;

            lock (_scoreLock)
            {
                DecayScores(now);

                if (left)
                    _leftScore += 1;
                else
                    _rightScore += 1;
            }
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
    }
}