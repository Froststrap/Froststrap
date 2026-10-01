// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Media;
using LucideAvalonia.Enum;
using System;
using System.Windows.Input;
using WindowState = Avalonia.Controls.WindowState;

namespace Froststrap.UI.Elements.Controls
{
    internal class TitleBar : TemplatedControl
    {
        public static readonly StyledProperty<string?> TitleProperty =
            AvaloniaProperty.Register<TitleBar, string?>(nameof(Title));

        public static readonly StyledProperty<bool> ShowMinimizeProperty =
            AvaloniaProperty.Register<TitleBar, bool>(nameof(ShowMinimize), true);

        public static readonly StyledProperty<bool> ShowMaximizeProperty =
            AvaloniaProperty.Register<TitleBar, bool>(nameof(ShowMaximize), true);

        public static readonly StyledProperty<bool> ShowCloseProperty =
            AvaloniaProperty.Register<TitleBar, bool>(nameof(ShowClose), true);

        public static readonly StyledProperty<bool> ShowPinProperty =
            AvaloniaProperty.Register<TitleBar, bool>(nameof(ShowPin), defaultValue: false);

        public static readonly StyledProperty<IImage?> IconProperty =
            AvaloniaProperty.Register<TitleBar, IImage?>(nameof(Icon), defaultValue: null);

        public static readonly StyledProperty<LucideIconNames?> LucideIconProperty =
            AvaloniaProperty.Register<TitleBar, LucideIconNames?>(nameof(LucideIcon), defaultValue: null);

        public static readonly StyledProperty<ICommand?> CloseCommandProperty =
            AvaloniaProperty.Register<TitleBar, ICommand?>(nameof(CloseCommand));

        public static readonly StyledProperty<object?> CloseCommandParameterProperty =
            AvaloniaProperty.Register<TitleBar, object?>(nameof(CloseCommandParameter));

        public static readonly StyledProperty<bool> IsPinnedProperty =
            AvaloniaProperty.Register<TitleBar, bool>(nameof(IsPinned), defaultValue: false);

        public static readonly StyledProperty<ICommand?> PinCommandProperty =
            AvaloniaProperty.Register<TitleBar, ICommand?>(nameof(PinCommand));

        public static readonly StyledProperty<object?> PinCommandParameterProperty =
            AvaloniaProperty.Register<TitleBar, object?>(nameof(PinCommandParameter));

        public static readonly StyledProperty<WindowState> WindowStateProperty =
            AvaloniaProperty.Register<TitleBar, WindowState>(nameof(WindowState), defaultValue: WindowState.Normal);

        public string? Title { get => GetValue(TitleProperty); set => SetValue(TitleProperty, value); }
        public bool ShowMinimize { get => GetValue(ShowMinimizeProperty); set => SetValue(ShowMinimizeProperty, value); }
        public bool ShowMaximize { get => GetValue(ShowMaximizeProperty); set => SetValue(ShowMaximizeProperty, value); }
        public bool ShowClose { get => GetValue(ShowCloseProperty); set => SetValue(ShowCloseProperty, value); }
        public bool ShowPin { get => GetValue(ShowPinProperty); set => SetValue(ShowPinProperty, value); }
        public IImage? Icon { get => GetValue(IconProperty); set => SetValue(IconProperty, value); }
        public LucideIconNames? LucideIcon { get => GetValue(LucideIconProperty); set => SetValue(LucideIconProperty, value); }
        public ICommand? CloseCommand { get => GetValue(CloseCommandProperty); set => SetValue(CloseCommandProperty, value); }
        public object? CloseCommandParameter { get => GetValue(CloseCommandParameterProperty); set => SetValue(CloseCommandParameterProperty, value); }
        public bool IsPinned { get => GetValue(IsPinnedProperty); set => SetValue(IsPinnedProperty, value); }
        public ICommand? PinCommand { get => GetValue(PinCommandProperty); set => SetValue(PinCommandProperty, value); }
        public object? PinCommandParameter { get => GetValue(PinCommandParameterProperty); set => SetValue(PinCommandParameterProperty, value); }
        public WindowState WindowState { get => GetValue(WindowStateProperty); set => SetValue(WindowStateProperty, value); }

        private Window? _window;
        private IconButton? _minBtn;
        private IconButton? _maxBtn;
        private IconButton? _closeBtn;
        private IconButton? _pinBtn;

        protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
        {
            base.OnApplyTemplate(e);

            _window = TopLevel.GetTopLevel(this) as Window;

            foreach (var it in new[] { "PART_LeftPanel", "PART_RightPanel" })
            {
                var ctrl = e.NameScope.Find<StackPanel>(it);
                ctrl?.IsVisible = !OperatingSystem.IsMacOS();
            }

            if (_window is not null)
                _window.PropertyChanged += OnWindowPropertyChanged;

            _minBtn = e.NameScope.Find<IconButton>("PART_MinimizeButton");
            _maxBtn = e.NameScope.Find<IconButton>("PART_MaximizeButton");
            _closeBtn = e.NameScope.Find<IconButton>("PART_CloseButton");
            _pinBtn = e.NameScope.Find<IconButton>("PART_PinButton");

            _minBtn?.Click += OnMinimizeClick;
            _maxBtn?.Click += OnMaximizeClick;
            _closeBtn?.Click += OnCloseClick;

            UpdateMaximizeIcon();
            UpdatePinIcon();
        }

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);

            if (change.Property == IsPinnedProperty)
                UpdatePinIcon();
        }

        private void OnWindowPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
        {
            if (e.Property.Name == nameof(Window.WindowState))
            {
                SetValue(WindowStateProperty, _window!.WindowState);
                UpdateMaximizeIcon();
            }
        }

        private void UpdateMaximizeIcon()
        {
            if (_maxBtn != null && _window != null)
            {
                _maxBtn.Icon = _window.WindowState == WindowState.Maximized
                    ? LucideIconNames.Minimize
                    : LucideIconNames.Maximize;
            }
        }

        private void UpdatePinIcon()
        {
            if (_pinBtn is null)
                return;

            _pinBtn.Icon = IsPinned ? LucideIconNames.Pin : LucideIconNames.PinOff;

            string? tooltip = IsPinned
                ? Strings.Menu_Overlay_Unpin
                : Strings.Menu_Overlay_Pin;

            if (!String.IsNullOrEmpty(tooltip))
                ToolTip.SetTip(_pinBtn, tooltip);
        }

        private void OnMinimizeClick(object? sender, EventArgs e)
        {
            _window?.WindowState = WindowState.Minimized;
        }

        private void OnMaximizeClick(object? sender, EventArgs e)
        {
            if (_window == null) return;
            _window.WindowState = _window.WindowState == WindowState.Maximized
                ? WindowState.Normal
                : WindowState.Maximized;
        }

        private void OnCloseClick(object? sender, EventArgs e)
        {
            if (CloseCommand is not null)
            {
                if (CloseCommand.CanExecute(CloseCommandParameter))
                    CloseCommand.Execute(CloseCommandParameter);

                return;
            }

            _window?.Close();
        }

        protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
        {
            base.OnDetachedFromVisualTree(e);

            if (_window is not null)
                _window.PropertyChanged -= OnWindowPropertyChanged;

            _minBtn?.Click -= OnMinimizeClick;
            _maxBtn?.Click -= OnMaximizeClick;
            _closeBtn?.Click -= OnCloseClick;
        }
    }
}