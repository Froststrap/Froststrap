namespace Froststrap.AppData
{
    public class RobloxStudioData : CommonAppData, IAppData
    {
        public string ProductName => "Roblox Studio";

        // LinuxStudio has no meaning, we need to add an alterantive method (vinegar maybe?)
        public override string BinaryType => OperatingSystem.IsMacOS() ? "MacStudio"
            : OperatingSystem.IsLinux() ? "LinuxStudio"
            : "WindowsStudio64";

        public string RegistryName => "RobloxStudio";

        public string ProcessName => OperatingSystem.IsMacOS() ? "RobloxStudio" : "RobloxStudioBeta";

        public override string ExecutableName => App.RobloxStudioAppName;

        public override JsonManager<DistributionState> DistributionStateManager => App.StudioState;
    }
}
