// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

using Froststrap.Integrations;
using Froststrap.UI.ViewModels.ContextMenu;

namespace Froststrap.UI.Elements.ContextMenu
{
    /// <summary>
    /// Interaction logic for ServerHistory.axaml
    /// </summary>
    internal sealed partial class ServerHistory : Base.AvaloniaWindow
    {
        public ServerHistory()
        {
            InitializeComponent();
        }

        public ServerHistory(Integrations.ActivityWatcher.Watcher watcher) : this()
        {
            var viewModel = new ServerHistoryViewModel(watcher);

            viewModel.RequestCloseEvent += (_, _) => Close();
            Closed += (_, _) => viewModel.Detach();

            DataContext = viewModel;
        }
    }
}