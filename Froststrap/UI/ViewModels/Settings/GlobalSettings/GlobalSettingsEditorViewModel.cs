using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Windows.Input;

namespace Froststrap.UI.ViewModels.Settings.GlobalSettings
{
    internal class GlobalSettingsEditorViewModel : ObservableObject
    {
        private readonly MainWindowViewModel _mainWindowViewModel;
        public ICommand BackCommand { get; }

        public GlobalSettingsEditorViewModel(MainWindowViewModel mainWindowViewModel)
        {
            _mainWindowViewModel = mainWindowViewModel;

            App.Logger.Info("FastFlagEditorViewModel created.");

            BackCommand = new RelayCommand(() =>
            {
                _mainWindowViewModel?.NavigateToGlobalSettingsCommand.Execute(null);
            });
        }
    }
}
