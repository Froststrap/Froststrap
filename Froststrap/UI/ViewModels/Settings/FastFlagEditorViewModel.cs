using ReactiveUI;

namespace Froststrap.UI.ViewModels.Settings
{
    public class FastFlagEditorViewModel : ReactiveObject, IRoutableViewModel
    {
        public string? UrlPathSegment => "fastflageditor";
        public IScreen HostScreen { get; }

        public FastFlagEditorViewModel(IScreen hostScreen)
        {
            HostScreen = hostScreen;
        }
    }
}