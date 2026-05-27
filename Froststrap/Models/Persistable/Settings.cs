using System.Collections.ObjectModel;

namespace Froststrap.Models.Persistable
{
    public class Settings
    {

        // Integration Page
        public bool EnableActivityTracking { get; set; } = true;
        public bool ShowServerDetails { get; set; } = true;
        public bool AutoRejoin { get; set; } = false;
        public bool ShowGameHistoryMenu { get; set; } = true;
        public bool PlaytimeCounter { get; set; } = true;
        public TrayDoubleClickAction DoubleClickAction { get; set; } = TrayDoubleClickAction.ServerInfo;
        public bool UseDisableAppPatch { get; set; } = false;
        public bool AutoChangeTitle { get; set; } = true;
        public bool AutoChangeIcon { get; set; } = false;
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
        
        // Windows control settings
        public bool UseWindowControl { get; set; } = false;
        public bool MoveWindowAllowed { get; set; } = false;
        public bool TitleControlAllowed { get; set; } = false;
        public bool WindowTransparencyAllowed { get; set; } = false;
        public bool CanGameChangeColor { get; set; } = false;
        public WindowMonitorStyle WindowMonitorStyle { get; set; } = WindowMonitorStyle.Single;
        public int WindowReadFPS { get; set; } = 60;
        public bool WindowAllowAll { get; set; } = false;
        public ObservableCollection<long> WindowAllowedUniverses { get; set; } = [];
        public ObservableCollection<long> WindowBlacklistedUniverses { get; set; } = [];

        public ObservableCollection<CustomIntegration> CustomIntegrations { get; set; } = [];

        // Bootstrapper Page
        public bool ConfirmLaunches { get; set; } = true;
        public bool AllowCookieAccess { get; set; } = false;
        public bool AutoCloseCrashHandler { get; set; } = false;
        public CleanerOptions CleanerOptions { get; set; } = CleanerOptions.Never;
        public List<string> CleanerDirectories { get; set; } = [];
        public bool BackgroundUpdatesEnabled { get; set; } = false;
        public bool MultiInstanceLaunching { get; set; } = false;
        public bool Error773Fix { get; set; } = false;
        public bool EnableBetterMatchmaking { get; set; } = false;
        public bool EnableBetterMatchmakingRandomization { get; set; } = false;
        public ProcessPriorityOption SelectedProcessPriority { get; set; } = ProcessPriorityOption.Normal;

        // FastFlag Editor/Settings Related
        public bool UseFastFlagManager { get; set; } = true;

        // Appearance Page
        public BootstrapperStyle BootstrapperStyle { get; set; } = BootstrapperStyle.FluentAeroDialog;
        public string? SelectedCustomTheme { get; set; } = null;
        public bool CycleEnabled { get; set; }
        public CycleFrequency CycleFrequency { get; set; } = CycleFrequency.EveryLaunch;
        public int CycleIntervalValue { get; set; } = 1;
        public List<string> CycleEnabledCustomThemes { get; set; } = [];
        public int CycleCurrentIndex { get; set; }
        public DateTime CycleLastCycleTime { get; set; } = DateTime.MinValue;
        public BootstrapperIcon BootstrapperIcon { get; set; } = BootstrapperIcon.IconFroststrap;
        public WindowsBackdrops SelectedBackdrop { get; set; } = WindowsBackdrops.None;
        public string Locale { get; set; } = "nil";
        public List<GradientStops> CustomGradientStops { get; set; } =
        [
            new GradientStops { Offset = 0.0, Color = "#4D5560" },
            new GradientStops { Offset = 0.5, Color = "#383F47" },
            new GradientStops { Offset = 1.0, Color = "#252A30" }
        ];
        public double GradientAngle { get; set; } = 0;
        public BackgroundMode BackgroundType { get; set; } = BackgroundMode.Gradient;
        public string? BackgroundImagePath { get; set; } = "";
        public BackgroundStretch BackgroundStretch { get; set; } = BackgroundStretch.UniformToFill;
        public double BackgroundOpacity { get; set; } = 1.0;
        public string BootstrapperTitle { get; set; } = App.ProjectName;
        public string BootstrapperIconCustomLocation { get; set; } = "";
        public int MaxThreadDownload { get; set; } = 3;
        public Theme Theme { get; set; } = Theme.Default;

        // Settings Page
        public UpdateCheck UpdateChecks { get; set; } = UpdateCheck.Stable;
        public bool UpdateRoblox { get; set; } = true;
        public bool AutomaticallyUpdateSober { get; set; } = true;
        public string RobloxDomain { get; set; } = RobloxInterfaces.Deployment.DefaultRobloxDomain;
        public bool StaticDirectory { get; set; } = false;
        public string Channel { get; set; } = RobloxInterfaces.Deployment.DefaultChannel;
        public ChannelChangeMode ChannelChangeMode { get; set; } = ChannelChangeMode.Prompt;
        public StudioRenderer StudioRenderer { get; set; } = StudioRenderer.DXVK;
        public bool StudioVersionOverrideEnabled { get; set; } = false;
        public string StudioVersionOverrideHash { get; set; } = string.Empty;
        public bool StudioGameMode { get; set; } = false;
        public bool StudioDebug { get; set; } = false;

        // Misc Stuff
        public string SelectedRegion { get; set; } = string.Empty;
        public bool DisableGameSearch { get; set; } = false;
        public bool ForceLocalData { get; set; } = false;
        public bool DebugDisableVersionPackageCleanup { get; set; } = false;
    }
}
