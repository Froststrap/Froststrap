using Avalonia.Interactivity;
using Froststrap.UI.ViewModels.Dialogs;

namespace Froststrap.UI.Elements.Dialogs
{
    public partial class AdvancedSettingsDialog : Base.AvaloniaWindow
    {
        public AdvancedSettingViewModel ViewModel { get; } = new();
        public static AdvancedSettingViewModel SharedViewModel { get; private set; } = new();

        public event EventHandler? SettingsSaved;

        public AdvancedSettingsDialog()
        {
            InitializeComponent();
            DataContext = ViewModel;
            SharedViewModel = ViewModel;
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            App.Settings.Save();
            SettingsSaved?.Invoke(this, EventArgs.Empty);
        }
    }
}