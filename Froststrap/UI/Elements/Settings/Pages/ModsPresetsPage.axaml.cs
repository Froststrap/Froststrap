using Avalonia.Controls;
using Froststrap.UI.ViewModels.Settings;

namespace Froststrap.UI.Elements.Settings.Pages
{
    internal class PresetModsDialogService(MainWindowViewModel mainVm)
    {
        private readonly MainWindowViewModel _mainVm = mainVm ?? throw new ArgumentNullException(nameof(mainVm));

        public void OpenCommunityMods() => _mainVm.NavigateToCommunityModsCommand.Execute(null);
        public void OpenMods() => _mainVm.NavigateToMyModsCommand.Execute(null);
        public void OpenModGenerator() => _mainVm.NavigateToModGeneratorCommand.Execute(null);
    }

    public partial class ModsPresetsPage : UserControl
    {
        private PresetModsDialogService? _dialogService;

        public ModsPresetsPage()
        {
            InitializeComponent();
            App.FrostRPC?.SetPage("Preset Mods");
            this.DataContextChanged += OnDataContextChanged;
        }

        private void OnDataContextChanged(object? sender, EventArgs e)
        {
            if (DataContext is not ModsPresetsViewModel viewModel)
                return;

            viewModel.OpenCommunityModsEvent -= OnOpenCommunityMods;
            viewModel.OpenModsEvent -= OnOpenMods;
            viewModel.OpenModGeneratorEvent -= OnOpenModGenerator;

            viewModel.OpenCommunityModsEvent += OnOpenCommunityMods;
            viewModel.OpenModsEvent += OnOpenMods;
            viewModel.OpenModGeneratorEvent += OnOpenModGenerator;
        }

        private void OnOpenCommunityMods(object? sender, EventArgs e) => EnsureDialogService()?.OpenCommunityMods();
        private void OnOpenMods(object? sender, EventArgs e) => EnsureDialogService()?.OpenMods();
        private void OnOpenModGenerator(object? sender, EventArgs e) => EnsureDialogService()?.OpenModGenerator();

        private PresetModsDialogService? EnsureDialogService()
        {
            if (_dialogService != null) return _dialogService;

            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel?.DataContext is MainWindowViewModel mainVm)
            {
                _dialogService = new PresetModsDialogService(mainVm);
                return _dialogService;
            }

            return null;
        }
    }
}