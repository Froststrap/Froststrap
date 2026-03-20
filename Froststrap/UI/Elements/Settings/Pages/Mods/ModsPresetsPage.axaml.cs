using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Froststrap.UI.ViewModels.Settings.Mods;
using Froststrap.UI.ViewModels.Settings;

namespace Froststrap.UI.Elements.Settings.Pages.Mods
{
    internal class PresetModsDialogService
    {
        private readonly MainWindowViewModel _mainVm;

        public PresetModsDialogService(MainWindowViewModel mainVm)
        {
            _mainVm = mainVm ?? throw new ArgumentNullException(nameof(mainVm));
        }

        public void OpenCommunityMods()
        {
            _mainVm.NavigateToCommunityModsCommand.Execute(System.Reactive.Unit.Default);
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

    public partial class ModsPresetsPage : UserControl
    {
        private bool _navigationSetUp = false;

        public ModsPresetsPage()
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
                if (topLevel?.DataContext is MainWindowViewModel mainVm && DataContext is ModsPresetsViewModel modsVm)
                {
                    var service = new PresetModsDialogService(mainVm);

                    modsVm.OpenCommunityModsEvent += (s, e) => service.OpenCommunityMods();
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
