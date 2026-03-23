using System;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Froststrap.UI.ViewModels.Settings;
using Froststrap.UI.ViewModels.Settings.Mods;

namespace Froststrap.UI.Elements.Settings.Pages
{
    /// <summary>
    /// Implementation of IModsDialogService for Mods navigation
    /// </summary>
    internal class ModsDialogService : IModsDialogService
    {
        private readonly MainWindowViewModel _mainVm;

        public ModsDialogService(MainWindowViewModel mainVm)
        {
            _mainVm = mainVm ?? throw new ArgumentNullException(nameof(mainVm));
        }

        public async Task OpenCommunityModsAsync()
        {
            _mainVm.NavigateToCommunityModsCommand.Execute(null);
            await Task.CompletedTask;
        }

        public async Task OpenPresetModsAsync()
        {
            _mainVm.NavigateToPresetModsCommand.Execute(null);
            await Task.CompletedTask;
        }

        public async Task OpenModGeneratorAsync()
        {
            _mainVm.NavigateToModGeneratorCommand.Execute(null);
            await Task.CompletedTask;
        }
    }

    public partial class ModsPage : UserControl
    {
        private string _originalName = "";
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

        private void ModName_GotFocus(object? sender, GotFocusEventArgs e)
        {
            if (sender is TextBox textBox)
                _originalName = textBox.Text ?? "";
        }

        private void ModName_LostFocus(object? sender, RoutedEventArgs e)
        {
            if (sender is TextBox textBox && DataContext is ModsViewModel viewModel)
            {
                if (_originalName != textBox.Text)
                {
                    bool success = viewModel.RenameMod(_originalName, textBox.Text ?? "");

                    if (!success)
                    {
                        textBox.Text = _originalName;
                    }
                }
            }
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
                {
                    await Task.Run(() => vm.ProcessDroppedFiles(paths!));
                }
            }
        }
    }
}