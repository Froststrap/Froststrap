using Froststrap.UI.ViewModels;

namespace Froststrap.Models
{
    public class ModConfig : NotifyPropertyChangedViewModel
    {
        private string _folderName = "";
        private int _priority = 0;
        private ModTarget _target = ModTarget.Both;
        private bool _enabled = true;

        public string FolderName
        {
            get => _folderName;
            set
            {
                if (SetProperty(ref _folderName, value))
                    OnPropertyChanged(nameof(FileCount));
            }
        }

        public int Priority
        {
            get => _priority;
            set => SetProperty(ref _priority, value);
        }

        public ModTarget Target
        {
            get => _target;
            set => SetProperty(ref _target, value);
        }

        public bool Enabled
        {
            get => _enabled;
            set => SetProperty(ref _enabled, value);
        }

        public int FileCount => GetFileCount();

        public string FileCountDisplay => $"{FileCount} {Strings.Common_Files}";
        public string PriorityDisplay => $"{Strings.Common_Priority}: {Priority}";

        private int GetFileCount()
        {
            if (string.IsNullOrEmpty(FolderName)) return 0;
            string path = Path.Combine(Paths.Modifications, FolderName);
            if (!Directory.Exists(path)) return 0;
            try
            {
                return Directory.GetFiles(path, "*", SearchOption.AllDirectories).Length;
            }
            catch
            {
                return 0;
            }
        }
    }
}