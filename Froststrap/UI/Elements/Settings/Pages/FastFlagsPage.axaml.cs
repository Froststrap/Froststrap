using Froststrap.UI.ViewModels.Settings;
using ReactiveUI;
using ReactiveUI.Avalonia;

namespace Froststrap.UI.Elements.Settings.Pages
{
    public partial class FastFlagsPage : ReactiveUserControl<MainWindowViewModel.SettingsPageViewModelWrapper>
    {
        public FastFlagsPage()
        {
            InitializeComponent();

            App.FrostRPC?.SetPage("FastFlags Settings");

            this.WhenActivated(disposables =>
            {
                if (ViewModel?.InnerViewModel is FastFlagsViewModel fastFlagsVm)
                {
                    fastFlagsVm.OpenFlagEditorEvent += HandleOpenFlagEditor;
                }
            });
        }

        private void HandleOpenFlagEditor(object? sender, EventArgs e)
        {
            if (ViewModel?.HostScreen is MainWindowViewModel mainVm)
            {
                mainVm.SelectedPage = "fastflageditor";
                mainVm.CurrentPageTitle = "Fast Flag Editor";
                mainVm.CurrentPageDescription = "Modify advanced Roblox engine settings.";

                var editorVm = new FastFlagEditorViewModel(mainVm);
                var wrapper = new MainWindowViewModel.SettingsPageViewModelWrapper(mainVm, "fastflageditor", editorVm);

                mainVm.Router.Navigate.Execute(wrapper).Subscribe();
            }
        }
    }
}