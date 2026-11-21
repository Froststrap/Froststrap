// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

using Froststrap.Clients.Model;

namespace Froststrap.Clients.Providers.Cookies;

internal sealed class PlainCookieStore(Func<ClientInstall> install) : CookieStore(install)
{
    public override string? CookiesPath => Install.CookiesDirectory;

    public override Task<string> ReadAuthCookieAsync() => LoadLinuxCookies(CookiesPath!);

    public override bool WriteAuthCookie(string plainCookie) => WriteLinuxCookieFile(plainCookie, CookiesPath!);

    private static async Task<string> LoadLinuxCookies(string cookiesPath)
    {
        string cookieText = await File.ReadAllTextAsync(cookiesPath);
        if (string.IsNullOrWhiteSpace(cookieText))
            return string.Empty;

        var cookieParts = cookieText.Split(';', StringSplitOptions.RemoveEmptyEntries);
        foreach (var part in cookieParts)
        {
            var trimmed = part.Trim();
            int eqIndex = trimmed.IndexOf('=', StringComparison.Ordinal);
            if (eqIndex > 0)
            {
                string name = trimmed[..eqIndex].Trim();
                string value = trimmed[(eqIndex + 1)..].Trim();
                if (name == ".ROBLOSECURITY")
                    return value;
            }
        }
        return string.Empty;
    }

    private static bool WriteLinuxCookieFile(string plainCookie, string filePath)
    {
        string content = $".ROBLOSECURITY={plainCookie};";

        BackupFile(filePath);

        File.WriteAllText(filePath, content);
        File.SetUnixFileMode(filePath, UnixFileMode.UserRead | UnixFileMode.UserWrite);

        return true;
    }
}
