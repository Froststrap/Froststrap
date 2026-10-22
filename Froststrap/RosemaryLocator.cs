using System.Text.Json;

namespace Froststrap;

internal sealed class RosemaryLocator(HttpClient httpClient)
{
    private const string ReleaseUrl = "https://api.github.com/repos/microlift/rosemary/releases/latest";

    public static string ManagedPath => Path.Combine(Paths.Cache, "Rosemary", "rosemary.AppImage");

    public static string? FindConfigured()
    {
        string configured = App.Settings.Prop.RosemaryExecutablePath.Trim();
        if (IsExecutable(configured))
            return Path.GetFullPath(configured);

        string? path = Environment.GetEnvironmentVariable("PATH");
        if (!string.IsNullOrEmpty(path))
        {
            foreach (string directory in path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
            {
                string candidate = Path.Combine(directory, "rosemary");
                if (IsExecutable(candidate))
                    return Path.GetFullPath(candidate);
            }
        }

        return IsExecutable(ManagedPath) ? ManagedPath : null;
    }

    public async Task<string?> ResolveAsync(Func<Task<bool>> confirmDownload, Func<long, long, Task>? reportProgress = null)
    {
        string? executable = FindConfigured();
        if (executable is not null)
            return executable;

        if (!await confirmDownload())
            return null;

        try
        {
            using var response = await httpClient.GetAsync(ReleaseUrl, HttpCompletionOption.ResponseHeadersRead);
            response.EnsureSuccessStatusCode();
            using JsonDocument release = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());

            string? downloadUrl = release.RootElement.GetProperty("assets")
                .EnumerateArray()
                .Select(asset => asset.GetProperty("browser_download_url").GetString())
                .FirstOrDefault(url => url?.EndsWith(".AppImage", StringComparison.OrdinalIgnoreCase) == true);

            if (string.IsNullOrEmpty(downloadUrl))
                throw new InvalidOperationException(Strings.Rosemary_DownloadFailed);

            Directory.CreateDirectory(Path.GetDirectoryName(ManagedPath)!);
            using var download = await httpClient.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead);
            download.EnsureSuccessStatusCode();
            long total = download.Content.Headers.ContentLength ?? 0;
            await using var source = await download.Content.ReadAsStreamAsync();
            await using var target = new FileStream(ManagedPath, FileMode.Create, FileAccess.Write, FileShare.None);

            byte[] buffer = new byte[81920];
            long current = 0;
            int read;
            while ((read = await source.ReadAsync(buffer)) > 0)
            {
                await target.WriteAsync(buffer.AsMemory(0, read));
                current += read;
                if (reportProgress is not null)
                    await reportProgress(current, total);
            }

            File.SetUnixFileMode(ManagedPath, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute |
                UnixFileMode.GroupRead | UnixFileMode.GroupExecute | UnixFileMode.OtherRead | UnixFileMode.OtherExecute);

            return IsExecutable(ManagedPath) ? ManagedPath : null;
        }
        catch (Exception ex)
        {
            App.Logger.Error(ex, "Failed to download Rosemary.");
            return null;
        }
    }

    private static bool IsExecutable(string? path)
    {
        if (!OperatingSystem.IsLinux() || string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            return false;

        try
        {
            UnixFileMode mode = File.GetUnixFileMode(path);
            return (mode & (UnixFileMode.UserExecute | UnixFileMode.GroupExecute | UnixFileMode.OtherExecute)) != 0;
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }
}
