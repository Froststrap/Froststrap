// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

namespace Froststrap.Clients.Contracts;

internal readonly record struct StoredCookies(string AuthCookie, string BrowserTracker);

internal interface ICookieStore
{
    string? CookiesPath { get; }

    Task<StoredCookies> ReadAsync();

    bool WriteAuthCookie(string plainCookie);
}
