// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

using Froststrap.Clients.Model;

namespace Froststrap.Clients.Providers.Cookies;

internal sealed class BinaryCookieStore(Func<ClientInstall> install) : CookieStore(install)
{
    public override string? CookiesPath => Path.Combine(Install.CookiesDirectory!, "com.roblox.RobloxPlayer.binarycookies");

    public override Task<string> ReadAuthCookieAsync() => LoadMacCookies(CookiesPath!);

    public override bool WriteAuthCookie(string plainCookie) => WriteMacCookieFile(plainCookie, CookiesPath!);

    private static async Task<string> LoadMacCookies(string cookiesPath)
    {
        byte[] fileBytes = await File.ReadAllBytesAsync(cookiesPath);
        var cookies = ParseBinaryCookies(fileBytes);
        return ExtractRoblosecurity(cookies) ?? string.Empty;
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
        int pageHeaderSize = 4 + 4 + records.Count * 4 + 4;

        using var pageStream = new MemoryStream();
        using (var writer = new BinaryWriter(pageStream, Encoding.UTF8, leaveOpen: true))
        {
            writer.Write(PageHeader);
            writer.Write(records.Count);

            int offset = pageHeaderSize;
            foreach (var record in records)
            {
                writer.Write(offset);
                offset += record.Length;
            }

            writer.Write(0);

            foreach (var record in records)
                writer.Write(record);
        }

        byte[] page = pageStream.ToArray();

        int checksum = 0;
        for (int i = 0; i < page.Length; i += 4)
            checksum += page[i];

        using var fileStream = new MemoryStream();
        using var fileWriter = new BinaryWriter(fileStream);
        fileWriter.Write(FileMagic);
        fileWriter.Write(IPAddress.HostToNetworkOrder(1));
        fileWriter.Write(IPAddress.HostToNetworkOrder(page.Length));
        fileWriter.Write(page);
        fileWriter.Write(IPAddress.HostToNetworkOrder(checksum));
        fileWriter.Write(FileFooter);
        return fileStream.ToArray();
    }

    private static readonly byte[] FileMagic = [0x63, 0x6F, 0x6F, 0x6B];

    private static readonly byte[] PageHeader = [0x00, 0x00, 0x01, 0x00];

    private static readonly byte[] FileFooter = [0x07, 0x17, 0x20, 0x05, 0x00, 0x00, 0x00, 0x4B];

    private static byte[] BuildCookieRecord(BinaryCookie cookie)
    {
        byte[] domainBytes = Encoding.UTF8.GetBytes(cookie.Domain + "\0");
        byte[] nameBytes = Encoding.UTF8.GetBytes(cookie.Name + "\0");
        byte[] pathBytes = Encoding.UTF8.GetBytes(cookie.Path + "\0");
        byte[] valueBytes = Encoding.UTF8.GetBytes(cookie.Value + "\0");

        const int fixedSize = 56;
        int totalSize = fixedSize + domainBytes.Length + nameBytes.Length + pathBytes.Length + valueBytes.Length;
        byte[] record = new byte[totalSize];
        using var ms = new MemoryStream(record);
        using var writer = new BinaryWriter(ms);

        int urlOffset = fixedSize;
        int nameOffset = urlOffset + domainBytes.Length;
        int pathOffset = nameOffset + nameBytes.Length;
        int valueOffset = pathOffset + pathBytes.Length;

        writer.Write(totalSize);
        writer.Write(0);
        writer.Write(cookie.Flags);
        writer.Write(0);
        writer.Write(urlOffset);
        writer.Write(nameOffset);
        writer.Write(pathOffset);
        writer.Write(valueOffset);
        writer.Write(0L);
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

    private struct ParsedCookie
    {
        public string Name { get; set; }
        public string Value { get; set; }
        public string Domain { get; set; }
        public string Path { get; set; }
        public int Flags { get; set; }
        public DateTime? Expiry { get; set; }
        public DateTime? Creation { get; set; }
    }

    private static List<ParsedCookie> ParseBinaryCookies(byte[] data)
    {
        var cookies = new List<ParsedCookie>();
        int offset = 0;

        if (data.Length < 4 || data[0] != 0x63 || data[1] != 0x6F || data[2] != 0x6F || data[3] != 0x6B)
            throw new InvalidOperationException("Not a binarycookies file");

        offset += 4;
        int numPages = ReadBigEndianInt32(data, ref offset);

        int[] pageSizes = new int[numPages];
        for (int i = 0; i < numPages; i++)
            pageSizes[i] = ReadBigEndianInt32(data, ref offset);

        for (int p = 0; p < numPages; p++)
        {
            int pageStart = offset;
            int pageEnd = pageStart + pageSizes[p];

            int pageHeader = ReadLittleEndianInt32(data, pageStart);
            if (pageHeader != 0x00000100 && pageHeader != 0x00010000)
                App.Logger.Error($"Unexpected page header: 0x{pageHeader:X} at offset {pageStart}");

            int numCookies = ReadLittleEndianInt32(data, pageStart + 4);
            int cookieOffsetsOffset = pageStart + 8;

            for (int c = 0; c < numCookies; c++)
            {
                int cookieOffset = pageStart + ReadLittleEndianInt32(data, cookieOffsetsOffset + c * 4);
                try
                {
                    var cookie = ParseSingleCookie(data, cookieOffset);
                    cookies.Add(cookie);
                }
                catch (Exception ex)
                {
                    App.Logger.Error($"Failed to parse cookie {c} in page {p}: {ex.Message}");
                }
            }

            offset = pageEnd;
        }

        return cookies;
    }

    private static ParsedCookie ParseSingleCookie(byte[] data, int offset)
    {
        int flags = ReadLittleEndianInt32(data, offset + 4);
        int urlOffset = ReadLittleEndianInt32(data, offset + 16);
        int nameOffset = ReadLittleEndianInt32(data, offset + 20);
        int pathOffset = ReadLittleEndianInt32(data, offset + 24);
        int valueOffset = ReadLittleEndianInt32(data, offset + 28);
        double expiryTime = BitConverter.ToDouble(data, offset + 40);
        double creationTime = BitConverter.ToDouble(data, offset + 48);

        string domain = ReadNullTerminatedString(data, offset + urlOffset);
        string name = ReadNullTerminatedString(data, offset + nameOffset);
        string path = ReadNullTerminatedString(data, offset + pathOffset);
        string value = ReadNullTerminatedString(data, offset + valueOffset);

        return new ParsedCookie
        {
            Name = name,
            Value = value,
            Domain = domain,
            Path = path,
            Flags = flags,
            Expiry = MacTimeToDateTime(expiryTime),
            Creation = MacTimeToDateTime(creationTime)
        };
    }

    private static string ReadNullTerminatedString(byte[] data, int offset)
    {
        int end = offset;
        while (end < data.Length && data[end] != 0) end++;
        return Encoding.UTF8.GetString(data, offset, end - offset);
    }

    private static int ReadBigEndianInt32(byte[] data, ref int offset)
    {
        int value = (data[offset] << 24) | (data[offset + 1] << 16) | (data[offset + 2] << 8) | data[offset + 3];
        offset += 4;
        return value;
    }

    private static int ReadLittleEndianInt32(byte[] data, int offset)
    {
        return data[offset] | (data[offset + 1] << 8) | (data[offset + 2] << 16) | (data[offset + 3] << 24);
    }

    private static DateTime? MacTimeToDateTime(double macTime)
    {
        if (macTime == 0) return null;
        DateTime epoch = new(2001, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        return epoch.AddSeconds(macTime).ToLocalTime();
    }

    private static string? ExtractRoblosecurity(List<ParsedCookie> cookies)
    {
        var cookie = cookies.FirstOrDefault(c =>
            c.Name == ".ROBLOSECURITY" &&
            c.Domain.Contains(".roblox.com", StringComparison.Ordinal));
        return cookie.Value;
    }
}
