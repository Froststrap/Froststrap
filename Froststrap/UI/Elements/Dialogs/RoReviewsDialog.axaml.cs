// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

using Avalonia.Interactivity;
using Avalonia.Input;
using Froststrap.UI.ViewModels.Dialogs;

namespace Froststrap.UI.Elements.Dialogs;

internal partial class RoReviewsDialog : Base.AvaloniaWindow
{
    public RoReviewsDialog(long targetId, string gameName) : this()
    {
        DataContext = new RoReviewsViewModel(targetId, gameName);
    }

    public RoReviewsDialog()
    {
        InitializeComponent();
    }

    private void CloseClicked(object? sender, RoutedEventArgs e) => Close();

    private void DraftKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter || !e.KeyModifiers.HasFlag(KeyModifiers.Shift))
            return;

        if (DataContext is RoReviewsViewModel viewModel && viewModel.SubmitCommand.CanExecute(null))
        {
            e.Handled = true;
            viewModel.SubmitCommand.Execute(null);
        }
    }

    private void RoReviewLinkClicked(object? sender, RoutedEventArgs e)
        => Process.Start(new ProcessStartInfo("https://chromewebstore.google.com/detail/roreview/pldfgikbjlmcodfkddefledmmbmbndbn")
        {
            UseShellExecute = true
        });

    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);
        (DataContext as IDisposable)?.Dispose();
    }
}
