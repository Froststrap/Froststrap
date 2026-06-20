using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Platform.Storage;
using Froststrap.UI.ViewModels.Settings;
using Froststrap.UI.ViewModels.Settings.Mods;

namespace Froststrap.UI.Elements.Settings.Pages.Mods
{
    internal class ModsDialogService(MainWindowViewModel mainVm) : IModsDialogService
    {
        private readonly MainWindowViewModel _mainVm = mainVm ?? throw new ArgumentNullException(nameof(mainVm));

        public Task OpenCommunityModsAsync()
        {
            _mainVm.NavigateToCommunityModsCommand.Execute(null);
            return Task.CompletedTask;
        }

        public Task OpenPresetModsAsync()
        {
            _mainVm.NavigateToPresetModsCommand.Execute(null);
            return Task.CompletedTask;
        }

        public Task OpenModGeneratorAsync()
        {
            _mainVm.NavigateToModGeneratorCommand.Execute(null);
            return Task.CompletedTask;
        }
    }

    public partial class ModsPage : UserControl
    {
        private bool _viewModelSetUp = false;

        public ModsPage()
        {
            AddHandler(DragDrop.DropEvent, Page_Drop);
            InitializeComponent();
            App.FrostRPC?.SetPage("Mods");
            this.Loaded += (s, e) => SetupViewModelIfNeeded();
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
                App.Logger?.WriteException("ModsPage::SetupViewModel", ex);
                CreateFallbackViewModel();
                _viewModelSetUp = true;
            }
        }

        private void CreateViewModelWithDialogService(MainWindowViewModel mainVm)
        {
            var dialogService = new ModsDialogService(mainVm);
            var newVm = new ModsViewModel(dialogService);
            DataContext = newVm;
        }

        private void CreateFallbackViewModel()
        {
            var fallbackVm = new ModsViewModel();
            DataContext = fallbackVm;
        }

        private async void Page_Drop(object? sender, DragEventArgs e)
        {
            var files = e.DataTransfer.TryGetFiles();
            if (files != null && DataContext is ModsViewModel vm)
            {
                var paths = files
                    .Select(f => f.TryGetLocalPath())
                    .Where(p => !string.IsNullOrEmpty(p))
                    .ToArray();

                if (paths.Length > 0)
                    await vm.ImportFromPaths(paths!);
            }
        }
    }
}