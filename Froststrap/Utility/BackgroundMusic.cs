// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

using System.Security.Cryptography;
using FluentAvalonia.UI.Controls;
using Froststrap.Backend;
using Froststrap.UI.Elements.Settings;

namespace Froststrap.Utility
{
    internal static class BackgroundMusic
    {
        private static readonly string[] Extensions = [".mp3", ".ogg", ".flac", ".wav"];

        private static readonly SemaphoreSlim Gate = new(1, 1);
        private static volatile bool _active;

        // a counter, so a request made while a scan is running isn't lost
        private static int _rescanRequested;
        private static int _rescanDone;
        private static int _queued;
        private static ulong _playId;

        // last track end report from the audio thread; 0 = none
        private static readonly Lock EndLock = new();
        private static ulong _endedId;
        private static bool _endedFailed;

        private static string? _folder;
        private static List<string> _tracks = [];
        private static int _index = -1;
        private static string? _playing;
        private static volatile string? _lastStarted;
        private static string? _failedFolder;
        private static bool _unreadable;
        private static bool _shuffled;

        // requests from the music controls, applied on the next Apply
        private static int _skip;
        private static int _pauseToggles;
        private static bool _paused;

        // what the controls show; updated after each Apply
        private static volatile string? _nowPlaying;
        private static volatile bool _nowPaused;

        /// Raised off the UI thread when the current track or the pause state changes.
        public static event Action? StateChanged;

        public static string? CurrentTrack => _nowPlaying;
        public static bool IsPaused => _nowPaused;

        // failures in a row; only a track that plays to the end resets it
        private static int _failures;
        private static readonly HashSet<string> RecentFailures = new(StringComparer.OrdinalIgnoreCase);

        static BackgroundMusic()
        {
            AudioPlayer.TrackEnded += OnTrackEnded;
        }

        // Settings can close and reopen right away in the same process; keep playing through that
        private static readonly TimeSpan StopDelay = TimeSpan.FromSeconds(2);
        private static readonly Lock WindowLock = new();
        private static int _windowGeneration;

        public static void Start()
        {
            lock (WindowLock)
            {
                _windowGeneration++;
                _active = true;
            }

            Retry();
        }

        public static void Stop()
        {
            SaveLastTrack();

            int generation;
            lock (WindowLock)
                generation = ++_windowGeneration;

            _ = StopLaterAsync(generation);
        }

        private static async Task StopLaterAsync(int generation)
        {
            await Task.Delay(StopDelay);

            lock (WindowLock)
            {
                // reopened in the meantime
                if (_windowGeneration != generation)
                    return;

                _active = false;
            }

            Refresh();
        }

        // turning music on is a retry; set here because a quick off/on can be coalesced into one Apply
        public static void Retry()
        {
            Interlocked.Increment(ref _rescanRequested);
            Refresh();
        }

        public static void Next()
        {
            Interlocked.Exchange(ref _skip, 1);
            Refresh();
        }

        public static void Previous()
        {
            Interlocked.Exchange(ref _skip, -1);
            Refresh();
        }

        public static void TogglePause()
        {
            Interlocked.Increment(ref _pauseToggles);
            Refresh();
        }

        // opening the file and audio device can block, so only the latest state is applied off the UI thread
        public static void Refresh()
        {
            if (Interlocked.Exchange(ref _queued, 1) == 0)
                _ = Task.Run(ApplyAsync);
        }

        /// Supported files directly inside `folder`, sorted by name. Null if the folder can't be read.
        public static List<string>? FindTracks(string folder)
        {
            try
            {
                return Directory.EnumerateFiles(folder)
                    .Where(f => Extensions.Contains(Path.GetExtension(f), StringComparer.OrdinalIgnoreCase))
                    .Order(StringComparer.OrdinalIgnoreCase)
                    .ToList();
            }
            catch (Exception ex)
            {
                App.Logger.Warn($"Could not read background music folder '{folder}': {ex.Message}");
                return null;
            }
        }

        // runs on the audio thread
        private static void OnTrackEnded(ulong id, bool failed)
        {
            lock (EndLock)
            {
                // a late report from a stopped track must not overwrite the current track's
                if (id != Interlocked.Read(ref _playId))
                    return;

                _endedId = id;
                _endedFailed = failed;
            }

            Refresh();
        }

        private static async Task ApplyAsync()
        {
            await Gate.WaitAsync();

            try
            {
                Interlocked.Exchange(ref _queued, 0);
                Apply();
            }
            catch (Exception ex)
            {
                App.Logger.Error($"Background music failed: {ex}");
                StopPlayback();
            }
            finally
            {
                Gate.Release();
            }

            if (_nowPlaying != _playing || _nowPaused != _paused)
            {
                _nowPlaying = _playing;
                _nowPaused = _paused;

                try
                {
                    StateChanged?.Invoke();
                }
                catch (Exception ex)
                {
                    App.Logger.Error($"Background music state handler failed: {ex}");
                }
            }
        }

        private static void Apply()
        {
            var settings = App.Settings.Prop;
            string folder = settings.BackgroundMusicFolder;
            double volume = Math.Clamp(settings.BackgroundMusicVolume, 0, 1);

            ulong endedId;
            bool endedFailed;
            lock (EndLock)
            {
                (endedId, endedFailed) = (_endedId, _endedFailed);
                _endedId = 0;
            }

            // stopping bumps _playId, so a late report from a stopped or replaced track never matches
            bool advance = endedId != 0 && endedId == Interlocked.Read(ref _playId);
            int skip = Interlocked.Exchange(ref _skip, 0);
            bool togglePause = Interlocked.Exchange(ref _pauseToggles, 0) % 2 == 1;

            if (!_active || !settings.BackgroundMusicEnabled || string.IsNullOrWhiteSpace(folder))
            {
                _failedFolder = null;
                ResetFailures();
                Interlocked.Increment(ref _rescanRequested);
                StopPlayback();
                return;
            }

            bool switched = _folder != folder;
            int requested = Volatile.Read(ref _rescanRequested);
            if (switched || requested != _rescanDone)
            {
                // read before scanning, so a request during the scan triggers another one
                _rescanDone = requested;
                LoadFolder(folder);
            }

            // a new folder starts fresh, so the old folder's track end doesn't count
            if (switched)
                advance = false;

            if (settings.BackgroundMusicShuffle != _shuffled)
                Arrange(settings.BackgroundMusicShuffle);

            if (advance)
            {
                if (endedFailed)
                    App.Logger.Warn("Background music track stopped with an error, skipping it");

                if (endedFailed)
                    AddFailure(_playing);
                else
                    ResetFailures();
            }

            // controls only act on a track that's playing
            bool skipping = skip != 0 && _playing is not null && !switched && !advance;
            if (togglePause && _playing is not null)
                _paused = !_paused;

            if (_playing is not null && !advance && !skipping)
            {
                AudioPlayer.SetVolume(volume);
                AudioPlayer.SetPaused(_paused);
                return;
            }

            // everything in this folder already failed; wait for a new folder, a toggle or a rescan
            if (_failedFolder == folder)
                return;

            if (_tracks.Count == 0)
            {
                Fail(folder, _unreadable
                    ? Strings.Menu_Appearance_BackgroundMusic_FolderUnreadable
                    : Strings.Menu_Appearance_BackgroundMusic_NoTracks);
                return;
            }

            // starting again resumes the same track, or moves on if the user doesn't want that
            int next = advance || _index < 0 || !settings.BackgroundMusicResume ? _index + 1 : _index;

            if (skipping)
                next = _index < 0 ? 0 : skip > 0 ? _index + 1 : (_index - 1 + _tracks.Count) % _tracks.Count;

            // stop once every track has failed in a row, whether it failed to start or broke off later
            while (_failures < _tracks.Count)
            {
                // past the end of the order, even while skipping failures: start a new round
                if (next >= _tracks.Count)
                {
                    if (_shuffled)
                        Reshuffle();

                    next = 0;
                }

                int index = next++;
                string path = _tracks[index];
                ulong id = Interlocked.Increment(ref _playId);

                AudioPlayerResult result = File.Exists(path)
                    ? AudioPlayer.PlayTrack(path, volume, id)
                    : AudioPlayerResult.FileNotReadable;

                if (result == AudioPlayerResult.Ok)
                {
                    _index = index;
                    _playing = path;
                    _lastStarted = path;
                    _paused = false;
                    return;
                }

                App.Logger.Warn($"Could not play background music ({result}): {path}");
                AddFailure(path);

                if (result is AudioPlayerResult.NoOutputDevice or AudioPlayerResult.LibraryUnavailable)
                {
                    Fail(folder, Strings.Menu_Appearance_BackgroundMusic_Unavailable);
                    return;
                }
            }

            Fail(folder, Strings.Menu_Appearance_BackgroundMusic_PlayFailed);
        }

        private static void LoadFolder(string folder)
        {
            string? current = _index >= 0 && _index < _tracks.Count ? _tracks[_index] : null;

            List<string>? found = FindTracks(folder);
            _unreadable = found is null;
            _tracks = found ?? [];
            _shuffled = false;

            // a rescan is a retry, so earlier failures no longer count
            _failedFolder = null;
            ResetFailures();

            if (_folder != folder)
            {
                // first load since the app started: pick up from the song saved last session
                string? saved = _folder is null ? App.State.Prop.BackgroundMusicLastTrack : null;

                _folder = folder;
                StopPlayback();
                _index = saved is null ? -1 : _tracks.FindIndex(t => string.Equals(t, saved, StringComparison.OrdinalIgnoreCase));
                return;
            }

            // same folder rescanned: keep our place if the current track is still there
            _index = current is null ? -1 : _tracks.FindIndex(t => string.Equals(t, current, StringComparison.OrdinalIgnoreCase));
        }

        // sorted, or shuffled with the current track moved to the front so it keeps playing
        private static void Arrange(bool shuffle)
        {
            string? current = _index >= 0 && _index < _tracks.Count ? _tracks[_index] : null;

            if (shuffle)
                _tracks = Shuffled(_tracks);
            else
                _tracks.Sort(StringComparer.OrdinalIgnoreCase);

            _shuffled = shuffle;
            _index = current is null ? -1 : _tracks.FindIndex(t => string.Equals(t, current, StringComparison.OrdinalIgnoreCase));

            if (shuffle && _index > 0)
            {
                (_tracks[0], _tracks[_index]) = (_tracks[_index], _tracks[0]);
                _index = 0;
            }
        }

        // next round in a new order: tracks that just failed go last, so the others get tried first,
        // and the track that just played doesn't come first
        private static void Reshuffle()
        {
            List<string> order = Shuffled(_tracks);
            List<string> untried = [.. order.Where(t => !RecentFailures.Contains(t))];
            _tracks = [.. untried, .. order.Where(RecentFailures.Contains)];

            // only swap with another untried track; repeating a song beats retrying a failed one
            if (untried.Count > 1 && string.Equals(_tracks[0], _playing, StringComparison.OrdinalIgnoreCase))
                (_tracks[0], _tracks[1]) = (_tracks[1], _tracks[0]);

            _index = -1;
        }

        private static void AddFailure(string? path)
        {
            _failures++;
            if (path is not null)
                RecentFailures.Add(path);
        }

        private static void ResetFailures()
        {
            _failures = 0;
            RecentFailures.Clear();
        }

        private static List<string> Shuffled(List<string> tracks)
        {
            string[] order = [.. tracks];
            RandomNumberGenerator.Shuffle<string>(order);
            return [.. order];
        }

        private static void Fail(string folder, string message)
        {
            StopPlayback();
            _failedFolder = folder;

            MainWindow.ShowGlobalNotification(
                Strings.Menu_Appearance_BackgroundMusic_Title,
                message,
                FAInfoBarSeverity.Error,
                5000);
        }

        // runs on the UI thread when Settings closes, so it doesn't race other State.json saves
        private static void SaveLastTrack()
        {
            string? last = _lastStarted;
            if (last is null || last == App.State.Prop.BackgroundMusicLastTrack)
                return;

            App.State.Prop.BackgroundMusicLastTrack = last;
            App.State.SaveSetting(nameof(State.BackgroundMusicLastTrack));
        }

        private static void StopPlayback()
        {
            // invalidate the id first so the stopped track's end report is ignored
            Interlocked.Increment(ref _playId);

            if (_playing is null)
                return;

            AudioPlayer.Stop();
            _playing = null;
            _paused = false;
        }
    }
}
