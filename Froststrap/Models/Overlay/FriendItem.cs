using Froststrap.Models.APIs.RobloxParty;

namespace Froststrap.Models.Overlay
{
    internal class FriendItem
    {
        public string Username { get; set; } = String.Empty;

        public string StatusText { get; set; } = String.Empty;

        public string? ProfileImage { get; set; }

        public string ConversationId { get; set; } = String.Empty;

        public UserMessagesPage? Data { get; set; }
    }
}
