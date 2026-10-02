using Froststrap.Enums.Overlay;
using Froststrap.UI.ViewModels;

namespace Froststrap.Models.Overlay
{
    internal class ChatFriend : NotifyPropertyChangedViewModel
    {
        private static readonly string[] PresenceProperties =
        [
            nameof(Status),
            nameof(IsInGame),
            nameof(IsInStudio),
            nameof(IsOnline),
            nameof(GameName),
            nameof(PlaceId),
            nameof(RootPlaceId),
            nameof(ServerId),
            nameof(CanJoin),
            nameof(CanViewGame),
            nameof(StatusText)
        ];

        public long UserId { get; init; }

        public bool IsGroup { get; init; }

        public int MemberCount { get; init; }

        public string DisplayName { get; init; } = String.Empty;

        public string Username { get; init; } = String.Empty;

        public string Handle => IsGroup || String.IsNullOrEmpty(Username) ? String.Empty : $"@{Username}";

        private string? _conversationId;
        public string? ConversationId
        {
            get => _conversationId;
            set
            {
                if (_conversationId == value)
                    return;

                _conversationId = value;

                OnPropertyChanged(nameof(ConversationId));
            }
        }

        private string? _headshot;
        public string? Headshot
        {
            get => _headshot;
            set
            {
                if (_headshot == value)
                    return;

                _headshot = value;

                OnPropertyChanged(nameof(Headshot));
            }
        }

        public FriendStatus Status { get; private set; } = FriendStatus.Offline;

        public bool IsInGame => Status == FriendStatus.InGame;

        public bool IsInStudio => Status == FriendStatus.InStudio;

        public bool IsOnline => Status == FriendStatus.Online;

        public string? GameName { get; private set; }

        public long PlaceId { get; private set; }

        public long RootPlaceId { get; private set; }

        public string? ServerId { get; private set; }

        public bool CanJoin => IsInGame && PlaceId > 0 && !String.IsNullOrEmpty(ServerId);

        public bool CanViewGame => IsInGame && !CanJoin && (RootPlaceId > 0 || PlaceId > 0);

        public string StatusText
        {
            get
            {
                if (IsGroup)
                    return String.Format(Locale.CurrentCulture, Strings.Menu_Overlay_Messages_Members, MemberCount);

                return Status switch
                {
                    FriendStatus.InGame => String.IsNullOrEmpty(GameName)
                        ? Strings.Menu_Overlay_Messages_InAGame
                        : String.Format(Locale.CurrentCulture, Strings.Menu_Overlay_Messages_Playing, GameName),
                    FriendStatus.InStudio => Strings.Menu_Overlay_Messages_InStudio,
                    FriendStatus.Online => Strings.Menu_Overlay_Messages_Online,
                    _ => Strings.Menu_Overlay_Messages_Offline
                };
            }
        }

        private int _unread;
        public int Unread
        {
            get => _unread;
            set
            {
                int clamped = Math.Max(value, 0);

                if (_unread == clamped)
                    return;

                _unread = clamped;

                OnPropertyChanged(nameof(Unread));
                OnPropertyChanged(nameof(HasUnread));
                OnPropertyChanged(nameof(UnreadText));
            }
        }

        public bool HasUnread => _unread > 0;

        public string UnreadText => _unread > 99 ? "99+" : _unread.ToString(Locale.CurrentCulture);

        public bool Matches(string search) =>
            DisplayName.Contains(search, StringComparison.OrdinalIgnoreCase)
            || Username.Contains(search, StringComparison.OrdinalIgnoreCase);

        public void Update(UserPresence? presence)
        {
            Status = presence?.UserPresenceType switch
            {
                UserPresence.InGameType => FriendStatus.InGame,
                UserPresence.InStudioType => FriendStatus.InStudio,
                UserPresence.OnlineType => FriendStatus.Online,
                _ => FriendStatus.Offline
            };

            GameName = IsInGame && !String.IsNullOrWhiteSpace(presence?.LastLocation) ? presence!.LastLocation : null;
            PlaceId = presence?.PlaceId ?? 0;
            RootPlaceId = presence?.RootPlaceId ?? 0;
            ServerId = String.IsNullOrEmpty(presence?.GameId) ? null : presence!.GameId;

            foreach (string name in PresenceProperties)
                OnPropertyChanged(name);
        }
    }
}