using System.ComponentModel;

namespace Bloxstrap.UI.ViewModels.Dialogs
{
    public class AdvancedSettingViewModel : INotifyPropertyChanged
    {
        public static event EventHandler? ShowPresetColumnChanged;
        public static event EventHandler? ShowFlagCountChanged;

        public bool ShowPresetColumnSetting
        {
            get => App.Settings.Prop.ShowPresetColumn;
            set
            {
                App.Settings.Prop.ShowPresetColumn = value;
                OnPropertyChanged(nameof(ShowPresetColumnSetting));
                ShowPresetColumnChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        public bool ShowFlagCount
        {
            get => App.Settings.Prop.ShowFlagCount;
            set
            {
                App.Settings.Prop.ShowFlagCount = value;
                OnPropertyChanged(nameof(ShowFlagCount));
                ShowFlagCountChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        public bool UseAltManually
        {
            get => App.Settings.Prop.UseAltManually;
            set
            {
                App.Settings.Prop.UseAltManually = value;
                OnPropertyChanged(nameof(UseAltManually));
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged(string name) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}