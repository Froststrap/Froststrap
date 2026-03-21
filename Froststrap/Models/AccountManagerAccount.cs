namespace Froststrap.Models
{
    public record AccountManagerAccount
    {
        public string SecurityToken { get; init; }
        public long UserId { get; init; }
        public string Username { get; init; }
        public string DisplayName { get; init; }
        public DateTime LastUsed { get; init; } = DateTime.UtcNow;

        public AccountManagerAccount(string securityToken, long userId, string username, string displayName)
        {
            SecurityToken = securityToken;
            UserId = userId;
            Username = username;
            DisplayName = displayName;
        }
    }
}