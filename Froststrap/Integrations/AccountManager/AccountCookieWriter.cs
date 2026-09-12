// SPDX-FileCopyrightText: 2026 Froststrap
// Copyright (C) Froststrap Team
//
// SPDX-License-Identifier: MPL-2.0

using Newtonsoft.Json;
using System.Globalization;
using System.Net;
using System.Security.Cryptography;
using System.Text;

namespace Froststrap.Integrations.AccountManager
{
    internal static class AccountCookieWriter
    {
        private static readonly JsonSerializerOptions _jsonOptions = new() { WriteIndented = true };

        public static bool WriteCookieFileForAccount(AccountManagerAccount account)
        {
            string plainCookie = account.SecurityToken;
            if (string.IsNullOrEmpty(plainCookie))
            {
                App.Logger.Info("Account has no valid cookie.");
                return false;
            }

            string filePath = CookiesManager.CookiesPath;

            try
            {
                if (OperatingSystem.IsWindows())
                    return WriteWindowsCookieFile(plainCookie, filePath);
                if (OperatingSystem.IsMacOS())
                    return WriteMacCookieFile(plainCookie, filePath);
                if (OperatingSystem.IsLinux())
                    return WriteLinuxCookieFile(plainCookie, filePath);

                App.Logger.Info("Unsupported OS.");
                return false;
            }
            catch (Exception ex)
            {
                App.Logger.Error(ex);
                return false;
            }
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

            string json = System.Text.Json.JsonSerializer.Serialize(cookieData, _jsonOptions);
            BackupFile(filePath);
            File.WriteAllText(filePath, json);
            return true;
        }

        private static bool WriteMacCookieFile(string plainCookie, string filePath)
        {
            var staticCookies = new[]
            {
                new BinaryCookie("rbx-ip2", "rbx-ip2"),
                new BinaryCookie("RBXPaymentsFlowContext", "98619ec1-af61-4739-a12b-c8fc4abbab79,"),
                new BinaryCookie("rbxas", "40ac424f7d0844b99d92179bdb9e22341d7e2c49cc7ae6c5101cca1c9a67b365"),
                new BinaryCookie("ARID", "fc r502pFLAl14bk47SYcQoDVdTFrOeqwK6AxopmW+t1Ke66bZfJX7J3KniMv+tn8NuVRLyJkFkO5z3XXzJm1ANOvLfHLkBv9q1aIoq/MT4rKYzZ6KXnZb+xnOMsu43OExsFnQhxZy2LCOzbYJMnuidqjZSIH6qQSU=**uKVnWDwQcFDl0aet")
            };

            var guestCookie = new BinaryCookie("GuestData", GenerateGuestData());
            var trackerCookie = new BinaryCookie("RBXEventTrackerV2", GenerateTrackerData());
            var securityCookie = new BinaryCookie(".ROBLOSECURITY", plainCookie, isSecure: true)
            {
                Expiry = MacTimeFromDateTime(DateTime.UtcNow.AddDays(365))
            };

            var allCookies = new List<BinaryCookie>(staticCookies) { guestCookie, trackerCookie, securityCookie };

            for (int i = 0; i < allCookies.Count; i++)
            {
                var c = allCookies[i];
                c.Domain = ".roblox.com";
                c.Path = "/";
                if (!c.Expiry.HasValue) c.Expiry = 0;
                if (!c.Creation.HasValue) c.Creation = MacTimeFromDateTime(DateTime.UtcNow);
                allCookies[i] = c;
            }

            byte[] binaryData = SerializeBinaryCookies(allCookies);
            BackupFile(filePath);
            File.WriteAllBytes(filePath, binaryData);
            return true;
        }

        private static bool WriteLinuxCookieFile(string plainCookie, string filePath)
        {
            string content = $".ROBLOSECURITY={plainCookie};";
            BackupFile(filePath);
            File.WriteAllText(filePath, content);
            return true;
        }

        private struct BinaryCookie(string name, string value, bool isSecure = false)
        {
            public string Name { get; set; } = name;
            public string Value { get; set; } = value;
            public string Domain { get; set; } = ".roblox.com";
            public string Path { get; set; } = "/";
            public int Flags { get; set; } = isSecure ? 1 : 0;
            public double? Expiry { get; set; }
            public double? Creation { get; set; }
        }

        private static byte[] SerializeBinaryCookies(List<BinaryCookie> cookies)
        {
            var records = cookies.Select(BuildCookieRecord).ToList();
            int numCookies = records.Count;
            int pageHeaderSize = 4 + 4 + numCookies * 4;
            int totalPageSize = pageHeaderSize + records.Sum(b => b.Length);

            using var ms = new MemoryStream();
            using var writer = new BinaryWriter(ms);

            writer.Write(0x00000100);
            writer.Write(numCookies);
            int offset = pageHeaderSize;
            foreach (var rec in records)
            {
                writer.Write(offset);
                offset += rec.Length;
            }
            foreach (var rec in records)
                writer.Write(rec);

            var pageData = ms.ToArray();

            using var finalMs = new MemoryStream();
            using var finalWriter = new BinaryWriter(finalMs);
            finalWriter.Write([0x63, 0x6F, 0x6F, 0x6B]);
            finalWriter.Write(IPAddress.HostToNetworkOrder(1));
            finalWriter.Write(IPAddress.HostToNetworkOrder(totalPageSize));
            finalWriter.Write(pageData);
            return finalMs.ToArray();
        }

        private static byte[] BuildCookieRecord(BinaryCookie cookie)
        {
            byte[] domainBytes = Encoding.UTF8.GetBytes(cookie.Domain + "\0");
            byte[] nameBytes = Encoding.UTF8.GetBytes(cookie.Name + "\0");
            byte[] pathBytes = Encoding.UTF8.GetBytes(cookie.Path + "\0");
            byte[] valueBytes = Encoding.UTF8.GetBytes(cookie.Value + "\0");

            int fixedSize = 52;
            int totalSize = fixedSize + domainBytes.Length + nameBytes.Length + pathBytes.Length + valueBytes.Length;
            byte[] record = new byte[totalSize];
            using var ms = new MemoryStream(record);
            using var writer = new BinaryWriter(ms);

            writer.Write(0);
            writer.Write(cookie.Flags);
            writer.Write(new byte[12]);

            int urlOffset = fixedSize;
            int nameOffset = urlOffset + domainBytes.Length;
            int pathOffset = nameOffset + nameBytes.Length;
            int valueOffset = pathOffset + pathBytes.Length;

            writer.Write(urlOffset);
            writer.Write(nameOffset);
            writer.Write(pathOffset);
            writer.Write(valueOffset);
            writer.Write(cookie.Expiry ?? 0.0);
            writer.Write(cookie.Creation ?? 0.0);

            writer.Write(domainBytes);
            writer.Write(nameBytes);
            writer.Write(pathBytes);
            writer.Write(valueBytes);

            return record;
        }

        private static double MacTimeFromDateTime(DateTime dt) =>
            (dt.ToUniversalTime() - new DateTime(2001, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalSeconds;

        private static string GenerateGuestData()
        {
            using var rng = RandomNumberGenerator.Create();
            byte[] bytes = new byte[4];
            rng.GetBytes(bytes);
            int randomPositive = BitConverter.ToInt32(bytes, 0) & int.MaxValue;
            int userId = -(randomPositive % 999999999) - 1;
            return $"UserID={userId};";
        }

        private static string GenerateTrackerData()
        {
            string createDate = DateTime.UtcNow.ToString("MM/dd/yyyy HH:mm:ss", CultureInfo.InvariantCulture);
            long browserId = GenerateRandomBrowserId();
            return $"CreateDate={createDate}&rbxid=&rbxuid=&browserid={browserId};";
        }

        private static long GenerateRandomBrowserId()
        {
            using var rng = RandomNumberGenerator.Create();
            byte[] bytes = new byte[8];
            rng.GetBytes(bytes);
            long value = BitConverter.ToInt64(bytes, 0) & long.MaxValue;
            return value < 1_000_000_000_000_000L ? value + 1_000_000_000_000_000L : value;
        }

        private static void BackupFile(string filePath)
        {
            if (File.Exists(filePath))
            {
                try { File.Copy(filePath, filePath + ".bak", overwrite: true); }
                catch { /* ignore */ }
            }
        }
    }
}
