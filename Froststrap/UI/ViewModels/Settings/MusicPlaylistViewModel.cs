// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

using Avalonia.Media.Imaging;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.Input;
using Froststrap.Backend;
using Froststrap.UI.ViewModels;
using Froststrap.Utility;
using System.Collections.ObjectModel;
using System.Runtime.InteropServices;

namespace Froststrap.UI.ViewModels.Settings
{
    /// The songs in the background music folder, like the custom theme list.
    internal sealed class MusicPlaylistViewModel : NotifyPropertyChangedViewModel, IDisposable
    {
        private const int ThumbnailSize = 64;
        private const int CoverSize = 192;

        private int _loadVersion;
        private int _coverVersion;
        private bool _disposed;
        private IReadOnlyList<string> _listed = [];
        private MusicPlaylistItem? _selected;
        private Bitmap? _selectedCover;

        public MusicPlaylistViewModel()
        {
            PlayCommand = new RelayCommand(Play, () => Selected is not null);

            BackgroundMusic.PlaylistChanged += OnPlaylistChanged;
            BackgroundMusic.StateChanged += OnStateChanged;
            Reload();
        }

        public ObservableCollection<MusicPlaylistItem> Tracks { get; } = [];

        public bool HasTracks => Tracks.Count > 0;

        public MusicPlaylistItem? Selected
        {
            get => _selected;
            set
            {
                if (!SetProperty(ref _selected, value))
                    return;

                OnPropertyChanged(nameof(HasSelection));
                PlayCommand.NotifyCanExecuteChanged();

                int version = ++_coverVersion;
                SelectedCover = null;

                if (value is not null)
                    _ = LoadSelectedCoverAsync(value, version);
            }
        }

        public bool HasSelection => Selected is not null;

        /// A larger copy of the selected song's cover, for the details panel.
        public Bitmap? SelectedCover
        {
            get => _selectedCover;
            private set
            {
                var old = _selectedCover;
                if (SetProperty(ref _selectedCover, value))
                {
                    old?.Dispose();
                    OnPropertyChanged(nameof(HasSelectedCover));
                }
            }
        }

        public bool HasSelectedCover => SelectedCover is not null;

        public RelayCommand PlayCommand { get; }

        private void Play()
        {
            if (Selected is { } item)
                BackgroundMusic.Play(item.Path);
        }

        public void PlaySelected()
        {
            if (PlayCommand.CanExecute(null))
                PlayCommand.Execute(null);
        }

        private void OnPlaylistChanged() => Dispatcher.UIThread.Post(Reload);

        private void OnStateChanged() => Dispatcher.UIThread.Post(UpdateNowPlaying);

        private void Reload()
        {
            if (_disposed)
                return;

            var playlist = BackgroundMusic.Playlist;
            if (ReferenceEquals(playlist, _listed))
            {
                UpdateNowPlaying();
                return;
            }

            _listed = playlist;
            string? keep = Selected?.Path;

            foreach (var item in Tracks)
                item.Dispose();
            Tracks.Clear();

            foreach (string path in playlist)
                Tracks.Add(new MusicPlaylistItem(path));

            OnPropertyChanged(nameof(HasTracks));
            UpdateNowPlaying();

            // keep the selection if the song is still there, else show what's playing
            Selected = Tracks.FirstOrDefault(t => string.Equals(t.Path, keep, StringComparison.OrdinalIgnoreCase))
                ?? Tracks.FirstOrDefault(t => t.IsNowPlaying)
                ?? Tracks.FirstOrDefault();

            _ = LoadInfoAsync([.. Tracks], ++_loadVersion);
        }

        private void UpdateNowPlaying()
        {
            if (_disposed)
                return;

            string? playing = BackgroundMusic.CurrentTrack;
            foreach (var item in Tracks)
                item.IsNowPlaying = string.Equals(item.Path, playing, StringComparison.OrdinalIgnoreCase);
        }

        // tags and thumbnails are read one song at a time off the UI thread; a reload makes this stale
        private async Task LoadInfoAsync(List<MusicPlaylistItem> items, int version)
        {
            foreach (var item in items)
            {
                if (_disposed || version != _loadVersion)
                    return;

                TrackInfo? info;
                Bitmap? thumbnail;

                // one bad file shouldn't stop the rest of the list from loading
                try
                {
                    (info, thumbnail) = await Task.Run(() =>
                    {
                        TrackInfo? info = AudioPlayer.ReadTrackInfo(item.Path);
                        return (info, DecodeCover(info?.Art ?? ReadOnlyMemory<byte>.Empty, ThumbnailSize));
                    });
                }
                catch (Exception ex)
                {
                    App.Logger.Warn($"Could not read track info for '{item.Path}': {ex.Message}");
                    (info, thumbnail) = (null, null);
                }

                if (_disposed || version != _loadVersion)
                {
                    thumbnail?.Dispose();
                    return;
                }

                item.Apply(info, thumbnail);
            }
        }

        private async Task LoadSelectedCoverAsync(MusicPlaylistItem item, int version)
        {
            // the list already found no cover, so don't read the file again
            if (item.InfoLoaded && !item.HasThumbnail)
                return;

            Bitmap? cover;
            try
            {
                cover = await Task.Run(() =>
                    DecodeCover(AudioPlayer.ReadTrackInfo(item.Path)?.Art ?? ReadOnlyMemory<byte>.Empty, CoverSize));
            }
            catch (Exception ex)
            {
                App.Logger.Warn($"Could not read album art for '{item.Path}': {ex.Message}");
                return;
            }

            if (_disposed || version != _coverVersion)
            {
                cover?.Dispose();
                return;
            }

            SelectedCover = cover;
        }

        private static Bitmap? DecodeCover(ReadOnlyMemory<byte> art, int width)
        {
            if (art.IsEmpty)
                return null;

            try
            {
                // the art is already a byte array, so read it in place instead of copying it
                using var stream = MemoryMarshal.TryGetArray(art, out ArraySegment<byte> bytes)
                    ? new MemoryStream(bytes.Array!, bytes.Offset, bytes.Count, writable: false)
                    : new MemoryStream(art.ToArray());
                return Bitmap.DecodeToWidth(stream, width);
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
            BackgroundMusic.PlaylistChanged -= OnPlaylistChanged;
            BackgroundMusic.StateChanged -= OnStateChanged;

            SelectedCover = null;
            foreach (var item in Tracks)
                item.Dispose();
            Tracks.Clear();
        }
    }

    /// One song in the list. Shows the file name until its tags have been read.
    internal sealed class MusicPlaylistItem(string path) : NotifyPropertyChangedViewModel, IDisposable
    {
        private string _title = System.IO.Path.GetFileNameWithoutExtension(path);
        private string _artist = "";
        private string _album = "";
        private string _duration = "";
        private Bitmap? _thumbnail;
        private bool _isNowPlaying;

        public string Path { get; } = path;

        public string Title
        {
            get => _title;
            private set => SetProperty(ref _title, value);
        }

        public string Artist
        {
            get => _artist;
            private set
            {
                if (SetProperty(ref _artist, value))
                    OnPropertyChanged(nameof(HasArtist));
            }
        }

        public bool HasArtist => Artist.Length > 0;

        public string Album
        {
            get => _album;
            private set
            {
                if (SetProperty(ref _album, value))
                    OnPropertyChanged(nameof(HasAlbum));
            }
        }

        public bool HasAlbum => Album.Length > 0;

        public string Duration
        {
            get => _duration;
            private set
            {
                if (SetProperty(ref _duration, value))
                    OnPropertyChanged(nameof(HasDuration));
            }
        }

        public bool HasDuration => Duration.Length > 0;

        public Bitmap? Thumbnail
        {
            get => _thumbnail;
            private set
            {
                if (SetProperty(ref _thumbnail, value))
                    OnPropertyChanged(nameof(HasThumbnail));
            }
        }

        public bool HasThumbnail => Thumbnail is not null;

        /// Set once the tags have been read, even if the file had none.
        public bool InfoLoaded { get; private set; }

        public bool IsNowPlaying
        {
            get => _isNowPlaying;
            set => SetProperty(ref _isNowPlaying, value);
        }

        public void Apply(TrackInfo? info, Bitmap? thumbnail)
        {
            if (!string.IsNullOrWhiteSpace(info?.Title))
                Title = info.Title;

            Artist = info?.Artist?.Trim() ?? "";
            Album = info?.Album?.Trim() ?? "";
            Duration = info?.Duration is { } d ? FormatDuration(d) : "";

            var old = Thumbnail;
            Thumbnail = thumbnail;
            old?.Dispose();

            InfoLoaded = true;
        }

        private static string FormatDuration(TimeSpan d) =>
            d.TotalHours >= 1 ? d.ToString(@"h\:mm\:ss", CultureInfo.InvariantCulture) : d.ToString(@"m\:ss", CultureInfo.InvariantCulture);

        public void Dispose()
        {
            var old = Thumbnail;
            Thumbnail = null;
            old?.Dispose();
        }
    }
}
