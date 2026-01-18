namespace Froststrap.Models
{
    internal class WatcherData
    {
        public int ProcessId { get; set; }

        public string? LogFile { get; set; }

        public List<int>? AutoclosePids { get; set; }

        public LaunchMode LaunchMode { get; set; } = LaunchMode.Player;
    }
}
