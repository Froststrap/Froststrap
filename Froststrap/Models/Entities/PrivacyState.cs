namespace Froststrap.Models.Entities
{
    internal class PrivacyState
    {
        public PrivacyLevel? Online { get; init; }

        public PrivacyLevel? Join { get; init; }

        public IReadOnlyList<PrivacyLevel> OnlineOptions { get; init; } = [];

        public IReadOnlyList<PrivacyLevel> JoinOptions { get; init; } = [];
    }
}