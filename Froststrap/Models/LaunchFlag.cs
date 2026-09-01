namespace Froststrap.Models
{
    internal class LaunchFlag(string identifiers)
    {
        public string Identifiers { get; private set; } = identifiers;

        public bool Active;
        public string? Data;
    }
}