using Avalonia;
using Avalonia.Controls;
using Froststrap.UI.ViewModels.Settings;
using Froststrap.UI.ViewModels.Settings.Mods;
using System;

namespace Froststrap.UI.Elements.Settings.Pages.Mods
{
    internal class ModGeneratorDialogService : IModsDialogService
    {
        private readonly MainWindowViewModel _mainVm;

        public ModGeneratorDialogService(MainWindowViewModel mainVm)
        {
            _mainVm = mainVm ?? throw new ArgumentNullException(nameof(mainVm));
        }

        public async Task OpenCommunityModsAsync()
        {
            _mainVm.NavigateToCommunityModsCommand.Execute(System.Reactive.Unit.Default);
            await Task.CompletedTask;
        }

        public async Task OpenPresetModsAsync()
        {
            _mainVm.NavigateToPresetModsCommand.Execute(System.Reactive.Unit.Default);
            await Task.CompletedTask;
        }

        public async Task OpenModGeneratorAsync()
        {
            _mainVm.NavigateToModGeneratorCommand.Execute(System.Reactive.Unit.Default);
            await Task.CompletedTask;
        }
    }

    public partial class ModGeneratorPage : UserControl
    {
        private bool _viewModelSetUp = false;

        public ModGeneratorPage()
        {
            InitializeComponent();
            App.FrostRPC?.SetPage("Mod Generator");
            this.Loaded += (s, e) => SetupViewModelIfNeeded();
        }

        private void SetupViewModelIfNeeded()
        {
            if (_viewModelSetUp)
            {
                return;
            }

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
            catch (Exception)
            {
                CreateFallbackViewModel();
                _viewModelSetUp = true;
            }
        }

        private void CreateViewModelWithDialogService(MainWindowViewModel mainVm)
        {
            var dialogService = new ModGeneratorDialogService(mainVm);
            var newVm = new ModGeneratorViewModel(dialogService);
            DataContext = newVm;
        }

        private void CreateFallbackViewModel()
        {
            var fallbackVm = new ModGeneratorViewModel();
            DataContext = fallbackVm;
        }
    }
}