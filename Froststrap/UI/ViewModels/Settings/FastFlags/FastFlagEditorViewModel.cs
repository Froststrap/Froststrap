using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Windows.Input;

namespace Froststrap.UI.ViewModels.Settings.FastFlags
{
    public class FastFlagEditorViewModel : ObservableObject
    {
        private readonly MainWindowViewModel _mainWindowViewModel;
        public ICommand BackCommand { get; }

        public FastFlagEditorViewModel(MainWindowViewModel mainWindowViewModel)
        {
            _mainWindowViewModel = mainWindowViewModel;

            App.Logger.WriteLine("FastFlagEditorViewModel", "FastFlagEditorViewModel created.");

            BackCommand = new RelayCommand(() =>
            {
                if (_mainWindowViewModel != null)
                {
                    _mainWindowViewModel.NavigateToFastFlagsCommand.Execute(null);
                }
            });
        }
    }
}