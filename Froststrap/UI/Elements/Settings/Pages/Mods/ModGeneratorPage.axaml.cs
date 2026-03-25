using Avalonia.Controls;
using Froststrap.UI.ViewModels.Settings;
using Froststrap.UI.ViewModels.Settings.Mods;

namespace Froststrap.UI.Elements.Settings.Pages.Mods
{
    internal class ModGeneratorDialogService
    {
        private readonly MainWindowViewModel _mainVm;

        public ModGeneratorDialogService(MainWindowViewModel mainVm)
        {
            _mainVm = mainVm ?? throw new ArgumentNullException(nameof(mainVm));
        }

        public void OpenCommunityMods()
        {
            _mainVm.NavigateToCommunityModsCommand.Execute(null);
        }

        public void OpenPresetMods()
        {
            _mainVm.NavigateToPresetModsCommand.Execute(null);
        }

        public void OpenMyMods()
        {
            _mainVm.NavigateToMyModsCommand.Execute(null);
        }
    }

    public partial class ModGeneratorPage : UserControl
    {
        private bool _navigationSetUp = false;

        public ModGeneratorPage()
        {
            InitializeComponent();
            App.FrostRPC?.SetPage("Mod Generator");
            this.Loaded += (s, e) => SetupNavigationIfNeeded();
        }

        private void SetupNavigationIfNeeded()
        {
            if (_navigationSetUp) return;

            try
            {
                var topLevel = TopLevel.GetTopLevel(this);

                if (topLevel?.DataContext is MainWindowViewModel mainVm && DataContext is ModGeneratorViewModel genVm)
                {
                    var service = new ModGeneratorDialogService(mainVm);

                    genVm.OpenCommunityModsEvent += (s, e) => service.OpenCommunityMods();
                    genVm.OpenPresetModsEvent += (s, e) => service.OpenPresetMods();
                    genVm.OpenModsEvent += (s, e) => service.OpenMyMods();

                    _navigationSetUp = true;
                }
            }
            catch (Exception ex)
            {
                App.Logger?.WriteException("ModGeneratorPage::SetupNavigation", ex);
            }
        }
    }
}