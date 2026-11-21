// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

using System.Security.Cryptography;
using Froststrap.Clients.Contracts;
using Froststrap.Clients.Model;

namespace Froststrap.Clients.Providers.Cookies;

internal abstract class CookieStore(Func<ClientInstall> install) : ICookieStore
{
    protected ClientInstall Install => install();

    public abstract string? CookiesPath { get; }

    public abstract Task<string> ReadAuthCookieAsync();

    public abstract bool WriteAuthCookie(string plainCookie);

    protected static string GenerateGuestData()
    {
        using var rng = RandomNumberGenerator.Create();
        byte[] bytes = new byte[4];
        rng.GetBytes(bytes);
        int randomPositive = BitConverter.ToInt32(bytes, 0) & int.MaxValue;
        int userId = -(randomPositive % 999999999) - 1;
        return $"UserID={userId};";
    }

    protected static string GenerateTrackerData()
    {
        string createDate = DateTime.UtcNow.ToString("MM/dd/yyyy HH:mm:ss", CultureInfo.InvariantCulture);
        long browserId = GenerateRandomBrowserId();
        return $"CreateDate={createDate}&rbxid=&rbxuid=&browserid={browserId};";
    }

    protected static long GenerateRandomBrowserId()
    {
        using var rng = RandomNumberGenerator.Create();
        byte[] bytes = new byte[8];
        rng.GetBytes(bytes);
        long value = BitConverter.ToInt64(bytes, 0) & long.MaxValue;
        return value < 1_000_000_000_000_000L ? value + 1_000_000_000_000_000L : value;
    }

    protected static void BackupFile(string filePath)
    {
        if (File.Exists(filePath))
        {
            try { File.Copy(filePath, filePath + ".bak", overwrite: true); }
            catch { /* ignore */ }
        }
    }
}

internal sealed class NullCookieStore() : CookieStore(() => throw new InvalidOperationException())
{
    public override string? CookiesPath => null;

    public override Task<string> ReadAuthCookieAsync() => Task.FromResult(string.Empty);

    public override bool WriteAuthCookie(string plainCookie)
    {
        App.Logger.Info("This client has no cookie store.");
        return false;
    }
}

internal static class CookieStores
{
    public static ICookieStore Create(CookieFormat format, Func<ClientInstall> install) => format switch
    {
        CookieFormat.ProtectedNetscape when OperatingSystem.IsWindows() => new ProtectedCookieStore(install),
        CookieFormat.BinaryCookies => new BinaryCookieStore(install),
        CookieFormat.PlainHeader => new PlainCookieStore(install),
        _ => new NullCookieStore()
    };
}
