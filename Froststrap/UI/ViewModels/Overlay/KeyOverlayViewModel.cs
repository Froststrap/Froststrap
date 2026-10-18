// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

using Froststrap.Integrations;
using Froststrap.Models.Overlay;

using System.Globalization;

namespace Froststrap.UI.ViewModels.Overlay
{
    internal class KeyOverlayViewModel : NotifyPropertyChangedViewModel
    {
        private bool _showKeys;
        private bool _showMouse;
        private bool _showCpsLeft;
        private bool _showCpsRight;
        private bool _isMoveMode;

        private bool _isUpPressed;
        private bool _isLeftPressed;
        private bool _isDownPressed;
        private bool _isRightPressed;
        private bool _isSpacePressed;

        private bool _isMouseLeftPressed;
        private bool _isMouseRightPressed;

        private string _leftCps = "0.0";
        private string _rightCps = "0.0";

        public KeyOverlayViewModel()
        {
            RefreshOptions();
        }

        private string _upLabel = String.Empty;
        public string UpLabel
        {
            get => _upLabel;
            private set => SetProperty(ref _upLabel, value);
        }

        private string _leftLabel = String.Empty;
        public string LeftLabel
        {
            get => _leftLabel;
            private set => SetProperty(ref _leftLabel, value);
        }

        private string _downLabel = String.Empty;
        public string DownLabel
        {
            get => _downLabel;
            private set => SetProperty(ref _downLabel, value);
        }

        private string _rightLabel = String.Empty;
        public string RightLabel
        {
            get => _rightLabel;
            private set => SetProperty(ref _rightLabel, value);
        }

        public bool ShowKeys
        {
            get => _showKeys;
            private set => SetProperty(ref _showKeys, value);
        }

        public bool ShowMouse
        {
            get => _showMouse;
            private set => SetProperty(ref _showMouse, value);
        }

        public bool ShowCpsLeft
        {
            get => _showCpsLeft;
            private set => SetProperty(ref _showCpsLeft, value);
        }

        public bool ShowCpsRight
        {
            get => _showCpsRight;
            private set => SetProperty(ref _showCpsRight, value);
        }

        public bool ShowMouseArea => _showMouse || _showCpsLeft || _showCpsRight;

        public bool ShowCpsRow => _showCpsLeft || _showCpsRight;

        public bool IsMoveMode
        {
            get => _isMoveMode;
            set => SetProperty(ref _isMoveMode, value);
        }

        public bool IsUpPressed
        {
            get => _isUpPressed;
            private set => SetProperty(ref _isUpPressed, value);
        }

        public bool IsLeftPressed
        {
            get => _isLeftPressed;
            private set => SetProperty(ref _isLeftPressed, value);
        }

        public bool IsDownPressed
        {
            get => _isDownPressed;
            private set => SetProperty(ref _isDownPressed, value);
        }

        public bool IsRightPressed
        {
            get => _isRightPressed;
            private set => SetProperty(ref _isRightPressed, value);
        }

        public bool IsSpacePressed
        {
            get => _isSpacePressed;
            private set => SetProperty(ref _isSpacePressed, value);
        }

        public bool IsMouseLeftPressed
        {
            get => _isMouseLeftPressed;
            private set => SetProperty(ref _isMouseLeftPressed, value);
        }

        public bool IsMouseRightPressed
        {
            get => _isMouseRightPressed;
            private set => SetProperty(ref _isMouseRightPressed, value);
        }

        public string LeftCps
        {
            get => _leftCps;
            private set => SetProperty(ref _leftCps, value);
        }

        public string RightCps
        {
            get => _rightCps;
            private set => SetProperty(ref _rightCps, value);
        }

        public void RefreshOptions()
        {
            var labels = SoftKeyLayout.Labels(App.Settings.Prop.SoftKeyProfile);

            UpLabel = labels.Up;
            LeftLabel = labels.Left;
            DownLabel = labels.Down;
            RightLabel = labels.Right;

            ShowKeys = App.Settings.Prop.KeyOverlayShowKeys;
            ShowMouse = App.Settings.Prop.KeyOverlayShowMouse;
            ShowCpsLeft = App.Settings.Prop.KeyOverlayShowCpsLeft;
            ShowCpsRight = App.Settings.Prop.KeyOverlayShowCpsRight;

            OnPropertyChanged(nameof(ShowMouseArea));
            OnPropertyChanged(nameof(ShowCpsRow));
        }

        public void Apply(KeyOverlaySnapshot snapshot)
        {
            IsUpPressed = snapshot.Up;
            IsLeftPressed = snapshot.Left;
            IsDownPressed = snapshot.Down;
            IsRightPressed = snapshot.Right;
            IsSpacePressed = snapshot.Space;

            IsMouseLeftPressed = snapshot.MouseLeft;
            IsMouseRightPressed = snapshot.MouseRight;

            LeftCps = snapshot.LeftCps.ToString("F1", CultureInfo.InvariantCulture);
            RightCps = snapshot.RightCps.ToString("F1", CultureInfo.InvariantCulture);
        }

        public void ApplyPreview()
        {
            IsUpPressed = true;
            IsMouseLeftPressed = true;

            LeftCps = "6.4";
            RightCps = "6.4";
        }
    }
}
