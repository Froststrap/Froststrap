using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Windows.Input;

namespace Froststrap.UI.ViewModels.Settings.GlobalSettings
{
    public class GlobalSettingsEditorViewModel : ObservableObject
    {
        private readonly MainWindowViewModel _mainWindowViewModel;
        public ICommand BackCommand { get; }

        public GlobalSettingsEditorViewModel(MainWindowViewModel mainWindowViewModel)
        {
            _mainWindowViewModel = mainWindowViewModel;

            App.Logger.WriteLine("GlobalSettingsEditorViewModel", "FastFlagEditorViewModel created.");

            BackCommand = new RelayCommand(() =>
            {
                if (_mainWindowViewModel != null)
                {
                    _mainWindowViewModel.NavigateToGlobalSettingsCommand.Execute(null);
                }
            });
        }
    }
}