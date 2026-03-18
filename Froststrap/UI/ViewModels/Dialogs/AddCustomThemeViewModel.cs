namespace Froststrap.UI.ViewModels.Dialogs;


public class AddCustomThemeViewModel : NotifyPropertyChangedViewModel
{
    public static CustomThemeTemplate[] Templates => Enum.GetValues<CustomThemeTemplate>();

    private CustomThemeTemplate _template = CustomThemeTemplate.Simple;
    public CustomThemeTemplate Template
    {
        get => _template;
        set => SetProperty(ref _template, value);
    }

    private string _name = "";
    public string Name
    {
        get => _name;
        set => SetProperty(ref _name, value);
    }

    private string _filePath = "";
    public string FilePath
    {
        get => _filePath;
        set
        {
            if (SetProperty(ref _filePath, value))
            {
                OnPropertyChanged(nameof(HasFilePath));
            }
        }
    }
    public bool HasFilePath => !string.IsNullOrEmpty(FilePath);

    public int SelectedTab { get; set; } = 0;

    private string _nameError = "";
    public string NameError
    {
        get => _nameError;
        set
        {
            if (SetProperty(ref _nameError, value))
                OnPropertyChanged(nameof(HasNameError));
        }
    }
    public bool HasNameError => !string.IsNullOrEmpty(NameError);

    private string _fileError = "";
    public string FileError
    {
        get => _fileError;
        set
        {
            if (SetProperty(ref _fileError, value))
                OnPropertyChanged(nameof(HasFileError));
        }
    }
    public bool HasFileError => !string.IsNullOrEmpty(FileError);
}