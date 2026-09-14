// SPDX-FileCopyrightText: 2026 Froststrap
// Copyright (C) Froststrap Team
//
// SPDX-License-Identifier: MPL-2.0

using System.Runtime.InteropServices;
using System.Security.Cryptography;
using ktsu.CredentialCache;
using ktsu.Semantics.Strings;

namespace Froststrap.Integrations.AccountManager;

internal static class AccountSecurity
{
    private const string ServiceName = "Froststrap";

    public static string? GetCredential(string accountId)
    {
        if (string.IsNullOrWhiteSpace(accountId))
            return null;

        try
        {
            PersonaGUID persona = CreatePersona(accountId);

            if (ktsu.CredentialCache.CredentialCache.Instance.TryGet(persona, out Credential? credential)
                && credential is CredentialWithToken token)
            {
                return token.Token.ToString(CultureInfo.InvariantCulture);
            }
        }
        catch
        {
        }

        return null;
    }

    public static bool SetCredential(string accountId, string credential)
    {
        if (string.IsNullOrWhiteSpace(accountId) || string.IsNullOrEmpty(credential))
            return false;

        try
        {
            PersonaGUID persona = CreatePersona(accountId);

            ktsu.CredentialCache.CredentialCache.Instance.AddOrReplace(
                persona,
                new CredentialWithToken
                {
                    Token = SemanticString<CredentialToken>.Create(credential),
                }
            );

            return true;
        }
        catch (Exception ex)
        {
            App.Logger.Error($"Failed to store credential for account {accountId}: {ex}");
            return false;
        }
    }

    public static bool DeleteCredential(string accountId)
    {
        if (string.IsNullOrWhiteSpace(accountId))
            return false;

        try
        {
            PersonaGUID persona = CreatePersona(accountId);
            return ktsu.CredentialCache.CredentialCache.Instance.Remove(persona);
        }
        catch
        {
            return false;
        }
    }

    private static PersonaGUID CreatePersona(string accountId) =>
        SemanticString<PersonaGUID>.Create($"{ServiceName}:{accountId}");

    // Legacy DPAPI methods.
    public static string Protect(string text)
    {
        if (string.IsNullOrEmpty(text))
            return string.Empty;

        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return text;

        try
        {
            return Convert.ToBase64String(
                ProtectedData.Protect(
                    Encoding.UTF8.GetBytes(text),
                    null,
                    DataProtectionScope.CurrentUser
                )
            );
        }
        catch
        {
            return text;
        }
    }

    public static bool TryUnprotect(string text, out string unprotected)
    {
        unprotected = string.Empty;

        if (string.IsNullOrEmpty(text))
            return false;

        // Legacy Linux/macOS credentials were stored as plaintext.
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            unprotected = text;
            return true;
        }

        try
        {
            byte[] protectedData = Convert.FromBase64String(text);

            byte[] decryptedData = ProtectedData.Unprotect(
                protectedData,
                null,
                DataProtectionScope.CurrentUser
            );

            unprotected = Encoding.UTF8.GetString(decryptedData);

            return !string.IsNullOrEmpty(unprotected);
        }
        catch
        {
            return false;
        }
    }
}
