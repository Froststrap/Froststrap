// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using Froststrap.UI.ViewModels.Dialogs;

namespace Froststrap.UI.Elements;

internal sealed partial class CommunityModInfoOverlay : UserControl
{
    public event EventHandler? Dismissed;

    public CommunityModInfoOverlay()
    {
        InitializeComponent();
    }

    public CommunityModInfoOverlay(CommunityMod mod) : this()
    {
        var viewModel = new CommunityModInfoViewModel(mod, () => Dismissed?.Invoke(this, EventArgs.Empty));
        DataContext = viewModel;
        viewModel.Initialize();

        Dispatcher.UIThread.Post(() => Focus(), DispatcherPriority.Input);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            Dismissed?.Invoke(this, EventArgs.Empty);
            e.Handled = true;
            return;
        }

        base.OnKeyDown(e);
    }

    private void Backdrop_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        Dismissed?.Invoke(this, EventArgs.Empty);
        e.Handled = true;
    }

    private void Card_PointerPressed(object? sender, PointerPressedEventArgs e) => e.Handled = true;
}
