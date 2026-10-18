// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

using Froststrap.Enums;
using Froststrap.Extensions;
using Froststrap.Integrations.OverlayModules;
using Froststrap.Models.Overlay;
using Froststrap.UI.ViewModels.Overlay;

namespace Froststrap.UI.Elements.Overlay
{
    internal partial class KeyOverlayView : UserControl
    {
        public KeyOverlayView()
        {
            InitializeComponent();

            DataContext = ViewModel;
        }

        public KeyOverlayViewModel ViewModel { get; } = new();

        public bool IsPreview { get; set; }

        public void ApplyAppearance()
        {
            KeyOverlayStyleDefinition definition = App.Settings.Prop.KeyOverlayStyle.GetDefinition();

            ApplyBrushColour("KeyOverlayCardBrush", definition.Card);
            ApplyBrushColour("KeyOverlayKeyBrush", definition.Key);
            ApplyBrushColour("KeyOverlayKeyTextBrush", definition.KeyText);
            ApplyBrushColour("KeyOverlayDividerBrush", definition.Divider);
            ApplyBrushColour("KeyOverlayLabelBrush", definition.Label);

            bool minecraft = definition.Layout == KeyOverlayLayout.Minecraft;

            StandardLayout.IsVisible = !minecraft;
            MinecraftLayout.IsVisible = minecraft;

            double scale = App.Settings.Prop.KeyOverlayScale.GetFactor();
            ScaleRoot.LayoutTransform = new ScaleTransform(scale, scale);

            ApplyHighlightColour();
        }

        protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
        {
            base.OnAttachedToVisualTree(e);

            KeyOverlayDisplay.Changed -= OnDisplayChanged;
            KeyOverlayDisplay.Changed += OnDisplayChanged;

            ViewModel.RefreshOptions();
            ApplyAppearance();

            if (IsPreview)
                ViewModel.ApplyPreview();
        }

        protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
        {
            KeyOverlayDisplay.Changed -= OnDisplayChanged;

            base.OnDetachedFromVisualTree(e);
        }

        private void OnDisplayChanged(object? sender, EventArgs e)
        {
            ViewModel.RefreshOptions();
            ApplyAppearance();
        }

        private void ApplyBrushColour(string key, Color colour)
        {
            if (Resources[key] is SolidColorBrush brush)
                brush.Color = colour;
        }

        private void ApplyHighlightColour()
        {
            if (Resources["KeyOverlayKeyActiveBrush"] is not SolidColorBrush brush)
                return;

            string? stored = App.Settings.Prop.KeyOverlayHighlightColor;

            if (!string.IsNullOrWhiteSpace(stored) && Color.TryParse(stored, out var custom))
                brush.Color = custom;
            else if (TryGetAccentColour(out var accent))
                brush.Color = accent;
            else
                brush.Color = Color.Parse("#0078D4");
        }

        private bool TryGetAccentColour(out Color colour)
        {
            colour = default;

            if (!this.TryFindResource("SystemAccentColor", out var accent))
                return false;

            switch (accent)
            {
                case Color value:
                    colour = value;
                    return true;

                case ISolidColorBrush brush:
                    colour = brush.Color;
                    return true;

                default:
                    return false;
            }
        }
    }
}
