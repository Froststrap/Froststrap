namespace Froststrap;

public class WineManager(string baseWineDir)
{
    private readonly string _wineRoot = Path.Combine(baseWineDir, "kombucha");
    private readonly string _prefixDir = Path.Combine(baseWineDir, "prefixes", "studio");
    public string PrefixDir => _prefixDir;

    public async Task EnsurePrefixAsync(CancellationToken cancellationToken = default)
    {
        if (Directory.Exists(Path.Combine(_prefixDir, "drive_c", "windows")))
            return;

        Directory.CreateDirectory(_prefixDir);
        await RunAsync("wineboot", [ "-u" ], cancellationToken: cancellationToken);
    }

    public async Task<int> RunAsync(string exePath, string[] args, Dictionary<string, string>? env = null, CancellationToken cancellationToken = default)
    {
        var winePath = Path.Combine(_wineRoot, "bin", "wine");
        if (!File.Exists(winePath))
            throw new FileNotFoundException($"Wine executable not found at {winePath}");

        var psi = new ProcessStartInfo(winePath)
        {
            ArgumentList = { exePath },
            UseShellExecute = false,
            RedirectStandardOutput = false,
            RedirectStandardError = false,
        };

        foreach (var arg in args)
            psi.ArgumentList.Add(arg);

        psi.EnvironmentVariables["WINEPREFIX"] = _prefixDir;
        psi.EnvironmentVariables["WINEDEBUG"] = "-all";

        if (env != null)
            foreach (var kv in env)
                psi.EnvironmentVariables[kv.Key] = kv.Value;

        using var process = Process.Start(psi);
        _ = process ?? throw new Exception($"Failed to start Wine with {exePath}");
        await process.WaitForExitAsync(cancellationToken);
        return process.ExitCode;
    }

    public async Task AddRegistryValueAsync(string keyPath, string valueName, string data, string type = "REG_SZ", CancellationToken cancellationToken = default)
    {
        var args = new List<string> { "add", keyPath };
        if (!string.IsNullOrEmpty(valueName))
            args.AddRange([ "/v", valueName ]);
        else
            args.Add("/ve");
        args.AddRange([ "/t", type, "/d", data, "/f" ]);
        await RunAsync("reg", [.. args], cancellationToken: cancellationToken);
    }

    public async Task DeleteRegistryValueAsync(string keyPath, string valueName, CancellationToken cancellationToken = default)
    {
        var args = new List<string> { "delete", keyPath, "/f" };
        if (!string.IsNullOrEmpty(valueName))
            args.AddRange([ "/v", valueName ]);
        await RunAsync("reg", [.. args], cancellationToken: cancellationToken);
    }

    public async Task<string?> QueryRegistryValueAsync(string keyPath, string valueName, CancellationToken cancellationToken = default)
    {
        var (exitCode, output) = await RunWithOutputAsync("reg", [ "query", keyPath, "/v", valueName ], cancellationToken: cancellationToken);
        if (exitCode != 0) return null;

        var lines = output.Split('\n');
        foreach (var line in lines)
        {
            if (line.Contains(valueName) && line.Contains("REG_SZ"))
            {
                var parts = line.Split([ ' ', '\t' ], StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length >= 3)
                    return parts[2].Trim();
            }
        }
        return null;
    }

    public async Task<bool> RegistryKeyExistsAsync(string keyPath, CancellationToken cancellationToken = default)
    {
        var (exitCode, _) = await RunWithOutputAsync("reg", [ "query", keyPath ], cancellationToken: cancellationToken);
        return exitCode == 0;
    }

    public async Task<(int exitCode, string output)> RunWithOutputAsync(string exePath, string[] args, Dictionary<string, string>? env = null, CancellationToken cancellationToken = default)
    {
        var winePath = Path.Combine(_wineRoot, "bin", "wine");
        if (!File.Exists(winePath))
            throw new FileNotFoundException($"Wine executable not found at {winePath}");

        var psi = new ProcessStartInfo(winePath)
        {
            ArgumentList = { exePath },
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };
        foreach (var arg in args)
            psi.ArgumentList.Add(arg);

        psi.EnvironmentVariables["WINEPREFIX"] = _prefixDir;
        psi.EnvironmentVariables["WINEDEBUG"] = "-all";

        if (env != null)
            foreach (var kv in env)
                psi.EnvironmentVariables[kv.Key] = kv.Value;

        using var process = Process.Start(psi);
        _ = process ?? throw new Exception($"Failed to start Wine with {exePath}");
        var output = await process.StandardOutput.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken);
        return (process.ExitCode, output);
    }
}