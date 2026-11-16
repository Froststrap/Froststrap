// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

using Avalonia.Controls;
using Froststrap.UI.ViewModels.Overlay.Controls;

namespace Froststrap.UI.Elements.Overlay.Controls
{
    internal partial class Notes : UserControl
    {
        private readonly NotesViewModel _viewModel = new();

        public Notes()
        {
            DataContext = _viewModel;

            InitializeComponent();
        }

        public void Flush() => _viewModel.Save();
    }
}
