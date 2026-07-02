using Avalonia.Controls;
using Froststrap.UI.ViewModels.Settings;
using Froststrap.UI.ViewModels.Settings.Mods;

namespace Froststrap.UI.Elements.Settings.Pages.Mods
{
    internal class ModGeneratorDialogService(MainWindowViewModel mainVm)
    {
        private readonly MainWindowViewModel _mainVm = mainVm ?? throw new ArgumentNullException(nameof(mainVm));

        public void OpenCommunityMods() => _mainVm.NavigateToCommunityModsCommand.Execute(null);

        public void OpenPresetMods() => _mainVm.NavigateToPresetModsCommand.Execute(null);

        public void OpenMyMods() => _mainVm.NavigateToMyModsCommand.Execute(null);
    }

    public partial class ModGeneratorPage : UserControl
    {
        private ModGeneratorDialogService? _dialogService;

        public ModGeneratorPage()
        {
            InitializeComponent();
            App.FrostRPC?.SetPage("Mod Generator");
            this.DataContextChanged += OnDataContextChanged;
        }

        private void OnDataContextChanged(object? sender, EventArgs e)
        {
            if (DataContext is not ModGeneratorViewModel viewModel) return;

            viewModel.OpenCommunityModsEvent -= OnOpenCommunityMods;
            viewModel.OpenPresetModsEvent -= OnOpenPresetMods;
            viewModel.OpenModsEvent -= OnOpenMods;

            viewModel.OpenCommunityModsEvent += OnOpenCommunityMods;
            viewModel.OpenPresetModsEvent += OnOpenPresetMods;
            viewModel.OpenModsEvent += OnOpenMods;
        }

        private void OnOpenCommunityMods(object? sender, EventArgs e) => EnsureDialogService()?.OpenCommunityMods();
        private void OnOpenPresetMods(object? sender, EventArgs e) => EnsureDialogService()?.OpenPresetMods();
        private void OnOpenMods(object? sender, EventArgs e) => EnsureDialogService()?.OpenMyMods();

        private ModGeneratorDialogService? EnsureDialogService()
        {
            if (_dialogService != null) return _dialogService;
            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel?.DataContext is MainWindowViewModel mainVm)
                _dialogService = new ModGeneratorDialogService(mainVm);
            return _dialogService;
        }
    }
}