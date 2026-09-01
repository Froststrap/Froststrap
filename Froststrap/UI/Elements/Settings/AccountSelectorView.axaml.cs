using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Froststrap.UI.Elements.Dialogs;
using Froststrap.UI.ViewModels.Settings;

namespace Froststrap.UI.Elements.Settings
{
    internal partial class AccountSelectorView : UserControl, IDisposable
    {
        private readonly AccountSelectorViewModel? _viewModel;
        private bool _disposed;

        public AccountSelectorView()
        {
            InitializeComponent();

            _viewModel = new AccountSelectorViewModel();
            DataContext = _viewModel;
        }

        protected override void OnInitialized()
        {
            base.OnInitialized();

            _viewModel?.OnManualAddRequested += HandleManualAddRequested;
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
                App.Logger.Info("Showing manual cookie dialog");

                var dialog = new ManualCookieDialog();

                var desktop = Application.Current?.ApplicationLifetime as Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime;
                var parent = desktop?.MainWindow ?? (desktop?.Windows.Count > 0 ? desktop.Windows[0] : null);

                if (parent != null)
                {
                    var result = await dialog.ShowDialog<AccountManagerAccount?>(parent);

                    if (result != null)
                    {
                        App.Logger.Info($"Dialog returned account: {result.Username}");

                        _viewModel?.AddAccountDirect(result);
                    }
                }
                else
                {
                    App.Logger.Error("Could not find a parent window.");
                }
            }
            catch (Exception ex)
            {
                App.Logger.Error($"Couldn't show manual dialog: {ex.Message}");
            }
            finally
            {
                _viewModel?.IsAddingAccount = false;
            }
        }

        protected override void OnUnloaded(RoutedEventArgs e)
        {
            _viewModel?.OnManualAddRequested -= HandleManualAddRequested;
            base.OnUnloaded(e);
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (_disposed)
                return;

            if (disposing)
            {
                _viewModel?.Dispose();
            }

            _disposed = true;
        }
    }
}