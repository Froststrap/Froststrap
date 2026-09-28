namespace Froststrap.AppData
{
    internal class RobloxPlayerVNGData : CommonAppData, IAppData
    {
        public string ProductName => "Roblox VNG";

        public override string BinaryType => OperatingSystem.IsMacOS() ? "MacPlayer" : "WindowsPlayer";

        // Not sure if this is correct for mac
        public override string AppDataDirectory => OperatingSystem.IsWindows() ? Path.Combine(Paths.LocalAppData, "RobloxPCVNG") : base.AppDataDirectory;

        public string RegistryName => "RobloxPlayer";

        // Not sure if this is correct for mac
        public string ProcessName => OperatingSystem.IsMacOS() ? "RobloxPlayer" : "RobloxPlayerBeta";

        public override string ExecutableName => App.RobloxPlayerAppName;

        public override string CdnExtension => "/vng";

        public override bool SupportsCustomDeployments => false;

        public override JsonManager<DistributionState> DistributionStateManager => App.PlayerState;
    }
}