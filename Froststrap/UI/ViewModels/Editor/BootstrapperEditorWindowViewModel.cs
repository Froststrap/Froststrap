using Froststrap.UI.Elements.Bootstrapper;
using CommunityToolkit.Mvvm.Input;
using System.Windows.Input;

namespace Froststrap.UI.ViewModels.Editor
{
    internal partial class BootstrapperEditorWindowViewModel : NotifyPropertyChangedViewModel
    {
        private CustomDialog? _dialog;

        public ICommand PreviewCommand => new RelayCommand(Preview);
        public ICommand SaveCommand => new RelayCommand(Save);
        public ICommand OpenThemeFolderCommand => new RelayCommand(OpenThemeFolder);

        public Action<bool, string> ThemeSavedCallback { get; set; } = null!;

        public string Directory { get; set; } = "";
        public string Name { get; set; } = "";
        public string Title { get; set; } = "Editing \"Custom Theme\"";
        public string Code { get; set; } = "";
        public bool CodeChanged { get; set; }

        private void Preview()
        {
            try
            {
                CustomDialog dialog = new();
                dialog.ApplyCustomTheme(Name, Code);

                _dialog?.CloseBootstrapper();
                _dialog = dialog;

                dialog.Message = Strings.Bootstrapper_StylePreview_TextCancel;
                dialog.CancelEnabled = true;
                dialog.ShowBootstrapper();
            }
            catch (Exception ex)
            {
                App.Logger.Error("Unhandled exception: ", ex);
                _ = Frontend.ShowMessageBox(
                    string.Format(CultureInfo.InvariantCulture, Strings.CustomTheme_Editor_Errors_PreviewFailed, ex.Message),
                    MessageBoxImage.Error,
                    MessageBoxButton.OK
                );
            }
        }

        private void Save()
        {
            string path = Path.Combine(Directory, "Theme.xml");

            try
            {
                File.WriteAllText(path, Code);
                CodeChanged = false;
                ThemeSavedCallback?.Invoke(true, Strings.CustomTheme_Editor_Save_Success_Description);
            }
            catch (Exception ex)
            {
                App.Logger.Error("Unhandled exception: ", ex);
                ThemeSavedCallback?.Invoke(false, ex.Message);
            }
        }

        private void OpenThemeFolder()
        {
            if (string.IsNullOrEmpty(Directory)) return;
            Process.Start(new ProcessStartInfo
            {
                FileName = Directory,
                UseShellExecute = true,
                Verb = "open"
            });
        }
    }
}
