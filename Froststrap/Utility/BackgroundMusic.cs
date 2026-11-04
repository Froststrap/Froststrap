// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

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
        private static string? _failedFolder;
        private static bool _unreadable;

        // failures in a row; only a track that plays to the end resets it
        private static int _failures;

        static BackgroundMusic()
        {
            AudioPlayer.TrackEnded += OnTrackEnded;
        }

        public static void Start()
        {
            _active = true;
            Retry();
        }

        public static void Stop()
        {
            _active = false;
            Refresh();
        }

        // turning music on is a retry; set here because a quick off/on can be coalesced into one Apply
        public static void Retry()
        {
            Interlocked.Increment(ref _rescanRequested);
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

            if (!_active || !settings.BackgroundMusicEnabled || string.IsNullOrWhiteSpace(folder))
            {
                _failedFolder = null;
                _failures = 0;
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

            if (advance)
            {
                if (endedFailed)
                    App.Logger.Warn("Background music track stopped with an error, skipping it");

                _failures = endedFailed ? _failures + 1 : 0;
            }

            if (_playing is not null && !advance)
            {
                AudioPlayer.SetVolume(volume);
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

            int start = advance || _index < 0 ? _index + 1 : _index;

            // stop once every track has failed in a row, whether it failed to start or broke off later
            for (int attempt = 0; _failures < _tracks.Count; attempt++)
            {
                int index = (start + attempt) % _tracks.Count;
                string path = _tracks[index];
                ulong id = Interlocked.Increment(ref _playId);

                AudioPlayerResult result = File.Exists(path)
                    ? AudioPlayer.PlayTrack(path, volume, id)
                    : AudioPlayerResult.FileNotReadable;

                if (result == AudioPlayerResult.Ok)
                {
                    _index = index;
                    _playing = path;
                    return;
                }

                App.Logger.Warn($"Could not play background music ({result}): {path}");
                _failures++;

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

            // a rescan is a retry, so earlier failures no longer count
            _failedFolder = null;
            _failures = 0;

            if (_folder != folder)
            {
                _folder = folder;
                _index = -1;
                StopPlayback();
                return;
            }

            // same folder rescanned: keep our place if the current track is still there
            _index = current is null ? -1 : _tracks.FindIndex(t => string.Equals(t, current, StringComparison.OrdinalIgnoreCase));
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

        private static void StopPlayback()
        {
            // invalidate the id first so the stopped track's end report is ignored
            Interlocked.Increment(ref _playId);

            if (_playing is null)
                return;

            AudioPlayer.Stop();
            _playing = null;
        }
    }
}
