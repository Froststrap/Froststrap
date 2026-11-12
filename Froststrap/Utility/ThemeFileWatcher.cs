// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

namespace Froststrap.Utility
{
    internal static class ThemeFileWatcher
    {
        private const int DebounceMilliseconds = 300;

        private static readonly Lock Sync = new();

        private static FileSystemWatcher? _watcher;
        private static Timer? _debounce;
        private static WeakReference<Action>? _callback;
        private static string? _directory;

        public static void Watch(string directory, Action onChanged)
        {
            lock (Sync)
            {
                if (!string.Equals(_directory, directory, StringComparison.Ordinal) || _watcher == null)
                {
                    StopInternal();
                    Start(directory);
                }

                _callback = new WeakReference<Action>(onChanged);
            }
        }

        public static void Stop()
        {
            lock (Sync)
                StopInternal();
        }

        private static void Start(string directory)
        {
            _watcher = new FileSystemWatcher(directory, "Theme.xml")
            {
                NotifyFilter = NotifyFilters.LastWrite
                    | NotifyFilters.Size
                    | NotifyFilters.FileName
                    | NotifyFilters.CreationTime,
                IncludeSubdirectories = false,
                EnableRaisingEvents = true
            };

            _watcher.Changed += OnFileEvent;
            _watcher.Created += OnFileEvent;
            _watcher.Deleted += OnFileEvent;
            _watcher.Renamed += OnFileEvent;

            _directory = directory;
        }

        private static void OnFileEvent(object sender, FileSystemEventArgs e)
        {
            lock (Sync)
            {
                _debounce?.Dispose();
                _debounce = new Timer(_ => Invoke(), null, DebounceMilliseconds, Timeout.Infinite);
            }
        }

        private static void Invoke()
        {
            Action? callback = null;

            lock (Sync)
            {
                if (_callback?.TryGetTarget(out Action? target) == true)
                    callback = target;
            }

            if (callback == null)
                return;

            try
            {
                callback();
            }
            catch (Exception ex)
            {
                App.Logger.Error("Failed to reload the custom theme preview", ex);
            }
        }

        private static void StopInternal()
        {
            if (_watcher != null)
            {
                _watcher.EnableRaisingEvents = false;
                _watcher.Changed -= OnFileEvent;
                _watcher.Created -= OnFileEvent;
                _watcher.Deleted -= OnFileEvent;
                _watcher.Renamed -= OnFileEvent;
                _watcher.Dispose();
                _watcher = null;
            }

            _debounce?.Dispose();
            _debounce = null;
            _callback = null;
            _directory = null;
        }
    }
}
