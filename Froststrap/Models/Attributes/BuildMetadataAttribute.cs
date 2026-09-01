namespace Froststrap.Models.Attributes
{
    [AttributeUsage(AttributeTargets.Assembly)]
    internal sealed class BuildMetadataAttribute(string timestamp, string machine, string commitHash, string commitRef) : Attribute
    {
        public DateTime Timestamp { get; } = DateTime.Parse(timestamp, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal).ToLocalTime();
        public string Machine { get; } = machine;
        public string CommitHash { get; } = commitHash;
        public string CommitRef { get; } = commitRef;
    }
}