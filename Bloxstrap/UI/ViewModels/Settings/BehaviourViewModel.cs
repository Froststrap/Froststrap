using System.Collections.ObjectModel;
using System.Windows;

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

        public ObservableCollection<ProcessPriorityOption> ProcessPriorityOptions { get; } = new ObservableCollection<ProcessPriorityOption>(Enum.GetValues(typeof(ProcessPriorityOption)).Cast<ProcessPriorityOption>());

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
                if (value)
                {
                    var result = Frontend.ShowMessageBox(
                        "Roblox stated that multi-instance launching is considered an exploit, but it isn't bannable.\n\n" +
                        "Are you sure you want to enable multi-instance launching?",
                        MessageBoxImage.Warning,
                        MessageBoxButton.YesNo
                    );

                    if (result != MessageBoxResult.Yes)
                    {
                        return;
                    }
                }

                App.Settings.Prop.MultiInstanceLaunching = value;

                if (!value)
                {
                    Error773Fix = false;
                    OnPropertyChanged(nameof(Error773Fix));
                }

                OnPropertyChanged(nameof(MultiInstances));
            }
        }

        public bool FramerateUncap
        {
            get
            {
                string? value = App.GlobalSettings.GetPresets("Rendering.FramerateCap");
                return int.TryParse(value, out int framerate) && framerate > 240;
            }
            set
            {
                App.GlobalSettings.SetPresets("Rendering.FramerateCap", value ? "9999" : "-1");

                if (value)
                    App.GlobalSettings.SetReadOnly(true);
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
                    CleanerItems.Remove("RobloxLogs");
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