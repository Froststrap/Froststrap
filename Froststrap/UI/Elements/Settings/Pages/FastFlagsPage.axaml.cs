using Avalonia;
using Avalonia.Controls;
using Froststrap.UI.ViewModels.Settings;

namespace Froststrap.UI.Elements.Settings.Pages
{
    /// <summary>
    /// Implementation of IDialogService for FastFlags editing
    /// </summary>
    internal class FastFlagsDialogService : IDialogService
    {
        private readonly MainWindowViewModel _mainVm;

        public FastFlagsDialogService(MainWindowViewModel mainVm)
        {
            _mainVm = mainVm ?? throw new ArgumentNullException(nameof(mainVm));
        }

        public async Task OpenFastFlagEditorAsync()
        {
            _mainVm.NavigateToFastFlagEditorCommand.Execute(null);
            await Task.CompletedTask;
        }
    }

    public partial class FastFlagsPage : UserControl
    {
        private bool _viewModelSetUp = false;

        public FastFlagsPage()
        {
            InitializeComponent();
            App.FrostRPC?.SetPage("FastFlags Settings");
        }

        protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
        {
            base.OnAttachedToVisualTree(e);
            SetupViewModelIfNeeded();
        }

        private void SetupViewModelIfNeeded()
        {
            if (_viewModelSetUp) return;

            try
            {
                var topLevel = TopLevel.GetTopLevel(this);

                if (topLevel?.DataContext is MainWindowViewModel mainVm)
                {
                    CreateViewModelWithDialogService(mainVm);
                    _viewModelSetUp = true;
                }
                else
                {
                    CreateFallbackViewModel();
                    _viewModelSetUp = true;
                }
            }
            catch (Exception ex)
            {
                App.Logger.WriteException("FastFlagsPage", ex);
                CreateFallbackViewModel();
                _viewModelSetUp = true;
            }
        }

        private void CreateViewModelWithDialogService(MainWindowViewModel mainVm)
        {
            var dialogService = new FastFlagsDialogService(mainVm);

            var newVm = new FastFlagsViewModel(
                new DefaultFastFlagsService(),
                new DefaultSettingsService(),
                dialogService);

            DataContext = newVm;
        }

        private void CreateFallbackViewModel()
        {
            var fallbackVm = new FastFlagsViewModel();
            DataContext = fallbackVm;
        }
    }
}