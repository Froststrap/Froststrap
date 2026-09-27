using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Froststrap.Integrations.OverlayModules;
using Froststrap.UI.ViewModels.Overlay.Controls;

namespace Froststrap.UI.Elements.Overlay.Controls
{
    internal partial class FriendActivity : UserControl
    {
        private FriendActivityViewModel _viewModel;

        public FriendActivity()
        {
            InitializeComponent();

            _viewModel = new FriendActivityViewModel(null);
            DataContext = _viewModel;
        }

        public void Attach(RobloxParty? party)
        {
            _viewModel = new FriendActivityViewModel(party);
            DataContext = _viewModel;
        }

        private async void OnLoaded(object sender, RoutedEventArgs e) =>
            await _viewModel.LoadConversations();

        private async void ConversationsSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sender is ListBox listBox && listBox.SelectedItem is FriendItem selected)
                await _viewModel.LoadConversationHistory(selected);
        }

        private async void MessageTextBoxKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Enter)
                return;

            await _viewModel.SendMessage();
        }

        private void CloseOverlay(object sender, RoutedEventArgs e) =>
            (TopLevel.GetTopLevel(this) as Window)?.Hide();
    }
}