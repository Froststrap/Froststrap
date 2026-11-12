namespace Froststrap.Models.Attributes
{
    [AttributeUsage(AttributeTargets.Assembly)]
    internal sealed class BuildMetadataAttribute(string timestamp, string machine, string commitHash, string commitRef) : Attribute
    {
        public string Timestamp { get; } = timestamp;
        public string Machine { get; } = machine;
        public string CommitHash { get; } = commitHash;
        public string CommitRef { get; } = commitRef;

        public DateTime LocalTimestamp =>
            DateTime.Parse(Timestamp, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal).ToLocalTime();
    }
}
