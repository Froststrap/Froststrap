// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

using System.Windows.Input;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.Input;
using Froststrap.Backend;
using LucideAvalonia.Enum;

namespace Froststrap.UI.ViewModels.Settings
{
    /// The title bar music button: what's playing, playback controls, volume and shuffle.
    internal sealed class MusicControlViewModel : NotifyPropertyChangedViewModel, IDisposable
    {
        private const int CoverSize = 128;

        private string? _track;
        private int _loadVersion;
        private bool _disposed;

        private bool _isVisible;
        private bool _hasTrack;
        private bool _isPaused;
        private string _title = Strings.Menu_MusicControls_NothingPlaying;
        private string _subtitle = "";
        private Bitmap? _cover;

        public MusicControlViewModel()
        {
            BackgroundMusic.StateChanged += OnStateChanged;
            Update();
        }

        public ICommand PreviousCommand { get; } = new RelayCommand(BackgroundMusic.Previous);
        public ICommand PlayPauseCommand { get; } = new RelayCommand(BackgroundMusic.TogglePause);
        public ICommand NextCommand { get; } = new RelayCommand(BackgroundMusic.Next);

        /// Shown while music is on, so volume and shuffle stay reachable even when nothing plays.
        public bool IsVisible
        {
            get => _isVisible;
            private set => SetProperty(ref _isVisible, value);
        }

        /// The playback buttons only work while a song is loaded.
        public bool HasTrack
        {
            get => _hasTrack;
            private set => SetProperty(ref _hasTrack, value);
        }

        public bool IsPaused
        {
            get => _isPaused;
            private set
            {
                if (SetProperty(ref _isPaused, value))
                {
                    OnPropertyChanged(nameof(PlayPauseIcon));
                    OnPropertyChanged(nameof(PlayPauseTip));
                }
            }
        }

        public LucideIconNames PlayPauseIcon => IsPaused ? LucideIconNames.Play : LucideIconNames.Pause;
        public string PlayPauseTip => IsPaused ? Strings.Menu_MusicControls_Play : Strings.Menu_MusicControls_Pause;

        public string Title
        {
            get => _title;
            private set => SetProperty(ref _title, value);
        }

        /// Artist, or the album when there's no artist.
        public string Subtitle
        {
            get => _subtitle;
            private set
            {
                if (SetProperty(ref _subtitle, value))
                    OnPropertyChanged(nameof(HasSubtitle));
            }
        }

        public bool HasSubtitle => !string.IsNullOrEmpty(Subtitle);

        public Bitmap? Cover
        {
            get => _cover;
            private set
            {
                Bitmap? old = _cover;
                if (SetProperty(ref _cover, value))
                {
                    OnPropertyChanged(nameof(HasCover));
                    old?.Dispose();
                }
            }
        }

        public bool HasCover => Cover is not null;

        public double Volume
        {
            get => App.Settings.Prop.BackgroundMusicVolume;
            set
            {
                App.Settings.Prop.BackgroundMusicVolume = Math.Clamp(value, 0, 1);
                OnPropertyChanged();
                BackgroundMusic.Refresh();
            }
        }

        public bool Shuffle
        {
            get => App.Settings.Prop.BackgroundMusicShuffle;
            set
            {
                App.Settings.Prop.BackgroundMusicShuffle = value;
                OnPropertyChanged();
                BackgroundMusic.Refresh();
            }
        }

        // raised off the UI thread
        private void OnStateChanged() => Dispatcher.UIThread.Post(Update);

        private void Update()
        {
            if (_disposed)
                return;

            string? track = BackgroundMusic.CurrentTrack;
            IsVisible = BackgroundMusic.IsAvailable;
            HasTrack = track is not null;
            IsPaused = BackgroundMusic.IsPaused;

            if (track == _track)
                return;

            // any change, including to nothing, makes loads still in flight stale
            _track = track;
            int version = ++_loadVersion;
            Title = track is null ? Strings.Menu_MusicControls_NothingPlaying : Path.GetFileNameWithoutExtension(track);
            Subtitle = "";
            Cover = null;

            if (track is not null)
                _ = LoadInfoAsync(track, version);
        }

        // tags and cover are read off the UI thread; a newer track wins if they arrive late
        private async Task LoadInfoAsync(string track, int version)
        {
            var (info, cover) = await Task.Run(() =>
            {
                TrackInfo? info = AudioPlayer.ReadTrackInfo(track);
                return (info, DecodeCover(info?.Art ?? ReadOnlyMemory<byte>.Empty));
            });

            if (_disposed || version != _loadVersion)
            {
                cover?.Dispose();
                return;
            }

            if (!string.IsNullOrWhiteSpace(info?.Title))
                Title = info.Title;

            Subtitle = !string.IsNullOrWhiteSpace(info?.Artist) ? info.Artist : info?.Album ?? "";
            Cover = cover;
        }

        private static Bitmap? DecodeCover(ReadOnlyMemory<byte> art)
        {
            if (art.IsEmpty)
                return null;

            try
            {
                using var stream = new MemoryStream(art.ToArray());
                return Bitmap.DecodeToWidth(stream, CoverSize);
            }
            catch (Exception ex)
            {
                App.Logger.Warn($"Could not read album art: {ex.Message}");
                return null;
            }
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            BackgroundMusic.StateChanged -= OnStateChanged;
            Cover = null;
        }
    }
}
