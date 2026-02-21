using System.Collections.ObjectModel;

namespace Bloxstrap.Models.Persistable
{
    public class Settings
    {
        // Fishstrap feature to use private channel.
        public bool AllowCookieAccess { get; set; } = false;

        // Integration Page
        public bool EnableActivityTracking { get; set; } = true;
        public bool ShowServerDetails { get; set; } = true;
        public bool ShowServerUptime { get; set; } = false;
        public bool AutoRejoin { get; set; } = false;
        public bool ShowGameHistoryMenu { get; set; } = true;
        public bool PlaytimeCounter { get; set; } = true;
        public TrayDoubleClickAction DoubleClickAction { get; set; } = TrayDoubleClickAction.ServerInfo;
        public bool UseDisableAppPatch { get; set; } = false;
        public bool ShowUsingFroststrapRPC { get; set; } = true;
        public bool UseDiscordRichPresence { get; set; } = true;
        public bool HideRPCButtons { get; set; } = true;
        public bool EnableCustomStatusDisplay { get; set; } = true;
        public bool ShowAccountOnRichPresence { get; set; } = false;
        public bool StudioRPC { get; set; } = false;
        public bool StudioThumbnailChanging { get; set; } = false;
        public bool StudioEditingInfo { get; set; } = false;
        public bool StudioWorkspaceInfo { get; set; } = false;
        public bool StudioShowTesting { get; set; } = false;
        public bool StudioGameButton { get; set; } = false;
        public ObservableCollection<CustomIntegration> CustomIntegrations { get; set; } = new();

        // Bootstrapper Page
        public bool ConfirmLaunches { get; set; } = true;
        public bool AutoCloseCrashHandler { get; set; } = false;
        public MemoryCleanerInterval MemoryCleanerInterval { get; set; } = MemoryCleanerInterval.Never;
        public ObservableCollection<string> UserExcludedProcesses { get; set; } = new();
        public int RobloxTrimIntervalSeconds { get; set; } = 600;
        // i have no idea why i set it to true :sob:
        public bool EnableRobloxTrim { get; set; } = false;
        public string Locale { get; set; } = "nil";
        public CleanerOptions CleanerOptions { get; set; } = CleanerOptions.Never;
        public List<string> CleanerDirectories { get; set; } = new List<string>();
        public bool BackgroundUpdatesEnabled { get; set; } = false;
        public bool MultiInstanceLaunching { get; set; } = false;
        public bool Error773Fix { get; set; } = false;
        public int MultibloxInstanceCount { get; set; } = 2;
        public int MultibloxDelayMs { get; set; } = 1500;
        public RobloxIcon SelectedRobloxIcon { get; set; } = RobloxIcon.Default;
        public ProcessPriorityOption SelectedProcessPriority { get; set; } = ProcessPriorityOption.Normal;

        // FastFlag Editor/Settings Related
        public bool UseFastFlagManager { get; set; } = true;
        public bool ShowPresetColumn { get; set; } = false;
        public bool ShowFlagCount { get; set; } = true;
        public bool UseAltManually { get; set; } = true;

        // Appearance Page
        public BootstrapperStyle BootstrapperStyle { get; set; } = BootstrapperStyle.FroststrapDialog;
        public BootstrapperIcon BootstrapperIcon { get; set; } = BootstrapperIcon.IconBloxstrap;
        public WindowsBackdrops SelectedBackdrop { get; set; } = WindowsBackdrops.Mica;
        public string? SelectedCustomTheme { get; set; } = null;
        public List<GradientStops> CustomGradientStops { get; set; } = new()
        {
            new GradientStops { Offset = 0.0, Color = "#4D5560" },
            new GradientStops { Offset = 0.5, Color = "#383F47" },
            new GradientStops { Offset = 1.0, Color = "#252A30" }
        };
        public double GradientAngle { get; set; } = 0;
        public BackgroundMode BackgroundType { get; set; } = BackgroundMode.Gradient;
        public string? BackgroundImagePath { get; set; }
        public BackgroundStretch BackgroundStretch { get; set; } = BackgroundStretch.UniformToFill;
        public double BackgroundOpacity { get; set; } = 1.0;
        public string BootstrapperTitle { get; set; } = App.ProjectName;
        public string BootstrapperIconCustomLocation { get; set; } = "";
        public string DownloadingStringFormat { get; set; } = Strings.Bootstrapper_Status_Downloading + " {0} - {1}MB / {2}MB";
        public Theme Theme { get; set; } = Theme.Default;
        public string? CustomFontPath { get; set; } = null;
        public List<string> NavigationOrder { get; set; } = new List<string>();
        public bool IsNavigationOrderLocked { get; set; } = true;

        // Settings Page
        public bool CheckForUpdates { get; set; } = true;
        public bool WPFSoftwareRender { get; set; } = false;
        public bool UpdateRoblox { get; set; } = true;
        public string RobloxDomain { get; set; } = RobloxInterfaces.Deployment.DefaultRobloxDomain;
        public bool StaticDirectory { get; set; } = false;
        public string Channel { get; set; } = RobloxInterfaces.Deployment.DefaultChannel;
        public ChannelChangeMode ChannelChangeMode { get; set; } = ChannelChangeMode.Prompt;

        // Misc Stuff
        public bool IsNavigationSidebarExpanded { get; set; } = true;
        public string SelectedRegion { get; set; } = string.Empty;
        public bool ForceLocalData { get; set; } = false;
        public bool DebugDisableVersionPackageCleanup { get; set; } = false;
    }
}