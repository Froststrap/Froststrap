namespace Froststrap.AppData
{
    public class RobloxPlayerData : CommonAppData, IAppData
    {
        public string ProductName => "Roblox";

        public override string BinaryType => "WindowsPlayer";

        public string RegistryName => "RobloxPlayer";

        public string ProcessName => "RobloxPlayerBeta";

        public override string ExecutableName => App.RobloxPlayerAppName;

        public override JsonManager<DistributionState> DistributionStateManager => App.PlayerState;
    }
}