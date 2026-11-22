// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

using Froststrap.Clients.Contracts;
using Froststrap.Clients.Model;

namespace Froststrap.Clients.Providers.Cookies;

internal sealed class PlainCookieStore(Func<ClientInstall> install) : CookieStore(install)
{
    public override string? CookiesPath => Install.CookiesDirectory;

    public override Task<StoredCookies> ReadAsync() => LoadLinuxCookies(CookiesPath!);

    public override bool WriteAuthCookie(string plainCookie) => WriteLinuxCookieFile(plainCookie, CookiesPath!);

    private static async Task<StoredCookies> LoadLinuxCookies(string cookiesPath)
    {
        string cookieText = await ReadTextShared(cookiesPath);
        if (string.IsNullOrWhiteSpace(cookieText))
            return new StoredCookies(string.Empty, string.Empty);

        string authCookie = string.Empty;
        string tracker = string.Empty;

        var cookieParts = cookieText.Split(';', StringSplitOptions.RemoveEmptyEntries);
        foreach (var part in cookieParts)
        {
            var trimmed = part.Trim();
            int eqIndex = trimmed.IndexOf('=', StringComparison.Ordinal);
            if (eqIndex > 0)
            {
                string name = trimmed[..eqIndex].Trim();
                string value = trimmed[(eqIndex + 1)..].Trim();

                if (name == AuthCookieName)
                    authCookie = value;
                else if (name == TrackerCookieName)
                    tracker = value;
            }
        }

        return new StoredCookies(authCookie, tracker);
    }

    private static bool WriteLinuxCookieFile(string plainCookie, string filePath)
    {
        const UnixFileMode ownerOnly = UnixFileMode.UserRead | UnixFileMode.UserWrite;
        string content = $".ROBLOSECURITY={plainCookie};";
        string backupPath = filePath + ".bak";

        if (File.Exists(filePath))
            File.SetUnixFileMode(filePath, ownerOnly);

        BackupFile(filePath);

        if (File.Exists(backupPath))
            File.SetUnixFileMode(backupPath, ownerOnly);

        using (var stream = new FileStream(filePath, new FileStreamOptions
        {
            Mode = FileMode.Create,
            Access = FileAccess.Write,
            UnixCreateMode = ownerOnly
        }))
        using (var writer = new StreamWriter(stream))
            writer.Write(content);

        File.SetUnixFileMode(filePath, ownerOnly);

        return true;
    }
}
