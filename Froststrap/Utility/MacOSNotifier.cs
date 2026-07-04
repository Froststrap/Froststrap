namespace Froststrap.Utility;

public static class MacOSNotifier
{
    private static string? _notifierPath;
    private static string? _cachedIconPath;

    private static string? GetNotifierPath()
    {
        if (_notifierPath != null) return _notifierPath;

        string bundled = Path.Combine(AppContext.BaseDirectory, "Resources",
            "terminal-notifier.app", "Contents", "MacOS", "terminal-notifier");
        if (File.Exists(bundled))
        {
            App.Logger.WriteLine("MacOSNotifier", "Using .app");
            _notifierPath = bundled;
            return _notifierPath;
        }

        string? pathEnv = Environment.GetEnvironmentVariable("PATH");
        if (pathEnv != null)
        {
            foreach (string dir in pathEnv.Split(Path.PathSeparator))
            {
                string candidate = Path.Combine(dir, "terminal-notifier");
                if (File.Exists(candidate))
                {
                    App.Logger.WriteLine("MacOSNotifier", "Using homebrew");
                    _notifierPath = candidate;
                    return _notifierPath;
                }
            }
        }

        App.Logger.WriteLine("MacOSNotifier", "Failed");
        return null;
    }

    private static string? GetIconPath()
    {
        if (_cachedIconPath != null && File.Exists(_cachedIconPath))
            return _cachedIconPath;

        const string resourceName = "Froststrap.png";
        string tempPath = Path.Combine(Paths.Temp, "FroststrapNotification.png");

        if (!File.Exists(tempPath))
        {
            try
            {
                using var stream = Resource.GetStream(resourceName);
                if (stream == null)
                {
                    App.Logger.WriteLine("MacOSNotifier", $"Resource '{resourceName}' not found.");
                    return null;
                }

                Directory.CreateDirectory(Path.GetDirectoryName(tempPath)!);
                using var fileStream = File.Create(tempPath);
                stream.CopyTo(fileStream);
            }
            catch (Exception ex)
            {
                App.Logger.WriteException("MacOSNotifier", ex);
                return null;
            }
        }

        _cachedIconPath = tempPath;
        return _cachedIconPath;
    }

    public static bool ShowNotification(
        string title,
        string message,
        string? subtitle = null,
        string? appIconPath = null)
    {
        if (!OperatingSystem.IsMacOS())
            return false;

        string? notifier = GetNotifierPath();
        if (string.IsNullOrEmpty(notifier))
        {
            App.Logger.WriteLine("MacOSNotifier",
                "terminal-notifier not found. Please install via Homebrew: brew install terminal-notifier");
            return false;
        }

        if (string.IsNullOrEmpty(appIconPath))
        {
            appIconPath = GetIconPath();
        }

        var args = new List<string>
        {
            "-title", title,
            "-message", message
        };

        if (!string.IsNullOrEmpty(subtitle))
        {
            args.Add("-subtitle");
            args.Add(subtitle);
        }

        if (!string.IsNullOrEmpty(appIconPath) && File.Exists(appIconPath))
        {
            args.Add("-appIcon");
            args.Add(appIconPath);
        }

        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = notifier,
                Arguments = string.Join(" ", args.Select(a => $"\"{a}\"")),
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardError = true
            };

            using var process = new Process { StartInfo = startInfo };
            process.Start();
            process.WaitForExit(2000);

            if (process.ExitCode != 0)
            {
                string error = process.StandardError.ReadToEnd();
                App.Logger.WriteLine("MacOSNotifier", $"terminal-notifier exited with {process.ExitCode}: {error}");
                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            App.Logger.WriteException("MacOSNotifier", ex);
            return false;
        }
    }
}