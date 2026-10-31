// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

using Froststrap.Backend;

namespace Froststrap.Utility
{
    internal static class BackgroundMusic
    {
        private static readonly SemaphoreSlim Gate = new(1, 1);
        private static volatile bool _active;
        private static int _queued;
        private static string? _playing;

        public static void Start()
        {
            _active = true;
            Refresh();
        }

        public static void Stop()
        {
            _active = false;
            Refresh();
        }

        // opening the file and audio device can block, so only the latest state is applied off the UI thread
        public static void Refresh()
        {
            if (Interlocked.Exchange(ref _queued, 1) == 0)
                _ = Task.Run(ApplyAsync);
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
            string path = settings.BackgroundMusicPath;
            double volume = Math.Clamp(settings.BackgroundMusicVolume, 0, 1);

            if (!_active || !settings.BackgroundMusicEnabled || string.IsNullOrWhiteSpace(path))
            {
                StopPlayback();
                return;
            }

            if (_playing == path)
            {
                AudioPlayer.SetVolume(volume);
                return;
            }

            if (!File.Exists(path))
            {
                App.Logger.Warn($"Background music file not found: {path}");
                StopPlayback();
                return;
            }

            AudioPlayerResult result = AudioPlayer.Play(path, volume);

            if (result == AudioPlayerResult.Ok)
            {
                _playing = path;
            }
            else
            {
                App.Logger.Warn($"Could not play background music ({result}): {path}");
                StopPlayback();
            }
        }

        private static void StopPlayback()
        {
            if (_playing is null)
                return;

            AudioPlayer.Stop();
            _playing = null;
        }
    }
}
