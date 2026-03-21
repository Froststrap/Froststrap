using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Froststrap.UI.ViewModels.Settings.Mods;
using Froststrap.UI.ViewModels.Settings;

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
            _mainVm.NavigateToPresetModsCommand.Execute(System.Reactive.Unit.Default);
        }

        public void OpenMods()
        {
            _mainVm.NavigateToModsCommand.Execute(System.Reactive.Unit.Default);
        }

        public void OpenModGenerator()
        {
            _mainVm.NavigateToModGeneratorCommand.Execute(System.Reactive.Unit.Default);
        }
    }

    public partial class CommunityModsPage : UserControl
    {
        private bool _navigationSetUp = false;

        public CommunityModsPage()
        {
            InitializeComponent();

            App.FrostRPC?.SetPage("Preset Mods");

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
                if (topLevel?.DataContext is MainWindowViewModel mainVm && DataContext is CommunityModsViewModel modsVm)
                {
                    var service = new CommunityModsDialogService(mainVm);

                    modsVm.OpenPresetModsEvent += (s, e) => service.OpenPresetMods();
                    modsVm.OpenModsEvent += (s, e) => service.OpenMods();
                    modsVm.OpenModGeneratorEvent += (s, e) => service.OpenModGenerator();

                    _navigationSetUp = true;
                }
            }
            catch
            {
            }
        }
    }
}
