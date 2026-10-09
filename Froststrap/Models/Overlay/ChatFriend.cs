using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Froststrap.Enums;
using Froststrap.Models.APIs.Roblox;
using Froststrap.UI.ViewModels;
using Froststrap.Utility;

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

                _ = LoadHeadshotAsync();
            }
        }

        private Bitmap? _headshotBitmap;
        public Bitmap? HeadshotBitmap
        {
            get => _headshotBitmap;
            private set
            {
                if (_headshotBitmap == value)
                    return;

                _headshotBitmap = value;

                OnPropertyChanged(nameof(HeadshotBitmap));
            }
        }

        private bool _loadingHeadshot;
        private string? _loadedHeadshotUrl;

        private async Task LoadHeadshotAsync()
        {
            string? url = _headshot;

            if (_loadingHeadshot || String.IsNullOrEmpty(url) || _loadedHeadshotUrl == url)
                return;

            _loadingHeadshot = true;

            try
            {
                Bitmap? bitmap = await ImageLoader.LoadAsync(url, 72);

                if (bitmap is null)
                    return;

                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    if (_headshot == url)
                    {
                        HeadshotBitmap = bitmap;
                        _loadedHeadshotUrl = url;
                    }
                });
            }
            finally
            {
                _loadingHeadshot = false;
            }
        }

        public UserPresenceType Status { get; private set; } = UserPresenceType.Offline;

        public bool IsInGame => Status == UserPresenceType.InGame;

        public bool IsInStudio => Status == UserPresenceType.InStudio;

        public bool IsOnline => Status == UserPresenceType.Online;

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
                    UserPresenceType.InGame => String.IsNullOrEmpty(GameName)
                        ? Strings.Menu_Overlay_Messages_InAGame
                        : String.Format(Locale.CurrentCulture, Strings.Menu_Overlay_Messages_Playing, GameName),
                    UserPresenceType.InStudio => Strings.Menu_Overlay_Messages_InStudio,
                    UserPresenceType.Online => Strings.Menu_Overlay_Messages_Online,
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
            UserPresenceType status = presence?.UserPresenceType is UserPresenceType.InGame or UserPresenceType.InStudio or UserPresenceType.Online
                ? presence.UserPresenceType
                : UserPresenceType.Offline;

            Status = status;

            GameName = status == UserPresenceType.InGame && !String.IsNullOrWhiteSpace(presence?.LastLocation) ? presence!.LastLocation : null;
            PlaceId = presence?.PlaceId ?? 0;
            RootPlaceId = presence?.RootPlaceId ?? 0;
            ServerId = String.IsNullOrEmpty(presence?.GameId) ? null : presence!.GameId;

            foreach (string name in PresenceProperties)
                OnPropertyChanged(name);
        }
    }
}