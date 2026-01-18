namespace Froststrap.Models
{
    public class LaunchFlag
    {
        public string Identifiers { get; private set; }

        public bool Active = false;

        public string? Data;

        public LaunchFlag(string identifiers)
        {
            Identifiers = identifiers;
        }
    }
}
