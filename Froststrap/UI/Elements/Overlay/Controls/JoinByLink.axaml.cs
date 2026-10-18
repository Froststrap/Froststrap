// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

using Avalonia.Controls;
using Avalonia.Input;
using Froststrap.UI.ViewModels.Overlay;

namespace Froststrap.UI.Elements.Overlay.Controls
{
    internal partial class JoinByLink : UserControl
    {
        public JoinByLink()
        {
            InitializeComponent();
        }

        private void LinkPopup_Opened(object? sender, EventArgs e) => LinkBox.Focus();

        private void LinkBox_KeyDown(object? sender, KeyEventArgs e)
        {
            if (e.Key != Key.Enter)
                return;

            e.Handled = true;

            if (DataContext is GameOverlayViewModel vm && vm.JoinByLink.JoinCommand.CanExecute(null))
                vm.JoinByLink.JoinCommand.Execute(null);
        }
    }
}
