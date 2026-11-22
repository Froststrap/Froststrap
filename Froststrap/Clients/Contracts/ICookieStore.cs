// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

namespace Froststrap.Clients.Contracts;

internal interface ICookieStore
{
    string? CookiesPath { get; }

    Task<string> ReadAuthCookieAsync();

    bool WriteAuthCookie(string plainCookie);
}
