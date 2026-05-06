namespace Froststrap.Models.Attributes
{
    [AttributeUsage(AttributeTargets.Assembly)]
    public class BuildMetadataAttribute(string timestamp, string machine, string commitHash, string commitRef) : Attribute
    {
        public DateTime Timestamp { get; set; } = DateTime.Parse(timestamp).ToLocalTime();
        public string Machine { get; set; } = machine;
        public string CommitHash { get; set; } = commitHash;
        public string CommitRef { get; set; } = commitRef;
    }
}