// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

using Froststrap.Clients.Model;

namespace Froststrap.Clients.Providers.Cookies;

internal sealed class RosemaryCookieStore(Func<ClientInstall> install) : CookieStore(install)
{
    private const string AuthCookieName = ".ROBLOSECURITY";
    private const string CookieDomain = ".roblox.com";

    public override string? CookiesPath => Path.Combine(Install.CookiesDirectory!, "com.roblox.RobloxPlayer.cookies.tsv");

    public override async Task<string> ReadAuthCookieAsync()
    {
        foreach (string line in await File.ReadAllLinesAsync(CookiesPath!))
        {
            string[] fields = line.Split('\t');

            if (fields.Length >= 3 && fields[0] == AuthCookieName && IsRobloxDomain(fields[2]) && fields[1].Length > 0)
                return fields[1];
        }

        return string.Empty;
    }

    public override bool WriteAuthCookie(string plainCookie)
    {
        const UnixFileMode ownerOnly = UnixFileMode.UserRead | UnixFileMode.UserWrite;
        string filePath = CookiesPath!;
        string backupPath = filePath + ".bak";

        var lines = File.Exists(filePath)
            ? File.ReadAllLines(filePath).Where(line => !IsAuthCookieLine(line)).ToList()
            : [];

        long expiry = DateTimeOffset.UtcNow.AddDays(365).ToUnixTimeSeconds();
        lines.Add(string.Join('\t', AuthCookieName, plainCookie, CookieDomain, "/", "1", expiry.ToString(CultureInfo.InvariantCulture)));

        Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);

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
        {
            foreach (string line in lines)
                writer.Write(line + "\n");
        }

        File.SetUnixFileMode(filePath, ownerOnly);

        return true;
    }

    private static bool IsAuthCookieLine(string line)
    {
        string[] fields = line.Split('\t');
        return fields.Length >= 3 && fields[0] == AuthCookieName && IsRobloxDomain(fields[2]);
    }

    private static bool IsRobloxDomain(string domain)
    {
        string host = domain.TrimStart('.');
        return host.Equals("roblox.com", StringComparison.OrdinalIgnoreCase)
            || host.EndsWith(".roblox.com", StringComparison.OrdinalIgnoreCase);
    }
}
