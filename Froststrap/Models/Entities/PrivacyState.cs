namespace Froststrap.Models.Entities
{
    internal class PrivacyState
    {
        public string? Online { get; init; }

        public string? Join { get; init; }

        public IReadOnlyList<string> OnlineOptions { get; init; } = [];

        public IReadOnlyList<string> JoinOptions { get; init; } = [];
    }
}