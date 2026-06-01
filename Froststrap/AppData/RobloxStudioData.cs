namespace Froststrap.AppData
{
    public class RobloxStudioData : CommonAppData, IAppData
    {
        public string ProductName => "Roblox Studio";

        // We are using wine to run linux studio, so we need to use the windows binary name.
        public override string BinaryType => OperatingSystem.IsMacOS() ? "MacStudio" : "WindowsStudio64";

        public string RegistryName => "RobloxStudio";

        public string ProcessName => OperatingSystem.IsMacOS() ? "RobloxStudio" : "RobloxStudioBeta";

        public override string ExecutableName => App.RobloxStudioAppName;

        public override JsonManager<DistributionState> DistributionStateManager => App.StudioState;
    }
}