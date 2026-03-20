using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Froststrap.UI.ViewModels.Settings;

namespace Froststrap.UI.Elements.Settings.Pages
{
    internal class ModsDialogService
    {
        private readonly MainWindowViewModel _mainVm;

        public ModsDialogService(MainWindowViewModel mainVm)
        {
            _mainVm = mainVm ?? throw new ArgumentNullException(nameof(mainVm));
        }

        public void OpenCommunityMods()
        {
            _mainVm.NavigateToCommunityModsCommand.Execute(System.Reactive.Unit.Default);
        }

        public void OpenPresetMods()
        {
            _mainVm.NavigateToPresetModsCommand.Execute(System.Reactive.Unit.Default);
        }

        public void OpenModGenerator()
        {
            _mainVm.NavigateToModGeneratorCommand.Execute(System.Reactive.Unit.Default);
        }
    }

    public partial class ModsPage : UserControl
    {
        private string _originalName = "";
        private bool _navigationSetUp = false;

        public ModsPage()
        {
            AddHandler(DragDrop.DropEvent, Page_Drop);

            InitializeComponent();

            App.FrostRPC?.SetPage("Mods");

            SetupNavigationIfNeeded();
        }

        protected override void OnLoaded(RoutedEventArgs e)
        {
            base.OnLoaded(e);
            SetupNavigationIfNeeded();
        }

        private void SetupNavigationIfNeeded()
        {
            if (_navigationSetUp)
                return;

            try
            {
                var topLevel = TopLevel.GetTopLevel(this);
                if (topLevel?.DataContext is MainWindowViewModel mainVm && DataContext is ModsViewModel modsVm)
                {
                    var service = new ModsDialogService(mainVm);

                    modsVm.OpenCommunityModsEvent += (s, e) => service.OpenCommunityMods();
                    modsVm.OpenPresetModsEvent += (s, e) => service.OpenPresetMods();
                    modsVm.OpenModGeneratorEvent += (s, e) => service.OpenModGenerator();

                    _navigationSetUp = true;
                }
            }
            catch
            {
            }
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
