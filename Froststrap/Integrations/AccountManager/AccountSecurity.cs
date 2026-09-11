// SPDX-FileCopyrightText: 2026 Froststrap
// Copyright (C) Froststrap Team
//
// SPDX-License-Identifier: MPL-2.0

using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;

namespace Froststrap.Integrations.AccountManager
{
    internal static class AccountSecurity
    {
        public static string Protect(string text)
        {
            if (string.IsNullOrEmpty(text)) return "";
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return text;

            try
            {
                return Convert.ToBase64String(ProtectedData.Protect(
                    Encoding.UTF8.GetBytes(text), null, DataProtectionScope.CurrentUser));
            }
            catch
            {
                return text;
            }
        }

        public static string Unprotect(string text)
        {
            if (string.IsNullOrEmpty(text)) return "";
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return text;

            try
            {
                return Encoding.UTF8.GetString(ProtectedData.Unprotect(
                    Convert.FromBase64String(text), null, DataProtectionScope.CurrentUser));
            }
            catch
            {
                return text;
            }
        }
    }
}
