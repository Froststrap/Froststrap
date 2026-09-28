using Froststrap.AppData;

namespace Froststrap.Models.Distribution
{
    internal interface IDistribution
    {
        public string RobloxDomain { get; }
        public List<string> CdnUrls { get; }
        public IAppData RobloxPlayerData { get; }
        public IAppData RobloxStudioData { get; }
    }
}
