// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Froststrap.UI.ViewModels.Settings;

namespace Froststrap.UI.Elements.Settings
{
    internal partial class MusicPlaylistView : UserControl
    {
        private MusicPlaylistViewModel? _viewModel;

        public MusicPlaylistView()
        {
            InitializeComponent();
        }

        // the view model listens to static events, so it only lives while the page shows it
        protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
        {
            base.OnAttachedToVisualTree(e);

            _viewModel ??= new MusicPlaylistViewModel();
            DataContext = _viewModel;
        }

        protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
        {
            base.OnDetachedFromVisualTree(e);

            // unbind first, so disposing doesn't update a view that's going away
            DataContext = null;
            _viewModel?.Dispose();
            _viewModel = null;
        }

        // double-clicking a song plays it, like the Play button
        private void OnTrackDoubleTapped(object? sender, TappedEventArgs e) => _viewModel?.PlaySelected();
    }
}
