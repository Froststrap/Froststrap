using System.Collections.ObjectModel;

namespace Bloxstrap.UI.ViewModels.Settings
{
    public class BehaviourViewModel : NotifyPropertyChangedViewModel
    {

        public BehaviourViewModel()
        {

            foreach (var entry in RobloxIconEx.Selections)
                RobloxIcons.Add(new RobloxIconEntry { IconType = (RobloxIcon)entry });

            App.Cookies.StateChanged += (object? _, CookieState state) => CookieLoadingFailed = state != CookieState.Success && state != CookieState.Unknown;
        }

        public ObservableCollection<ProcessPriorityOption> ProcessPriorityOptions { get; } =
            new ObservableCollection<ProcessPriorityOption>(Enum.GetValues(typeof(ProcessPriorityOption)).Cast<ProcessPriorityOption>());

        public ProcessPriorityOption SelectedPriority
        {
            get => App.Settings.Prop.SelectedProcessPriority;
            set => App.Settings.Prop.SelectedProcessPriority = value;
        }

        public bool MultiInstances
        {
            get => App.Settings.Prop.MultiInstanceLaunching;
            set
            {
                App.Settings.Prop.MultiInstanceLaunching = value;

                if (!value)
                {
                    Error773Fix = false;
                    OnPropertyChanged(nameof(Error773Fix));
                }

                OnPropertyChanged(nameof(MultiInstances));
            }
        }

        public bool Error773Fix
        {
            get => App.Settings.Prop.Error773Fix;
            set => App.Settings.Prop.Error773Fix = value;
        }

        public bool BackgroundUpdates
        {
            get => App.Settings.Prop.BackgroundUpdatesEnabled;
            set => App.Settings.Prop.BackgroundUpdatesEnabled = value;
        }

        public bool CloseCrashHandler
        {
            get => App.Settings.Prop.AutoCloseCrashHandler;
            set => App.Settings.Prop.AutoCloseCrashHandler = value;
        }

        public bool ConfirmLaunches
        {
            get => App.Settings.Prop.ConfirmLaunches;
            set => App.Settings.Prop.ConfirmLaunches = value;
        }

        private string _newProcessName = "";
        public string NewProcessName
        {
            get => _newProcessName;
            set
            {
                _newProcessName = value;
                OnPropertyChanged(nameof(NewProcessName));
            }
        }

        private string _selectedProcess = "";
        public string SelectedProcess
        {
            get => _selectedProcess;
            set
            {
                _selectedProcess = value;
                EditProcessName = value;
                OnPropertyChanged(nameof(SelectedProcess));
                OnPropertyChanged(nameof(IsProcessSelected));
            }
        }

        private string _editProcessName = "";
        public string EditProcessName
        {
            get => _editProcessName;
            set
            {
                _editProcessName = value;
                OnPropertyChanged(nameof(EditProcessName));
            }
        }

        public bool IsProcessSelected => !string.IsNullOrEmpty(SelectedProcess);

        public IEnumerable<MemoryCleanerInterval> MemoryCleanerIntervals { get; } = Enum.GetValues(typeof(MemoryCleanerInterval)).Cast<MemoryCleanerInterval>();

        public MemoryCleanerInterval MemoryCleanerInterval
        {
            get => App.Settings.Prop.MemoryCleanerInterval;
            set
            {
                App.Settings.Prop.MemoryCleanerInterval = value;
                OnPropertyChanged(nameof(MemoryCleanerInterval));
                OnPropertyChanged(nameof(MemoryCleanerExclusionsExpanded));
            }
        }

        public ObservableCollection<string> UserExcludedProcesses => App.Settings.Prop.UserExcludedProcesses;

        public bool MemoryCleanerExclusionsExpanded => MemoryCleanerInterval != MemoryCleanerInterval.Never;

        public void AddProcessExclusion(string processName)
        {
            if (string.IsNullOrWhiteSpace(processName))
                return;

            var cleanName = processName.Trim().ToLower();

            if (!UserExcludedProcesses.Contains(cleanName, StringComparer.OrdinalIgnoreCase))
            {
                UserExcludedProcesses.Add(cleanName);
                OnPropertyChanged(nameof(UserExcludedProcesses));
                NewProcessName = "";
            }
        }

        public void RemoveProcessExclusion(string processName)
        {
            var item = UserExcludedProcesses.FirstOrDefault(p => p.Equals(processName, StringComparison.OrdinalIgnoreCase));

            if (item != null)
            {
                UserExcludedProcesses.Remove(item);
                OnPropertyChanged(nameof(UserExcludedProcesses));
                SelectedProcess = "";
                EditProcessName = "";
            }
        }

        public void UpdateProcessExclusion(string oldName, string newName)
        {
            if (string.IsNullOrWhiteSpace(oldName) || string.IsNullOrWhiteSpace(newName))
                return;

            var cleanNewName = newName.Trim().ToLower();

            if (UserExcludedProcesses.Any(p =>
                p.Equals(cleanNewName, StringComparison.OrdinalIgnoreCase) && !p.Equals(oldName, StringComparison.OrdinalIgnoreCase)))
            {
                return;
            }

            var index = UserExcludedProcesses.IndexOf(oldName);

            if (index >= 0)
            {
                UserExcludedProcesses[index] = cleanNewName;
                OnPropertyChanged(nameof(UserExcludedProcesses));
                SelectedProcess = cleanNewName;
                EditProcessName = cleanNewName;
            }
        }

        public bool CookieLoadingFinished => true;

        public bool CookieAccess
        {
            get => App.Settings.Prop.AllowCookieAccess;
            set
            {
                App.Settings.Prop.AllowCookieAccess = value;
                if (value)
                    Task.Run(App.Cookies.LoadCookies);

                OnPropertyChanged(nameof(CookieAccess));
            }
        }

        private bool _cookieLoadingFailed;
        public bool CookieLoadingFailed
        {
            get => _cookieLoadingFailed;
            set
            {
                _cookieLoadingFailed = value;
                OnPropertyChanged(nameof(CookieLoadingFailed));
            }
        }

        public IEnumerable<RobloxIcon> RobloxIcon { get; } = Enum.GetValues(typeof(RobloxIcon)).Cast<RobloxIcon>();

        public RobloxIcon SelectedRobloxIcon
        {
            get => App.Settings.Prop.SelectedRobloxIcon;
            set => App.Settings.Prop.SelectedRobloxIcon = value;
        }

        public ObservableCollection<RobloxIconEntry> RobloxIcons { get; set; } = new();

        public CleanerOptions SelectedCleanUpMode
        {
            get => App.Settings.Prop.CleanerOptions;
            set => App.Settings.Prop.CleanerOptions = value;
        }

        public IEnumerable<CleanerOptions> CleanerOptions { get; } = CleanerOptionsEx.Selections;

        public CleanerOptions CleanerOption
        {
            get => App.Settings.Prop.CleanerOptions;
            set
            {
                App.Settings.Prop.CleanerOptions = value;
            }
        }

        private List<string> CleanerItems = App.Settings.Prop.CleanerDirectories;

        public bool CleanerLogs
        {
            get => CleanerItems.Contains("RobloxLogs");
            set
            {
                if (value)
                    CleanerItems.Add("RobloxLogs");
                else
                    CleanerItems.Remove("RobloxLogs"); // should we try catch it?
            }
        }

        public bool CleanerCache
        {
            get => CleanerItems.Contains("RobloxCache");
            set
            {
                if (value)
                    CleanerItems.Add("RobloxCache");
                else
                    CleanerItems.Remove("RobloxCache");
            }
        }

        public bool CleanerFroststrap
        {
            get => CleanerItems.Contains("FroststrapLogs");
            set
            {
                if (value)
                    CleanerItems.Add("FroststrapLogs");
                else
                    CleanerItems.Remove("FroststrapLogs");
            }
        }
    }
}
