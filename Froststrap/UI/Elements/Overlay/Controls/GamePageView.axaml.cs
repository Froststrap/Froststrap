// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

using Avalonia;
using Avalonia.Controls;
using Froststrap.UI.ViewModels.Overlay.Controls;

namespace Froststrap.UI.Elements.Overlay.Controls
{
    internal sealed partial class GamePageView : UserControl, IDisposable
    {
        private GamePageViewModel? _viewModel;

        public GamePageView()
        {
            InitializeComponent();
        }

        public void Load(QuickPlayGameItem item, Action? onBack = null, Action? onFavoriteChanged = null)
        {
            _viewModel?.Dispose();

            _viewModel = new GamePageViewModel(item, onBack, placeId => ServersBrowser.Attach(null, placeId, showCurrentServer: false), onFavoriteChanged);
            DataContext = _viewModel;

            _ = _viewModel.LoadAsync();
        }

        public void Unload()
        {
            Dispose();
        }

        protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
        {
            base.OnDetachedFromVisualTree(e);

            Dispose();
        }

        public void Dispose()
        {
            _viewModel?.Dispose();
            _viewModel = null;
            DataContext = null;

            GC.SuppressFinalize(this);
        }
    }
}
