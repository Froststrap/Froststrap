using Froststrap.AppData;

namespace Froststrap.Models.Distribution
{
    internal abstract class CommonDist : IDistribution
    {
        public virtual string RobloxDomain { get; } = "roblox.com";

        public List<string> CdnUrls { get; } =
        [
            "https://setup.rbxcdn.com",
            "https://setup-ak.rbxcdn.com",
            "https://setup-aws.rbxcdn.com",
            "https://setup-cfly.rbxcdn.com",
            "https://s3.amazonaws.com/setup.roblox.com"
        ];

        public abstract IAppData RobloxPlayerData { get; }
        public abstract IAppData RobloxStudioData { get; }
    }
}
