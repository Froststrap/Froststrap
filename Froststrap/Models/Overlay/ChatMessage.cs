using Avalonia;
using Avalonia.Media;
using System;
using System.ComponentModel;

namespace Froststrap.Models.Overlay
{
    internal enum ChatMessageState
    {
        Sent,
        Pending,
        Failed
    }

    internal class ChatMessage : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;

        public string Sender { get; set; } = String.Empty;

        public string Text { get; set; } = String.Empty;

        public bool IsCurrentUser { get; set; }

        public string MessageId { get; set; } = String.Empty;

        private ChatMessageState _state = ChatMessageState.Sent;

        public ChatMessageState State
        {
            get => _state;
            set
            {
                if (_state == value)
                    return;

                _state = value;

                OnPropertyChanged(nameof(State));
                OnPropertyChanged(nameof(Background));
                OnPropertyChanged(nameof(Opacity));
            }
        }

        public string Alignment => IsCurrentUser ? "Right" : "Left";

        public string SenderVisibility => IsCurrentUser ? "Collapsed" : "Visible";

        public Brush Background
        {
            get
            {
                if (State == ChatMessageState.Failed)
                    return Themed("SystemFillColorCriticalBackgroundBrush", Color.FromRgb(0x44, 0x26, 0x26));

                if (IsCurrentUser)
                    return Themed("AccentFillColorDefaultBrush", Color.FromRgb(0x4C, 0x8B, 0xF5));

                return Themed("CardBackgroundFillColorSecondaryBrush", Color.FromRgb(0x2D, 0x2D, 0x2D));
            }
        }

        public double Opacity => State == ChatMessageState.Pending ? 0.6 : 1;

        private static Brush Themed(string key, Color fallback)
        {
            if (Application.Current is { } app &&
                app.TryGetResource(key, null, out object? value) &&
                value is Brush brush)
            {
                return brush;
            }

            return new SolidColorBrush(fallback);
        }

        private void OnPropertyChanged(string name) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}