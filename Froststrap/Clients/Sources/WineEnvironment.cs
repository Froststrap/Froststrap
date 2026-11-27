// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

using System.Formats.Tar;
using System.IO.Compression;
using System.Net.Security;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Froststrap.Clients.Contracts;
using Froststrap.Clients.Launchers;

namespace Froststrap.Clients.Sources;

internal sealed class WineEnvironment : IRuntimeEnvironment
{
    public Task PrepareAsync(string versionDirectory, string clientSettingsRoot, IInstallReporter reporter, CancellationToken cancellationToken) =>
        new WineSetup(reporter, versionDirectory, clientSettingsRoot, cancellationToken).RunAsync();
}

internal sealed class WineSetup(IInstallReporter reporter, string versionDirectory, string clientSettingsRoot, CancellationToken cancellationToken)
{
    private static readonly JsonSerializerOptions _indentedJsonOptions = new() { WriteIndented = true };
    private static readonly string[] DxvkDlls = ["d3d9.dll", "d3d10core.dll", "d3d11.dll", "dxgi.dll"];

    private const string WebView2MicrosoftRootPem = """
        -----BEGIN CERTIFICATE-----
        MIIF7TCCA9WgAwIBAgIQP4vItfyfspZDtWnWbELhRDANBgkqhkiG9w0BAQsFADCB
        iDELMAkGA1UEBhMCVVMxEzARBgNVBAgTCldhc2hpbmd0b24xEDAOBgNVBAcTB1Jl
        ZG1vbmQxHjAcBgNVBAoTFU1pY3Jvc29mdCBDb3Jwb3JhdGlvbjEyMDAGA1UEAxMp
        TWljcm9zb2Z0IFJvb3QgQ2VydGlmaWNhdGUgQXV0aG9yaXR5IDIwMTEwHhcNMTEw
        MzIyMjIwNTI4WhcNMzYwMzIyMjIxMzA0WjCBiDELMAkGA1UEBhMCVVMxEzARBgNV
        BAgTCldhc2hpbmd0b24xEDAOBgNVBAcTB1JlZG1vbmQxHjAcBgNVBAoTFU1pY3Jv
        c29mdCBDb3Jwb3JhdGlvbjEyMDAGA1UEAxMpTWljcm9zb2Z0IFJvb3QgQ2VydGlm
        aWNhdGUgQXV0aG9yaXR5IDIwMTEwggIiMA0GCSqGSIb3DQEBAQUAA4ICDwAwggIK
        AoICAQCygEGqNThNE3IyaCJNuLLx/9VSvGzH9dJKjDbu0cJcfoyKrq8TKG/Ac+M6
        ztAlqFo6be+ouFmrEyNozQwph9FvgFyPRH9dkAFSWKxRxV8qh9zc2AodwQO5e7BW
        6KPeZGHCnvjzfLnsDbVU/ky2ZU+I8JxImQxCCwl8MVkXeQZ4KI2JOkwDJb5xalwL
        54RgpJki49KvhKSn+9GY7Qyp3pSJ4Q6g3MDOmT3qCFK7VnnkH4S6Hri0xElcTzFL
        h93dBWcmmYDgcRGjuKVB4qRTufcyKYMME782XgSzS0NHL2vikR7TmE/dQgfI6B0S
        /Jmpaz6SfsjWaTr8ZL22CZ3K/QwLopt3YEsDlKQwaRLWQi3BQUzK3Kr9j1uDRprZ
        /LHR47PJf0h6zSTwQY9cdNCssBAgBkm3xy0hyFfj0IbzA2j70M5xwYmZSmQBbP3s
        MJHPQTySx+W6hh1hhMdfgzlirrSSL0fzC/hV66AfWdC7dJse0Hbm8ukG1xDo+mTe
        acY1logC8Ea4PyeZb8txiSk190gWAjWP1Xl8TQLPX+uKg09FcYj5qQ1OcunCnAfP
        SRtOBA5jUYxe2ADBVSy2xuDCZU7JNDn1nLPEfuhhbhNfFcRf2X7tHc7uROzLLoax
        7Dj2cO2rXBPB2Q8Nx4CyVe0096yb5MPa50c8prWPMd/FS6/r8QIDAQABo1EwTzAL
        BgNVHQ8EBAMCAYYwDwYDVR0TAQH/BAUwAwEB/zAdBgNVHQ4EFgQUci06AjGQQ7kU
        BU7h6qfHMdEjiTQwEAYJKwYBBAGCNxUBBAMCAQAwDQYJKoZIhvcNAQELBQADggIB
        AH9yzw+3xRXbm8BJyiZb/p4T5tPw0tuXX/JLP02zrhmu7deXoKzvqTqjwkGw5biR
        nhOBJAPmCf0/V0A5ISRW0RAvS0CpNoZLtFNXmvvxfomPEf4YbFGq6O0JlbXlccmh
        6Yd1phV/yX43VF50k8XDZ8wNT2uoFwxtCJJ+i92Bqi1wIcM9BhS7vyRep4TXPw8h
        Ir1LAAbblxzYXtTFC1yHblCk6MM4pPvLLMWSZpuFXst6bJN8gClYW1e1QGm6CHmm
        ZGIVnYeWRbVmIyADixxzoNOieTPgUFmG2y/lAiXqcyqfABTINseSO+lOAOzYVgm5
        M0kS0lQLAausR7aRKX1MtHWAUgHoyoL2n8ysnI8X6i8msKtyrAv+nlEex0NVZ09R
        s1fWtuzuUrc66U7h14GIvE+OdbtLqPA1qibUZ2dJsnBMO5PcHd94kIZysjik0dyS
        TclY6ysSXNQ7roxrsIPlAT/4CTL2kzU0Iq/dNw13CYArzUgA8YyZGUcFAenRv9FO
        0OYoQzeZpApKCNmacXPSqs0xE2N2oTdvkjgefRI8ZjLny23h/FKJ3crWZgWalmG+
        oijHHKOnNlA8OqTfSm7mhzvO6/DggTedEzxSjr25HTTGHdUKaj2YKXCMiSrRq4IQ
        SB/c9O+lxbtVGjhjhE63bK2VVOxlIhBJF7jAHscPrFRH
        -----END CERTIFICATE-----
        """;

    private readonly IInstallReporter _reporter = reporter;
    private readonly string _versionDirectory = versionDirectory;
    private readonly string _clientSettingsRoot = clientSettingsRoot;
    private readonly CancellationToken _cancellationToken = cancellationToken;

    public async Task RunAsync()
    {
        string baseWineDir = WineLauncher.WineRoot;
        string symlinkPath = Path.Combine(baseWineDir, "kombucha");
        string wineExe = WineLauncher.WineExecutable;

        if (!File.Exists(wineExe))
        {
            App.Logger.Info("Kombucha not found – downloading Kombucha.");

            var release = await GetLatestKombuchaReleaseAsync();
            _ = release ?? throw new InvalidOperationException("Could not fetch latest Kombucha release.");

            var asset = release.Assets?.FirstOrDefault(a => a.Name?.EndsWith(".tar.xz", StringComparison.Ordinal) == true);
            _ = asset ?? throw new InvalidOperationException("No .tar.xz asset found in Kombucha release.");

            _reporter.SetStatus(Strings.Bootstrapper_Status_DownloadingWine);

            string tempFile = Path.GetTempFileName();
            try
            {
                await _reporter.DownloadAsync(asset.BrowserDownloadUrl, tempFile);
                _reporter.SetIndeterminate(includeTaskbar: true);

                string versionTag = release.TagName.TrimStart('v');
                string extractDir = Path.Combine(baseWineDir, "kombucha_versions", $"kombucha-{versionTag}");
                if (Directory.Exists(extractDir))
                    Directory.Delete(extractDir, true);

                Directory.CreateDirectory(Path.GetDirectoryName(extractDir)!);

                _reporter.SetStatus(Strings.Bootstrapper_Status_ExtractingWine);
                await ExtractTarXzAsync(tempFile, Path.GetDirectoryName(extractDir)!, _cancellationToken);

                if (File.Exists(symlinkPath))
                    File.Delete(symlinkPath);
                if (Directory.Exists(symlinkPath))
                    Directory.Delete(symlinkPath, true);

                Directory.CreateSymbolicLink(symlinkPath, extractDir);
                App.Logger.Info($"Kombucha {versionTag} installed.");
            }
            finally
            {
                if (File.Exists(tempFile))
                    File.Delete(tempFile);
            }
        }
        else
        {
            App.Logger.Info("Wine already installed.");
        }

        var wineMgr = new WineManager(baseWineDir);

        _reporter.SetStatus(Strings.Bootstrapper_Status_InitializingWinePrefix);
        await wineMgr.EnsurePrefixAsync(_cancellationToken);

        await SetupDxvkAndRendererAsync();

        await SetupWebView2Async(wineMgr);
    }

    private static async Task ExtractTarXzAsync(string archivePath, string outputDir, CancellationToken cancellationToken)
    {
        var psi = new ProcessStartInfo("tar", $"-xJf \"{archivePath}\" -C \"{outputDir}\"")
        {
            UseShellExecute = false,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
        };
        using var process = Process.Start(psi);
        _ = process ?? throw new InvalidOperationException("Failed to start tar");
        await process.WaitForExitAsync(cancellationToken);
        if (process.ExitCode != 0)
        {
            var err = await process.StandardError.ReadToEndAsync(cancellationToken);
            throw new InvalidOperationException($"tar extraction failed: {err}");
        }
    }

    private async Task SetupDxvkAndRendererAsync()
    {
        var renderer = App.Settings.Prop.StudioRenderer;
        string targetRenderer = renderer switch
        {
            StudioRenderer.D3D11 => "D3D11",
            StudioRenderer.D3D11FL10 => "D3D11FL10",
            StudioRenderer.DXVK => "D3D11",
            StudioRenderer.Vulkan => "Vulkan",
            StudioRenderer.OpenGL => "OpenGL",
            _ => "D3D11"
        };

        SetRendererFastFlags(targetRenderer);

        if (renderer == StudioRenderer.DXVK)
        {
            string url = "https://github.com/doitsujin/dxvk/releases/download/v2.7.1/dxvk-2.7.1.tar.gz";
            await InstallDxvkAsync(url);
        }
        else
        {
            CleanupDxvkDlls();
        }

        ApplyRendererFlagsToVersionDirectory();
    }

    private static void SetRendererFastFlags(string prefer)
    {
        string[] renderers = ["D3D11", "D3D11FL10", "Vulkan", "OpenGL"];
        foreach (var r in renderers)
        {
            bool isPreferred = r == prefer;
            App.FastFlags.SetValue($"FFlagDebugGraphicsPrefer{r}", isPreferred ? "True" : "False");
            App.FastFlags.SetValue($"FFlagDebugGraphicsDisable{r}", isPreferred ? "False" : "True");
        }
    }

    private async Task InstallDxvkAsync(string url)
    {
        string cacheFile = Path.Combine(Paths.Downloads, $"dxvk-2.7.1.tar.gz");
        Directory.CreateDirectory(Paths.Downloads);

        bool needsInstall = !DxvkDlls.Any(dll => File.Exists(Path.Combine(_versionDirectory, dll)));

        if (needsInstall)
        {
            if (!File.Exists(cacheFile))
            {
                _reporter.SetStatus(Strings.Bootstrapper_Status_DownloadingDXVK);
                await _reporter.DownloadAsync(url, cacheFile);
                _reporter.SetIndeterminate();
            }

            _reporter.SetStatus(Strings.Bootstrapper_Status_ExtractingDXVK);
            await ExtractDxvkArchive(cacheFile, _versionDirectory);
        }
    }

    private static async Task ExtractDxvkArchive(string archivePath, string outputDir)
    {
        await Task.Run(() =>
        {
            using var fileStream = File.OpenRead(archivePath);
            using var gzipStream = new GZipStream(fileStream, CompressionMode.Decompress);
            using var tarReader = new TarReader(gzipStream);

            System.Formats.Tar.TarEntry? entry;
            while ((entry = tarReader.GetNextEntry()) != null)
            {
                if (entry.EntryType is TarEntryType.Directory)
                    continue;

                string entryName = entry.Name.Replace('\\', '/');
                if (entryName.Contains("/x64/", StringComparison.OrdinalIgnoreCase) && entryName.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
                {
                    string fileName = Path.GetFileName(entryName);
                    if (DxvkDlls.Contains(fileName))
                    {
                        string targetPath = Path.Combine(outputDir, fileName);
                        using var fs = File.Create(targetPath);
                        entry.DataStream?.CopyTo(fs);
                    }
                }
            }
        });
    }

    private void CleanupDxvkDlls()
    {
        foreach (var dll in DxvkDlls)
        {
            string path = Path.Combine(_versionDirectory, dll);
            if (File.Exists(path))
            {
                try { File.Delete(path); }
                catch (Exception ex) { App.Logger.Error($"Failed to delete {dll} in CleanupDxvkDlls: {ex.Message}"); }
            }
        }
    }

    private void ApplyRendererFlagsToVersionDirectory()
    {
        if (string.IsNullOrEmpty(_versionDirectory))
            return;

        string clientSettingsRoot = _clientSettingsRoot;

        string clientSettingsDir = Path.Combine(clientSettingsRoot, "ClientSettings");
        Directory.CreateDirectory(clientSettingsDir);
        string destPath = Path.Combine(clientSettingsDir, "ClientAppSettings.json");

        Dictionary<string, object> existing = [];
        if (File.Exists(destPath))
        {
            try
            {
                string existingJson = File.ReadAllText(destPath);
                existing = JsonHelper.Deserialize<Dictionary<string, object>>(existingJson) ?? [];
            }
            catch { }
        }

        var rendererFlags = App.FastFlags.Prop
            .Where(kv => kv.Key.StartsWith("FFlagDebugGraphicsPrefer", StringComparison.Ordinal) || kv.Key.StartsWith("FFlagDebugGraphicsDisable", StringComparison.Ordinal))
            .ToDictionary(kv => kv.Key, kv => kv.Value);

        foreach (var kv in rendererFlags)
            existing[kv.Key] = kv.Value;

        string mergedJson = JsonHelper.Serialize(existing, _indentedJsonOptions);
        File.WriteAllText(destPath, mergedJson);
    }

    private async Task SetupWebView2Async(WineManager wineMgr)
    {
        string? installedVersion = await wineMgr.QueryRegistryValueAsync(@"HKLM\Software\Wow6432Node\Microsoft\Windows\CurrentVersion\Uninstall\Microsoft EdgeWebView", "DisplayVersion", _cancellationToken);
        bool isInstalled = !string.IsNullOrEmpty(installedVersion);

        if (App.Settings.Prop.EnableWebView2 == isInstalled)
        {
            App.Logger.Info(App.Settings.Prop.EnableWebView2
                ? "WebView2 already installed, skipping."
                : "WebView2 already not installed, skipping.");
            return;
        }

        if (App.Settings.Prop.EnableWebView2)
        {
            App.Logger.Info("Downloading WebView2 Runtime via Wine...");
            _reporter.SetStatus(Strings.Bootstrapper_Status_DownloadingWebView2);

            try
            {
                if (!await wineMgr.RegistryKeyExistsAsync(@"HKCU\Software\Wine\AppDefaults\msedgewebview2.exe", _cancellationToken))
                    await wineMgr.AddRegistryValueAsync(@"HKCU\Software\Wine\AppDefaults\msedgewebview2.exe", "Version", "win7", cancellationToken: _cancellationToken);
            }
            catch (Exception ex)
            {
                App.Logger.Error($"Failed to set WebView2 AppDefaults override: {ex.Message}");
            }

            string? version = await GetWebView2LatestVersionAsync();
            if (version is null)
            {
                App.Logger.Warn("Could not resolve the latest WebView2 Runtime version, skipping install.");
                return;
            }

            var download = await GetWebView2RuntimeDownloadAsync(version);
            if (download is null)
            {
                App.Logger.Warn("Could not resolve a WebView2 Runtime download, skipping install.");
                return;
            }

            // private 0700 folder, not a predictable name in the shared temp dir
            string tempDir = Directory.CreateTempSubdirectory("froststrap-webview2-").FullName;
            string tempFile = Path.Combine(tempDir, "MicrosoftEdgeWebView2Setup.exe");
            try
            {
                await _reporter.DownloadAsync(download.Value.Url, tempFile);
                _reporter.SetIndeterminate();

                if (!await MatchesSha256Async(tempFile, download.Value.Sha256))
                {
                    App.Logger.Error("WebView2 installer doesn't match the hash from the update API, not running it.");
                    return;
                }

                _reporter.SetStatus(Strings.Bootstrapper_Status_InstallingWebView2);

                int exitCode = await wineMgr.RunAsync(tempFile,
                    ["--msedgewebview", "--do-not-launch-msedge", "--system-level"],
                    cancellationToken: _cancellationToken);

                App.Logger.Info(exitCode == 0
                    ? $"WebView2 Runtime {version} installed successfully."
                    : $"WebView2 installer exited with code {exitCode}.");
            }
            catch (Exception ex)
            {
                App.Logger.Error($"Failed to install WebView2: {ex.Message}");
            }
            finally
            {
                try
                {
                    Directory.Delete(tempDir, true);
                }
                catch (Exception ex)
                {
                    App.Logger.Warn($"Couldn't remove WebView2 temp folder: {ex.Message}");
                }
            }
        }
        else
        {
            App.Logger.Info("Uninstalling WebView2 Runtime via Wine...");
            _reporter.SetStatus(Strings.Bootstrapper_Status_UninstallingWebView2);

            string uninstallerPath = Path.Combine(wineMgr.PrefixDir, "drive_c", "Program Files (x86)",
                "Microsoft", "EdgeWebView", "Application", installedVersion!, "Installer", "setup.exe");

            if (!File.Exists(uninstallerPath))
            {
                App.Logger.Warn($"WebView2 uninstaller not found at {uninstallerPath}, skipping uninstall.");
                return;
            }

            try
            {
                int exitCode = await wineMgr.RunAsync(uninstallerPath,
                    ["--msedgewebview", "--uninstall", "--system-level", "--force-uninstall"],
                    cancellationToken: _cancellationToken);

                string? stillInstalled = await wineMgr.QueryRegistryValueAsync(@"HKLM\Software\Wow6432Node\Microsoft\Windows\CurrentVersion\Uninstall\Microsoft EdgeWebView", "DisplayVersion", _cancellationToken);
                App.Logger.Info(string.IsNullOrEmpty(stillInstalled)
                    ? "WebView2 Runtime uninstalled successfully."
                    : $"WebView2 uninstaller exited with code {exitCode}, but WebView2 still appears installed.");
            }
            catch (Exception ex)
            {
                App.Logger.Error($"Failed to uninstall WebView2: {ex.Message}");
            }
        }
    }
    private static readonly Lazy<HttpClient> _webView2HttpClient = new(CreateWebView2HttpClient);
    private static readonly X509Certificate2 _webView2RootCert = X509Certificate2.CreateFromPem(WebView2MicrosoftRootPem);
    private static HttpClient CreateWebView2HttpClient()
    {
        HttpClientHandler? handler = new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback = (_, cert, chain, sslPolicyErrors) =>
            {
                if (sslPolicyErrors == SslPolicyErrors.None)
                    return true;

                // the pinned root only fixes chain errors; a hostname mismatch always fails
                if (sslPolicyErrors != SslPolicyErrors.RemoteCertificateChainErrors || cert is null || chain is null)
                    return false;

                chain.ChainPolicy.TrustMode = X509ChainTrustMode.CustomRootTrust;
                chain.ChainPolicy.CustomTrustStore.Add(_webView2RootCert);
                chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;

                return chain.Build(cert);
            }
        };

        try
        {
            var client = new HttpClient(handler);
            handler = null;
            return client;
        }
        finally
        {
            handler?.Dispose();
        }
    }

    private async Task<bool> MatchesSha256Async(string path, string expectedBase64)
    {
        byte[] expected;
        try
        {
            expected = Convert.FromBase64String(expectedBase64);
        }
        catch (FormatException)
        {
            App.Logger.Warn($"WebView2 SHA-256 from the update API isn't valid base64: {expectedBase64}");
            return false;
        }

        await using var stream = File.OpenRead(path);
        byte[] actual = await SHA256.HashDataAsync(stream, _cancellationToken);
        return CryptographicOperations.FixedTimeEquals(actual, expected);
    }

    private async Task<string?> GetWebView2LatestVersionAsync()
    {
        try
        {
            string requestUrl = "https://msedge.api.cdp.microsoft.com/api/v1.1/contents/Browser/namespaces/Default/names/msedge-stable-win-x64/versions/latest?action=select";
            using var content = new StringContent(
                "{\"targetingAttributes\":{\"Updater\":\"MicrosoftEdgeUpdate\"}}",
                Encoding.UTF8, "application/json");

            using var response = await _webView2HttpClient.Value.PostAsync(new Uri(requestUrl), content, _cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                App.Logger.Warn($"Bad status: {(int)response.StatusCode} {response.StatusCode}");
                return null;
            }

            var data = await JsonHelper.ReadFromJsonAsync<WebView2LatestResponse>(response.Content, cancellationToken: _cancellationToken);
            return data?.ContentId?.Version;
        }
        catch (Exception ex)
        {
            App.Logger.Warn($"Failed to query latest version: {ex.Message}");
            return null;
        }
    }

    private async Task<(string Url, string Sha256)?> GetWebView2RuntimeDownloadAsync(string version)
    {
        try
        {
            string requestUrl = $"https://msedge.api.cdp.microsoft.com/api/v1.1/contents/Browser/namespaces/Default/names/msedge-stable-win-x64/versions/{version}/files?action=GenerateDownloadInfo";
            using var response = await _webView2HttpClient.Value.PostAsync(new Uri(requestUrl), null, _cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                App.Logger.Warn($"Bad status: {(int)response.StatusCode} {response.StatusCode}");
                return null;
            }

            var downloads = await JsonHelper.ReadFromJsonAsync<List<WebView2Download>>(response.Content, cancellationToken: _cancellationToken);
            if (downloads is null)
                return null;

            foreach (var d in downloads)
            {
                if (d.Url is null || d.FileId is null)
                    continue;

                string trimmed = d.FileId.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
                    ? d.FileId[..^4]
                    : d.FileId;

                if (trimmed.Split('_').Length != 3)
                    continue;

                if (string.IsNullOrWhiteSpace(d.Hashes?.Sha256))
                {
                    App.Logger.Warn("WebView2 Runtime entry has no SHA-256, skipping install.");
                    return null;
                }

                return (d.Url, d.Hashes.Sha256);
            }

            App.Logger.Warn("No standalone Runtime entry found among CDP download results.");
            return null;
        }
        catch (Exception ex)
        {
            App.Logger.Warn($"Failed to query downloads: {ex.Message}");
            return null;
        }
    }

    private async Task<GithubRelease?> GetLatestKombuchaReleaseAsync()
    {
        const string url = $"https://api.github.com/repos/vinegarhq/kombucha/releases/latest";
        return await JsonHelper.GetFromJsonAsync<GithubRelease>(App.HttpClient, url, _cancellationToken);
    }
}
