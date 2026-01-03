namespace Bloxstrap.AppData
{
    public class RobloxStudioData : CommonAppData, IAppData
    {
        public string ProductName => "Roblox Studio";

        public override string BinaryType => "WindowsStudio64";

        public string RegistryName => "RobloxStudio";

        public string ProcessName => "RobloxStudioBeta";

        public override string ExecutableName => App.RobloxStudioAppName;

        public override JsonManager<DistributionState> DistributionStateManager => App.StudioState;
    }
}