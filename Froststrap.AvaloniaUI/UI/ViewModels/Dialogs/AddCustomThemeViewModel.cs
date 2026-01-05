namespace Froststrap.UI.ViewModels.Dialogs
{
    internal class AddCustomThemeViewModel : NotifyPropertyChangedViewModel
    {
        public static CustomThemeTemplate[] Templates => Enum.GetValues<CustomThemeTemplate>();

        public CustomThemeTemplate Template { get; set; } = CustomThemeTemplate.Simple;

        public string Name { get; set; } = "";

        private string _filePath = "";
        public string FilePath
        {
            get => _filePath;
            set
            {
                if (_filePath != value)
                {
                    _filePath = value;
                    OnPropertyChanged(nameof(FilePath));
                    OnPropertyChanged(nameof(FilePathVisibility));
                }
            }
        }

        public bool FilePathVisibility => !string.IsNullOrEmpty(FilePath);

        public int SelectedTab { get; set; } = 0;

        private string _nameError = "";
        public string NameError
        {
            get => _nameError;
            set
            {
                if (_nameError != value)
                {
                    _nameError = value;
                    OnPropertyChanged(nameof(NameError));
                    OnPropertyChanged(nameof(NameErrorVisibility));
                }
            }
        }

        public bool NameErrorVisibility => !string.IsNullOrEmpty(NameError);

        private string _fileError = "";
        public string FileError
        {
            get => _fileError;
            set
            {
                if (_fileError != value)
                {
                    _fileError = value;
                    OnPropertyChanged(nameof(FileError));
                    OnPropertyChanged(nameof(FileErrorVisibility));
                }
            }
        }

        public bool FileErrorVisibility => !string.IsNullOrEmpty(FileError);
    }
}