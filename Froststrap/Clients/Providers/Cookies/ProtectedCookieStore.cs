// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

using System.Runtime.Versioning;
using System.Security.Cryptography;
using Froststrap.Clients.Model;
using Froststrap.RobloxInterfaces;

namespace Froststrap.Clients.Providers.Cookies;

[SupportedOSPlatform("windows")]
internal sealed class ProtectedCookieStore(Func<ClientInstall> install) : CookieStore(install)
{
    private const string AuthCookieName = ".ROBLOSECURITY";
    private const string AuthPattern = $@"\t{AuthCookieName}\t(.+?)(;|$)";

    private static readonly JsonSerializerOptions _jsonOptions = new() { WriteIndented = true };

    public override string? CookiesPath => Path.Combine(
        Install.CookiesDirectory!,
        Deployment.IsDefaultRobloxDomain ? "RobloxCookies.dat" : $"{Deployment.RobloxDomain}_RobloxCookies.dat");

    public override Task<string> ReadAuthCookieAsync() => LoadWindowsCookies(CookiesPath!);

    public override bool WriteAuthCookie(string plainCookie) => WriteWindowsCookieFile(plainCookie, CookiesPath!);

    private static async Task<string> LoadWindowsCookies(string cookiesPath)
    {
        string content = await File.ReadAllTextAsync(cookiesPath);
        var cookies = JsonHelper.Deserialize<RobloxCookies>(content)!;

        byte[] encryptedData = Convert.FromBase64String(cookies.Cookies);
        byte[] unencryptedData = ProtectedData.Unprotect(encryptedData, null, DataProtectionScope.CurrentUser);

        string rawCookies = Encoding.UTF8.GetString(unencryptedData);
        Match authCookieMatch = Regex.Match(rawCookies, AuthPattern);

        return authCookieMatch.Success ? authCookieMatch.Groups[1].Value : string.Empty;
    }

    private static bool WriteWindowsCookieFile(string plainCookie, string filePath)
    {
        string guestData = GenerateGuestData();
        string trackerData = GenerateTrackerData();

        string fullNetscape =
            $"#HttpOnly_.roblox.com\tTRUE\t/\tFALSE\t0\tGuestData\t{guestData}\n" +
            $"#HttpOnly_.roblox.com\tTRUE\t/\tFALSE\t0\tRBXEventTrackerV2\t{trackerData}\n" +
            $"#HttpOnly_.roblox.com\tTRUE\t/\tTRUE\t1817995500\t.ROBLOSECURITY\t{plainCookie}";

        byte[] encrypted = ProtectedData.Protect(
            Encoding.UTF8.GetBytes(fullNetscape),
            null,
            DataProtectionScope.CurrentUser);

        var cookieData = new RobloxCookies
        {
            Version = "1",
            Cookies = Convert.ToBase64String(encrypted)
        };

        string json = JsonHelper.Serialize(cookieData, _jsonOptions);
        BackupFile(filePath);
        File.WriteAllText(filePath, json);
        return true;
    }
}
