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
        private static readonly SemaphoreSlim Gate = new(1, 1);
        private static volatile bool _active;
        private static int _queued;
        private static string? _playing;
        private static string? _failed;

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
                _failed = null;
                StopPlayback();
                return;
            }

            if (_playing == path)
            {
                AudioPlayer.SetVolume(volume);
                return;
            }

            if (_failed == path)
                return;

            AudioPlayerResult result = File.Exists(path)
                ? AudioPlayer.Play(path, volume)
                : AudioPlayerResult.FileNotReadable;

            if (result == AudioPlayerResult.Ok)
            {
                _playing = path;
                _failed = null;
                return;
            }

            App.Logger.Warn($"Could not play background music ({result}): {path}");
            StopPlayback();
            _failed = path;

            string message = result is AudioPlayerResult.NoOutputDevice or AudioPlayerResult.LibraryUnavailable
                ? Strings.Menu_Appearance_BackgroundMusic_Unavailable
                : Strings.Menu_Appearance_BackgroundMusic_PlayFailed;

            MainWindow.ShowGlobalNotification(
                Strings.Menu_Appearance_BackgroundMusic_Title,
                message,
                FAInfoBarSeverity.Error,
                5000);
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
