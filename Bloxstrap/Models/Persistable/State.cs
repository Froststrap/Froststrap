using System.Windows.Forms;

namespace Bloxstrap.Models.Persistable
{
    public class State
    {
        public bool TestModeWarningShown { get; set; } = false;

        public bool IgnoreOutdatedChannel { get; set; } = false;

        public bool WatcherRunning { get; set; } = false;

        public bool PromptWebView2Install { get; set; } = true;

        public string? LastPage { get; set; } = null!;

        public bool ForceReinstall { get; set; } = false;

        public WindowState SettingsWindow { get; set; } = new();

        public List<ModConfig> Mods { get; set; } = new();
    }
}