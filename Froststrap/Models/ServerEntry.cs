namespace Froststrap.Models
{
    public class ServerEntry
    {
        public int Number { get; set; }
        public string ServerId { get; set; } = null!;
        public string Players { get; set; } = null!;
        public string Region { get; set; } = null!;
        public int? DataCenterId { get; set; }
        public string Uptime { get; set; } = "Loading...";
        public System.Windows.Input.ICommand? JoinCommand { get; set; }
    }
}