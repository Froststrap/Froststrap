using Avalonia.Media.Imaging;
using Froststrap.UI.ViewModels;
using Froststrap.Utility;
using System.Collections.ObjectModel;

namespace Froststrap.Models.Overlay
{
    internal enum ChatLineState
    {
        Sent,
        Pending,
        Failed
    }

    internal record ChatDayDivider(DateTime Day, string Text);

    internal record ChatSystemLine(string Text);

    internal class ChatMessageGroup : NotifyPropertyChangedViewModel
    {
        public long SenderId { get; init; }

        public string Sender { get; init; } = String.Empty;

        public string? Avatar { get; init; }

        private Bitmap? _avatarBitmap;
        public Bitmap? AvatarBitmap
        {
            get => _avatarBitmap;
            private set
            {
                if (_avatarBitmap == value)
                    return;

                _avatarBitmap = value;

                OnPropertyChanged(nameof(AvatarBitmap));
            }
        }

        public bool IsMine { get; init; }

        public DateTime? StartedAt { get; init; }

        public DateTime? LastAt { get; set; }

        public string TimeText => StartedAt?.ToLocalTime().ToString("t", Locale.CurrentCulture) ?? String.Empty;

        public ObservableCollection<ChatLine> Lines { get; } = [];

        private bool _loadingAvatar;

        public async Task LoadAvatarAsync()
        {
            if (_loadingAvatar || String.IsNullOrEmpty(Avatar))
                return;

            _loadingAvatar = true;

            try
            {
                Bitmap? bitmap = await ImageLoader.LoadAsync(Avatar, 68);

                if (bitmap is not null)
                    AvatarBitmap = bitmap;
            }
            finally
            {
                _loadingAvatar = false;
            }
        }
    }

    internal class ChatLine : NotifyPropertyChangedViewModel
    {
        public string Text { get; init; } = String.Empty;

        private ChatLineState _state = ChatLineState.Sent;
        public ChatLineState State
        {
            get => _state;
            set
            {
                if (_state == value)
                    return;

                _state = value;

                OnPropertyChanged(nameof(State));
                OnPropertyChanged(nameof(IsPending));
                OnPropertyChanged(nameof(IsFailed));
            }
        }

        public bool IsPending => _state == ChatLineState.Pending;

        public bool IsFailed => _state == ChatLineState.Failed;

        private string _failure = String.Empty;
        public string Failure
        {
            get => _failure;
            set
            {
                if (_failure == value)
                    return;

                _failure = value;

                OnPropertyChanged(nameof(Failure));
            }
        }
    }
}