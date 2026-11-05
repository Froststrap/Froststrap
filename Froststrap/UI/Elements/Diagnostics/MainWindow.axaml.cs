// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

using Avalonia.Input;
using Froststrap.UI.ViewModels.Diagnostics;

namespace Froststrap.UI.Elements.Diagnostics
{
    internal partial class MainWindow : Base.AvaloniaWindow
    {
        private readonly MainWindowViewModel _viewModel;
        protected override bool ApplyTopPadding => false;

        public MainWindow()
        {
            _viewModel = new MainWindowViewModel();
            DataContext = _viewModel;
            Closed += (_, _) => App.DiagnosticsOpen = false;

            InitializeComponent();
        }

        private void TitleBarGrid_PointerPressed(object? sender, PointerPressedEventArgs e)
        {
            if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
                return;

            BeginMoveDrag(e);
        }
    }
}
