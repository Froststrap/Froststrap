using Froststrap.UI.Elements.Bootstrapper;
using CommunityToolkit.Mvvm.Input;
using System.Windows.Input;
using System.IO;
using System.Diagnostics;

namespace Froststrap.UI.ViewModels.Editor
{
    public partial class BootstrapperEditorWindowViewModel : NotifyPropertyChangedViewModel
    {
        private CustomDialog? _dialog = null;

        private string _code = "";
        private bool _codeChanged = false;
        private string _title = "Editing \"Custom Theme\"";

        public ICommand PreviewCommand => new RelayCommand(Preview);
        public ICommand SaveCommand => new RelayCommand(Save);
        public ICommand OpenThemeFolderCommand => new RelayCommand(OpenThemeFolder);

        public Action<bool, string> ThemeSavedCallback { get; set; } = null!;

        public string Directory { get; set; } = "";
        public string Name { get; set; } = "";

        public string Title
        {
            get => _title;
            set => SetProperty(ref _title, value);
        }

        public string Code
        {
            get => _code;
            set => SetProperty(ref _code, value);
        }

        public bool CodeChanged
        {
            get => _codeChanged;
            set => SetProperty(ref _codeChanged, value);
        }

        private void Preview()
        {
            const string LOG_IDENT = "BootstrapperEditorWindowViewModel::Preview";
            try
            {
                CustomDialog dialog = new CustomDialog();
                dialog.ApplyCustomTheme(Name, Code);

                _dialog?.CloseBootstrapper();
                _dialog = dialog;

                dialog.Message = Strings.Bootstrapper_StylePreview_TextCancel;
                dialog.CancelEnabled = true;
                dialog.ShowBootstrapper();
            }
            catch (Exception ex)
            {
                App.Logger.WriteException(LOG_IDENT, ex);
                _ = Frontend.ShowMessageBox(
                    string.Format(Strings.CustomTheme_Editor_Errors_PreviewFailed, ex.Message),
                    MessageBoxImage.Error,
                    MessageBoxButton.OK
                );
            }
        }

        private void Save()
        {
            const string LOG_IDENT = "BootstrapperEditorWindowViewModel::Save";
            string path = Path.Combine(Directory, "Theme.xml");

            try
            {
                File.WriteAllText(path, Code);
                CodeChanged = false;
                ThemeSavedCallback?.Invoke(true, Strings.CustomTheme_Editor_Save_Success_Description);
            }
            catch (Exception ex)
            {
                App.Logger.WriteException(LOG_IDENT, ex);
                ThemeSavedCallback?.Invoke(false, ex.Message);
            }
        }

        private void OpenThemeFolder()
        {
            if (string.IsNullOrEmpty(Directory)) return;
            Process.Start(new ProcessStartInfo{ 
                FileName = Directory, 
                UseShellExecute = true, 
                Verb = "open" 
            });
        }
    }
}