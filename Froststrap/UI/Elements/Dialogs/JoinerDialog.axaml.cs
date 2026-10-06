// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace Froststrap.UI.Elements.Dialogs
{
    /// <summary>
    /// Interaction logic for JoinerDialog.axaml
    /// </summary>
    internal partial class JoinerDialog : Base.AvaloniaWindow
    {
        public NextAction CloseAction = NextAction.Terminate;

        private bool _joining;

        public JoinerDialog()
        {
            InitializeComponent();

            Loaded += (s, e) => LinkBox.Focus();
        }

        private void OnLinkBoxKeyDown(object? sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                e.Handled = true;
                _ = TryJoin();
            }
        }

        private void OnLinkTextChanged(object? sender, TextChangedEventArgs e)
        {
            if (_joining)
                return;

            if (JoinInstantlyOnPaste.IsChecked.GetValueOrDefault())
                _ = TryJoin();
        }

        private void OnJoinClicked(object? sender, RoutedEventArgs e) => _ = TryJoin();

        private async Task TryJoin()
        {
            if (_joining)
                return;

            _joining = true;

            string? launchCommand = await GameJoin.GetLaunchCommandByLink(LinkBox.Text);

            if (launchCommand is null)
            {
                _joining = false;
                StatusText.Text = Strings.Joiner_InvalidLink;
                return;
            }

            if (!IsVisible)
            {
                _joining = false;
                return;
            }

            LinkBox.IsEnabled = false;
            JoinButton.IsEnabled = false;
            StatusText.Text = Strings.Joiner_Joining;

            App.Logger.Info($"Joining with launch command from pasted link: {launchCommand}");

            App.LaunchSettings.RobloxLaunchArgs = launchCommand;

            if (CloseAfterJoining.IsChecked.GetValueOrDefault())
            {
                CloseAction = NextAction.LaunchRoblox;
                Close();
            }
            else
            {
                _ = LaunchHandler.LaunchRoblox(LaunchMode.Player);
            }
        }

        private void OnSettingsClicked(object? sender, RoutedEventArgs e)
        {
            CloseAction = NextAction.LaunchSettings;
            Close();
        }
    }
}
