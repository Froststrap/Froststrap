using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Windows.Input;

namespace Froststrap.UI.ViewModels.Settings.FastFlags
{
    internal class FastFlagEditorViewModel
    {
        private readonly MainWindowViewModel _mainWindowViewModel;
        public ICommand BackCommand { get; }

        public FastFlagEditorViewModel(MainWindowViewModel mainWindowViewModel)
        {
            _mainWindowViewModel = mainWindowViewModel;

            App.Logger.Debug("FastFlagEditorViewModel created.");

            BackCommand = new RelayCommand(() =>
            {
                _mainWindowViewModel?.NavigateToFastFlagsCommand.Execute(null);
            });
        }
    }
}
