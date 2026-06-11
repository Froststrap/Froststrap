using Avalonia.Controls;
using Froststrap.UI.ViewModels.Settings;
using Froststrap.UI.ViewModels.Settings.Mods;

namespace Froststrap.UI.Elements.Settings.Pages.Mods
{
    internal class CommunityModsDialogService(MainWindowViewModel mainVm)
    {
        private readonly MainWindowViewModel _mainVm = mainVm ?? throw new ArgumentNullException(nameof(mainVm));

        public void OpenPresetMods() => _mainVm.NavigateToPresetModsCommand.Execute(null);

        public void OpenMods() => _mainVm.NavigateToMyModsCommand.Execute(null);

        public void OpenModGenerator() => _mainVm.NavigateToModGeneratorCommand.Execute(null);
    }

    public partial class CommunityModsPage : UserControl
    {
        private CommunityModsDialogService? _dialogService;

        public CommunityModsPage()
        {
            InitializeComponent();
            App.FrostRPC?.SetPage("Community Mods");
            this.DataContextChanged += OnDataContextChanged;
        }

        private void OnDataContextChanged(object? sender, EventArgs e)
        {
            if (DataContext is not CommunityModsViewModel viewModel) return;

            viewModel.OpenPresetModsEvent -= OnOpenPresetMods;
            viewModel.OpenModsEvent -= OnOpenMods;
            viewModel.OpenModGeneratorEvent -= OnOpenModGenerator;

            viewModel.OpenPresetModsEvent += OnOpenPresetMods;
            viewModel.OpenModsEvent += OnOpenMods;
            viewModel.OpenModGeneratorEvent += OnOpenModGenerator;
        }

        private void OnOpenPresetMods(object? sender, EventArgs e) => EnsureDialogService()?.OpenPresetMods();
        private void OnOpenMods(object? sender, EventArgs e) => EnsureDialogService()?.OpenMods();
        private void OnOpenModGenerator(object? sender, EventArgs e) => EnsureDialogService()?.OpenModGenerator();

        private CommunityModsDialogService? EnsureDialogService()
        {
            if (_dialogService != null) return _dialogService;
            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel?.DataContext is MainWindowViewModel mainVm)
                _dialogService = new CommunityModsDialogService(mainVm);
            return _dialogService;
        }
    }
}