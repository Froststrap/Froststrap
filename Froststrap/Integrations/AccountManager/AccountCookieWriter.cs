// SPDX-FileCopyrightText: 2026 Froststrap
// Copyright (C) Froststrap Team
//
// SPDX-License-Identifier: MPL-2.0

namespace Froststrap.Integrations.AccountManager;

internal static class AccountCookieWriter
{
    public static bool WriteCookieFileForAccount(AccountManagerAccount account)
    {
        string plainCookie = account.SecurityToken;
        if (string.IsNullOrEmpty(plainCookie))
        {
            App.Logger.Info("Account has no valid cookie.");
            return false;
        }

        try
        {
            return App.Clients.Player.Cookies.WriteAuthCookie(plainCookie);
        }
        catch (Exception ex)
        {
            App.Logger.Error(ex);
            return false;
        }
    }
}
