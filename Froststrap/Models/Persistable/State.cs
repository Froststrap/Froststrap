namespace Froststrap.Models.Persistable
{
    public class State
    {
        public bool TestModeWarningShown { get; set; } = false;

        public bool IgnoreOutdatedChannel { get; set; } = false;

        public bool PromptWebView2Install { get; set; } = true;

        public string? LastPage { get; set; } = null!;

        public bool ForceReinstall { get; set; } = false;

        //if we were still windows only i would of just done it in nsis installer
        public bool IsFirstLaunch { get; set; } = true;

        public WindowState SettingsWindow { get; set; } = new();

        public bool IsNavigationPaneOpen { get; set; } = true;

        public string? LastMigratedVersion { get; set; } = null;

        public List<ModConfig> Mods { get; set; } = [];
    }
}