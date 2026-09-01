namespace Froststrap.Models.Persistable
{
    internal class State
    {
        public bool TestModeWarningShown { get; set; }

        public bool IgnoreOutdatedChannel { get; set; }

        public bool PromptWebView2Install { get; set; } = true;

        public string? LastPage { get; set; } = null!;

        public bool ForceReinstall { get; set; }

        //if we were still windows only i would of just done it in nsis installer
        public bool IsFirstLaunch { get; set; } = true;

        public WindowState SettingsWindow { get; set; } = new();

        public bool IsNavigationPaneOpen { get; set; } = true;
        public LaunchMode LastLaunchMode { get; set; } = LaunchMode.Player;

        public string? LastMigratedVersion { get; set; }

        public List<ModConfig> Mods { get; set; } = [];
    }
}