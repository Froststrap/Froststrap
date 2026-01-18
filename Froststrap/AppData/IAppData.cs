namespace Froststrap.AppData
{
    internal interface IAppData
    {
        string ProductName { get; }

        string BinaryType { get; }

        string RegistryName { get; }

        string ProcessName { get; }

        string ExecutableName { get; }

        string StaticDirectory { get; }

        string DynamicDirectory { get; }

        string Directory { get; }

        string ExecutablePath { get; }

        JsonManager<DistributionState> DistributionStateManager { get; }

        DistributionState DistributionState { get; }

        List<string> ModManifest { get; }
    }
}