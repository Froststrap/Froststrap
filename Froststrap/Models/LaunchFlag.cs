namespace Froststrap.Models
{
    public class LaunchFlag(string identifiers)
    {
        public string Identifiers { get; private set; } = identifiers;

        public bool Active = false;

        public string? Data;
    }
}