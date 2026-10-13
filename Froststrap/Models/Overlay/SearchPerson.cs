// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Froststrap.Enums;
using Froststrap.UI.ViewModels;
using Froststrap.Utility;

namespace Froststrap.Models.Overlay
{
    internal class SearchPerson : NotifyPropertyChangedViewModel
    {
        public long UserId { get; init; }

        private string _displayName = String.Empty;
        public string DisplayName
        {
            get => _displayName;
            set => SetProperty(ref _displayName, value);
        }

        private string _username = String.Empty;
        public string Username
        {
            get => _username;
            set
            {
                if (SetProperty(ref _username, value))
                    OnPropertyChanged(nameof(Handle));
            }
        }

        public string Handle => String.IsNullOrEmpty(_username) ? String.Empty : $"@{_username}";

        private bool _isVerified;
        public bool IsVerified
        {
            get => _isVerified;
            set => SetProperty(ref _isVerified, value);
        }

        private string? _headshotUrl;
        public string? HeadshotUrl
        {
            get => _headshotUrl;
            set
            {
                if (SetProperty(ref _headshotUrl, value))
                    _ = LoadHeadshotAsync();
            }
        }

        private Bitmap? _headshotBitmap;
        public Bitmap? HeadshotBitmap
        {
            get => _headshotBitmap;
            private set => SetProperty(ref _headshotBitmap, value);
        }

        private bool _loadingHeadshot;
        private string? _loadedHeadshotUrl;

        private async Task LoadHeadshotAsync()
        {
            string? url = _headshotUrl;

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
                    if (_headshotUrl == url)
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

        private UserPresenceType _status = UserPresenceType.Offline;
        public UserPresenceType Status
        {
            get => _status;
            set
            {
                if (SetProperty(ref _status, value))
                {
                    OnPropertyChanged(nameof(IsInGame));
                    OnPropertyChanged(nameof(IsInStudio));
                    OnPropertyChanged(nameof(IsOnline));
                    OnPropertyChanged(nameof(IsOffline));
                    OnPropertyChanged(nameof(StatusText));
                }
            }
        }

        public bool IsInGame => _status == UserPresenceType.InGame;
        public bool IsInStudio => _status == UserPresenceType.InStudio;
        public bool IsOnline => _status == UserPresenceType.Online;
        public bool IsOffline => _status == UserPresenceType.Offline;

        private string? _gameName;
        public string? GameName
        {
            get => _gameName;
            set
            {
                if (SetProperty(ref _gameName, value))
                    OnPropertyChanged(nameof(StatusText));
            }
        }

        public long PlaceId { get; set; }
        public long RootPlaceId { get; set; }
        public string? ServerId { get; set; }

        private bool _isSelf;
        public bool IsSelf
        {
            get => _isSelf;
            set
            {
                if (SetProperty(ref _isSelf, value))
                    NotifyActions();
            }
        }

        private bool _isFriend;
        public bool IsFriend
        {
            get => _isFriend;
            set
            {
                if (SetProperty(ref _isFriend, value))
                    NotifyActions();
            }
        }

        private bool _isPending;
        public bool IsPending
        {
            get => _isPending;
            set
            {
                if (SetProperty(ref _isPending, value))
                {
                    OnPropertyChanged(nameof(ShowPendingBadge));
                    NotifyActions();
                }
            }
        }

        private bool _hasIncoming;
        public bool HasIncoming
        {
            get => _hasIncoming;
            set
            {
                if (SetProperty(ref _hasIncoming, value))
                {
                    OnPropertyChanged(nameof(ShowPendingBadge));
                    NotifyActions();
                }
            }
        }

        private bool _isWorking;
        public bool IsWorking
        {
            get => _isWorking;
            set
            {
                if (SetProperty(ref _isWorking, value))
                    NotifyActions();
            }
        }

        public bool CanJoin => IsInGame && PlaceId > 0 && !String.IsNullOrEmpty(ServerId);
        public bool CanViewGame => IsInGame && !CanJoin && (RootPlaceId > 0 || PlaceId > 0);
        public bool CanAddFriend => !_isSelf && !_isFriend && !_isPending && !_hasIncoming && !_isWorking;
        public bool CanAcceptFriend => !_isSelf && _hasIncoming && !_isFriend && !_isWorking;
        public bool CanUnfriend => !_isSelf && _isFriend && !_isWorking;
        public bool ShowPendingBadge => _isPending && !_isFriend && !_hasIncoming && !_isWorking;

        public string StatusText
        {
            get
            {
                if (IsSelf)
                    return Strings.Menu_Overlay_SearchBar_You;

                if (_isPending)
                    return Strings.Menu_Overlay_SearchBar_Pending;

                if (_hasIncoming)
                    return Strings.Menu_Overlay_SearchBar_RequestReceived;

                return _status switch
                {
                    UserPresenceType.InGame => String.IsNullOrEmpty(_gameName)
                        ? Strings.Menu_Overlay_Messages_InAGame
                        : String.Format(Locale.CurrentCulture, Strings.Menu_Overlay_Messages_Playing, _gameName),
                    UserPresenceType.InStudio => Strings.Menu_Overlay_Messages_InStudio,
                    UserPresenceType.Online => Strings.Menu_Overlay_Messages_Online,
                    _ => Strings.Menu_Overlay_Messages_Offline
                };
            }
        }

        private void NotifyActions()
        {
            OnPropertyChanged(nameof(CanJoin));
            OnPropertyChanged(nameof(CanViewGame));
            OnPropertyChanged(nameof(CanAddFriend));
            OnPropertyChanged(nameof(CanAcceptFriend));
            OnPropertyChanged(nameof(CanUnfriend));
            OnPropertyChanged(nameof(ShowPendingBadge));
            OnPropertyChanged(nameof(StatusText));
        }
    }
}