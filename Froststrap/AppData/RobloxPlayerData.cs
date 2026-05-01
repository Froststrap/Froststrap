namespace Froststrap.AppData
{
    public class RobloxPlayerData : CommonAppData, IAppData
    {
        public string ProductName => "Roblox";

        // LinuxPlayer has no meaning, just skips Roblox CDN download and delegates to Sober
        public override string BinaryType => OperatingSystem.IsMacOS() ? "MacPlayer"
            : OperatingSystem.IsLinux() ? "LinuxPlayer"
            : "WindowsPlayer";

        public string RegistryName => "RobloxPlayer";

        public string ProcessName => OperatingSystem.IsMacOS() ? "RobloxPlayer" : "RobloxPlayerBeta";

        public override string ExecutableName => App.RobloxPlayerAppName;

        public override JsonManager<DistributionState> DistributionStateManager => App.PlayerState;
    }
}
