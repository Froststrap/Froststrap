using ReactiveUI;
using Froststrap.UI.Elements.Settings.Pages;
using CommunityToolkit.Mvvm.Input;
using System.Windows.Input;
using System.Reactive;

namespace Froststrap.UI.ViewModels.Settings.FastFlags
{
    public class FastFlagEditorViewModel : ReactiveObject, IRoutableViewModel
    {
        private readonly MainWindowViewModel _mainWindowViewModel;

        public string? UrlPathSegment => "fastflageditor";
        public IScreen HostScreen { get; }

        public string Breadcrumb => "Settings > Fast Flags > Editor";

        public ICommand BackCommand { get; }

        public FastFlagEditorViewModel(IScreen hostScreen)
        {
            HostScreen = hostScreen;
            _mainWindowViewModel = hostScreen as MainWindowViewModel;
            App.Logger.WriteLine("FastFlagEditorViewModel", $"FastFlagEditorViewModel created with HostScreen: {hostScreen?.GetType().Name}");

            BackCommand = new RelayCommand(() =>
            {
                if (_mainWindowViewModel != null)
                {
                    _mainWindowViewModel.NavigateToFastFlagsCommand.Execute(Unit.Default);
                }
            });
        }
    }
}