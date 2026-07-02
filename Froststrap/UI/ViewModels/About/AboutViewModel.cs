namespace Froststrap.UI.ViewModels.About
{
    public class AboutViewModel : NotifyPropertyChangedViewModel
    {
        public static string Version => string.Format(Strings.Menu_About_Version, App.Version);

        public static BuildMetadataAttribute BuildMetadata => App.BuildMetadata;

        public static string BuildTimestamp => BuildMetadata.Timestamp.ToFriendlyString();
        public static string BuildCommitHashUrl => $"https://github.com/{App.ProjectRepository}/commit/{BuildMetadata.CommitHash}";
        public static bool BuildInformationVisibility => !App.IsProductionBuild;
        public static bool BuildCommitVisibility => App.IsActionBuild;
    }
}