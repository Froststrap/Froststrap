// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

using System;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;

using Froststrap.Models.Overlay;

namespace Froststrap.UI.Elements.Controls
{
    internal class HotkeyBox : TemplatedControl
    {
        public static readonly StyledProperty<KeyModifiers> ModifiersProperty =
            AvaloniaProperty.Register<HotkeyBox, KeyModifiers>(
                nameof(Modifiers),
                KeyModifiers.None,
                defaultBindingMode: BindingMode.TwoWay);

        public KeyModifiers Modifiers
        {
            get => GetValue(ModifiersProperty);
            set => SetValue(ModifiersProperty, value);
        }

        public static readonly StyledProperty<Key> KeyProperty =
            AvaloniaProperty.Register<HotkeyBox, Key>(
                nameof(Key),
                Key.None,
                defaultBindingMode: BindingMode.TwoWay);

        public Key Key
        {
            get => GetValue(KeyProperty);
            set => SetValue(KeyProperty, value);
        }

        public static readonly StyledProperty<bool> IsRecordingProperty =
            AvaloniaProperty.Register<HotkeyBox, bool>(
                nameof(IsRecording),
                false,
                defaultBindingMode: BindingMode.OneWayToSource);

        public bool IsRecording
        {
            get => GetValue(IsRecordingProperty);
            private set => SetValue(IsRecordingProperty, value);
        }

        private Button? _recorder;
        private TextBlock? _shortcutText;

        public event EventHandler? RecordingChanged;

        protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
        {
            base.OnApplyTemplate(e);

            DetachRecorder();

            _recorder = e.NameScope.Find<Button>("PART_Recorder");
            _shortcutText = e.NameScope.Find<TextBlock>("PART_ShortcutText");

            if (_recorder is not null)
            {
                _recorder.Click += RecorderClicked;
                _recorder.LostFocus += RecorderLostFocus;

                _recorder.AddHandler(KeyDownEvent, RecorderKeyDown, RoutingStrategies.Tunnel);
                _recorder.AddHandler(KeyUpEvent, RecorderKeyUp, RoutingStrategies.Tunnel);
            }

            ShowShortcut();
        }

        protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
        {
            StopRecording();

            base.OnDetachedFromVisualTree(e);
        }

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);

            if (change.Property == ModifiersProperty || change.Property == KeyProperty)
                ShowShortcut();
        }

        private void DetachRecorder()
        {
            if (_recorder is null)
                return;

            _recorder.Click -= RecorderClicked;
            _recorder.LostFocus -= RecorderLostFocus;
            _recorder.RemoveHandler(KeyDownEvent, RecorderKeyDown);
            _recorder.RemoveHandler(KeyUpEvent, RecorderKeyUp);

            _recorder = null;
            _shortcutText = null;
        }

        private void RecorderClicked(object? sender, RoutedEventArgs e)
        {
            if (IsRecording)
            {
                StopRecording();
                return;
            }

            IsRecording = true;

            _recorder?.Classes.Set("accent", true);
            SetText(Strings.Menu_Integrations_OverlayHotkey_Recording);

            _recorder?.Focus();

            RecordingChanged?.Invoke(this, EventArgs.Empty);
        }

        private void RecorderKeyDown(object? sender, KeyEventArgs e)
        {
            if (!IsRecording)
                return;

            e.Handled = true;

            Key key = e.Key;

            if (key == Key.Escape)
            {
                StopRecording();
                return;
            }

            KeyModifiers modifiers = e.KeyModifiers | OverlayHotkey.ModifierOf(key);

            if (OverlayHotkey.IsModifier(key))
            {
                SetText($"{OverlayHotkey.Describe(modifiers, Key.None)} + …");
                return;
            }

            if (!OverlayHotkey.IsAllowed(modifiers, key))
            {
                SetText(Strings.Menu_Integrations_OverlayHotkey_NeedsModifier);
                return;
            }

            Modifiers = modifiers;
            Key = key;

            StopRecording();
        }

        private void RecorderKeyUp(object? sender, KeyEventArgs e)
        {
            if (IsRecording)
                e.Handled = true;
        }

        private void RecorderLostFocus(object? sender, RoutedEventArgs e) => StopRecording();

        private void StopRecording()
        {
            bool wasRecording = IsRecording;

            IsRecording = false;

            _recorder?.Classes.Set("accent", false);

            ShowShortcut();

            if (wasRecording)
                RecordingChanged?.Invoke(this, EventArgs.Empty);
        }

        private void ShowShortcut()
        {
            if (IsRecording)
                return;

            SetText(OverlayHotkey.Describe(Modifiers, Key));
        }

        private void SetText(string text)
        {
            if (_shortcutText is not null)
                _shortcutText.Text = text;
        }
    }
}