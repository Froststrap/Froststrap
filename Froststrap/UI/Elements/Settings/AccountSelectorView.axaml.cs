using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Froststrap.UI.Elements.Dialogs;
using Froststrap.UI.ViewModels.Settings;

namespace Froststrap.UI.Elements.Settings
{
    public partial class AccountSelectorView : UserControl
    {
        private const string LOG_IDENT = "AccountSelectorView";
        private AccountSelectorViewModel? _viewModel;

        public AccountSelectorView()
        {
            InitializeComponent();

            _viewModel = new AccountSelectorViewModel();
            DataContext = _viewModel;
        }

        protected override void OnInitialized()
        {
            base.OnInitialized();

            if (_viewModel != null)
            {
                _viewModel.OnManualAddRequested += HandleManualAddRequested;
            }
        }

        protected override void OnLoaded(RoutedEventArgs e)
        {
            base.OnLoaded(e);
        }

        private async void HandleManualAddRequested()
        {
            await ShowManualAccountDialogAsync();
        }

        private async Task ShowManualAccountDialogAsync()
        {
            try
            {
                App.Logger.WriteLine(LOG_IDENT, "Showing manual cookie dialog");

                var dialog = new ManualCookieDialog();

                var desktop = Application.Current?.ApplicationLifetime as Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime;
                var parent = desktop?.MainWindow ?? (desktop?.Windows.Count > 0 ? desktop.Windows[0] : null);

                if (parent != null)
                {
                    var result = await dialog.ShowDialog<AccountManagerAccount?>(parent);

                    if (result != null)
                    {
                        App.Logger.WriteLine(LOG_IDENT, $"Dialog returned account: {result.Username}");

                        if (_viewModel != null)
                        {
                            _viewModel.AddAccountDirect(result);
                        }
                    }
                }
                else
                {
                    App.Logger.WriteLine(LOG_IDENT, "Error: Could not find a parent window.");
                }
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine(LOG_IDENT, $"Error showing manual dialog: {ex.Message}");
            }
            finally
            {
                if (_viewModel != null)
                {
                    _viewModel.IsAddingAccount = false;
                }
            }
        }

        protected override void OnUnloaded(RoutedEventArgs e)
        {
            if (_viewModel != null)
            {
                _viewModel.OnManualAddRequested -= HandleManualAddRequested;
            }
            base.OnUnloaded(e);
        }
    }
}
