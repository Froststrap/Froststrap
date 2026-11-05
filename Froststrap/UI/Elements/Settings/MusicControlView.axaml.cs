// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

using Avalonia;
using Avalonia.Controls;
using Froststrap.UI.ViewModels.Settings;

namespace Froststrap.UI.Elements.Settings
{
    internal partial class MusicControlView : UserControl
    {
        private MusicControlViewModel? _viewModel;

        public MusicControlView()
        {
            InitializeComponent();
        }

        // the view model listens to a static event, so it only lives while the window shows it
        protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
        {
            base.OnAttachedToVisualTree(e);

            _viewModel ??= new MusicControlViewModel();
            DataContext = _viewModel;
        }

        protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
        {
            base.OnDetachedFromVisualTree(e);

            _viewModel?.Dispose();
            _viewModel = null;
            DataContext = null;
        }
    }
}
