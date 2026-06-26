using Avalonia;
using Avalonia.Controls;
using Froststrap.UI.ViewModels.Settings;

namespace Froststrap.UI.Elements.Settings.Pages
{
    /// <summary>
    /// Implementation of IDialogService for FastFlags editing
    /// </summary>
    public class FastFlagsDialogService(MainWindowViewModel mainVm) : IDialogService
    {
        private readonly MainWindowViewModel _mainVm = mainVm ?? throw new ArgumentNullException(nameof(mainVm));

        public Task OpenFastFlagEditorAsync()
        {
            _mainVm.NavigateToFastFlagEditorCommand.Execute(null);
            return Task.CompletedTask;
        }
    }

    public partial class FastFlagsPage : UserControl
    {
        public FastFlagsPage()
        {
            InitializeComponent();
            App.FrostRPC?.SetPage("FastFlags Settings");
        }
    }
}