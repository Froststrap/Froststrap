// SPDX-FileCopyrightText: 2026 Froststrap
// Copyright (C) Froststrap Team
//
// SPDX-License-Identifier: MPL-2.0

using System.Runtime.InteropServices;

namespace Froststrap.Integrations.AccountManager;

internal static class CredentialStore
{
    private const string TargetPrefix = "ktsu.CredentialCache:Froststrap:";
    private const string FileName = "credentials.json";
    private const uint CredentialTypeGeneric = 1;
    private const uint CredentialPersistLocalMachine = 2;
    private const int ErrorNotFound = 1168;

    private static readonly Lock FileLock = new();

    public static bool TryGet(string accountId, out string token)
    {
        token = string.Empty;

        if (string.IsNullOrWhiteSpace(accountId))
            return false;

        return OperatingSystem.IsWindows()
            ? TryGetWindows(accountId, out token)
            : TryGetFile(accountId, out token);
    }

    public static bool Set(string accountId, string token)
    {
        if (string.IsNullOrWhiteSpace(accountId) || string.IsNullOrEmpty(token))
            return false;

        return OperatingSystem.IsWindows()
            ? SetWindows(accountId, token)
            : SetFile(accountId, token);
    }

    public static bool Remove(string accountId)
    {
        if (string.IsNullOrWhiteSpace(accountId))
            return false;

        return OperatingSystem.IsWindows()
            ? RemoveWindows(accountId)
            : RemoveFile(accountId);
    }

    private static string BuildTarget(string accountId) => TargetPrefix + accountId;

    private static bool TryGetWindows(string accountId, out string token)
    {
        token = string.Empty;

        if (!CredRead(BuildTarget(accountId), CredentialTypeGeneric, 0, out IntPtr handle))
            return false;

        try
        {
            Credential credential = Marshal.PtrToStructure<Credential>(handle);

            if (credential.CredentialBlobSize == 0 || credential.CredentialBlob == IntPtr.Zero)
                return false;

            byte[] bytes = new byte[credential.CredentialBlobSize];
            Marshal.Copy(credential.CredentialBlob, bytes, 0, bytes.Length);

            token = ExtractToken(Encoding.UTF8.GetString(bytes));
            return !string.IsNullOrEmpty(token);
        }
        finally
        {
            CredFree(handle);
        }
    }

    private static bool SetWindows(string accountId, string token)
    {
        string blob = JsonHelper.Serialize(new StoredCredential { Token = token }, JsonHelper.PlainOptions);
        byte[] bytes = Encoding.UTF8.GetBytes(blob);

        IntPtr targetPtr = Marshal.StringToHGlobalUni(BuildTarget(accountId));
        IntPtr userNamePtr = Marshal.StringToHGlobalUni(accountId);
        IntPtr blobPtr = Marshal.AllocHGlobal(bytes.Length);

        try
        {
            Marshal.Copy(bytes, 0, blobPtr, bytes.Length);

            Credential credential = new()
            {
                Type = CredentialTypeGeneric,
                TargetName = targetPtr,
                CredentialBlobSize = (uint)bytes.Length,
                CredentialBlob = blobPtr,
                Persist = CredentialPersistLocalMachine,
                UserName = userNamePtr,
            };

            return CredWrite(ref credential, 0);
        }
        finally
        {
            Marshal.FreeHGlobal(blobPtr);
            Marshal.FreeHGlobal(userNamePtr);
            Marshal.FreeHGlobal(targetPtr);
        }
    }

    private static bool RemoveWindows(string accountId)
    {
        if (CredDelete(BuildTarget(accountId), CredentialTypeGeneric, 0))
            return true;

        return Marshal.GetLastPInvokeError() == ErrorNotFound;
    }

    private static string ExtractToken(string blob)
    {
        if (string.IsNullOrWhiteSpace(blob))
            return string.Empty;

        try
        {
            using JsonDocument document = JsonDocument.Parse(blob);

            if (document.RootElement.ValueKind == JsonValueKind.Object
                && document.RootElement.TryGetProperty("Token", out JsonElement tokenElement)
                && tokenElement.ValueKind == JsonValueKind.String)
            {
                return tokenElement.GetString() ?? string.Empty;
            }
        }
        catch (JsonException)
        {
        }

        return blob;
    }

    private static string FilePath => Path.Combine(Paths.Cache, FileName);

    private static bool TryGetFile(string accountId, out string token)
    {
        token = string.Empty;

        lock (FileLock)
        {
            var data = ReadFile();

            if (data.TryGetValue(accountId, out string? value) && !string.IsNullOrEmpty(value))
            {
                token = value;
                return true;
            }
        }

        return false;
    }

    private static bool SetFile(string accountId, string token)
    {
        lock (FileLock)
        {
            var data = ReadFile();
            data[accountId] = token;
            return WriteFile(data);
        }
    }

    private static bool RemoveFile(string accountId)
    {
        lock (FileLock)
        {
            var data = ReadFile();

            if (!data.Remove(accountId))
                return true;

            return WriteFile(data);
        }
    }

    private static Dictionary<string, string> ReadFile()
    {
        try
        {
            if (!File.Exists(FilePath))
                return [];

            string json = File.ReadAllText(FilePath);
            return JsonHelper.Deserialize<Dictionary<string, string>>(json, JsonHelper.PlainOptions) ?? [];
        }
        catch
        {
            return [];
        }
    }

    private static bool WriteFile(Dictionary<string, string> data)
    {
        try
        {
            Directory.CreateDirectory(Paths.Cache);
            File.WriteAllText(FilePath, JsonHelper.Serialize(data, JsonHelper.PlainOptions));

            if (!OperatingSystem.IsWindows())
                File.SetUnixFileMode(FilePath, UnixFileMode.UserRead | UnixFileMode.UserWrite);

            return true;
        }
        catch
        {
            return false;
        }
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct Credential
    {
        public uint Flags;
        public uint Type;
        public IntPtr TargetName;
        public IntPtr Comment;
        public long LastWritten;
        public uint CredentialBlobSize;
        public IntPtr CredentialBlob;
        public uint Persist;
        public uint AttributeCount;
        public IntPtr Attributes;
        public IntPtr TargetAlias;
        public IntPtr UserName;
    }

    [DllImport("advapi32.dll", EntryPoint = "CredReadW", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool CredRead(string target, uint type, uint flags, out IntPtr credential);

    [DllImport("advapi32.dll", EntryPoint = "CredWriteW", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool CredWrite(ref Credential credential, uint flags);

    [DllImport("advapi32.dll", EntryPoint = "CredDeleteW", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool CredDelete(string target, uint type, uint flags);

    [DllImport("advapi32.dll", EntryPoint = "CredFree", SetLastError = true)]
    private static extern void CredFree(IntPtr buffer);
}
