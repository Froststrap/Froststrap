using Avalonia.Controls;
using Avalonia.Interactivity;
using Froststrap.UI.ViewModels.Settings;
using Froststrap.UI.ViewModels.Settings.Mods;

namespace Froststrap.UI.Elements.Settings.Pages.Mods
{
    internal class CommunityModsDialogService
    {
        private readonly MainWindowViewModel _mainVm;

        public CommunityModsDialogService(MainWindowViewModel mainVm)
        {
            _mainVm = mainVm ?? throw new ArgumentNullException(nameof(mainVm));
        }

        public void OpenPresetMods()
        {
            _mainVm.NavigateToPresetModsCommand.Execute(null);
        }

        public void OpenMods()
        {
            _mainVm.NavigateToMyModsCommand.Execute(null);
        }

        public void OpenModGenerator()
        {
            _mainVm.NavigateToModGeneratorCommand.Execute(null);
        }
    }

    public partial class CommunityModsPage : UserControl
    {
        private bool _navigationSetUp = false;

        public CommunityModsPage()
        {
            InitializeComponent();
            App.FrostRPC?.SetPage("Community Mods");
        }

        protected override void OnLoaded(RoutedEventArgs e)
        {
            base.OnLoaded(e);
            SetupNavigationIfNeeded();
        }

        private void SetupNavigationIfNeeded()
        {
            if (_navigationSetUp) return;

            try
            {
                var topLevel = TopLevel.GetTopLevel(this);
                if (topLevel?.DataContext is MainWindowViewModel mainVm && DataContext is CommunityModsViewModel modsVm)
                {
                    var service = new CommunityModsDialogService(mainVm);

                    modsVm.OpenPresetModsEvent += (s, e) => service.OpenPresetMods();
                    modsVm.OpenModsEvent += (s, e) => service.OpenMods();
                    modsVm.OpenModGeneratorEvent += (s, e) => service.OpenModGenerator();

                    _navigationSetUp = true;
                }
            }
            catch (Exception ex)
            {
                App.Logger?.WriteException("CommunityModsPage::SetupNavigation", ex);
            }
        }
    }
}