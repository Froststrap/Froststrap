// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

using Froststrap.Clients.Contracts;
using Froststrap.Clients.Model;

namespace Froststrap.Clients.Providers;

internal static class ClientLogLocators
{
    public static ILogLocator Create(IClientLayout layout, Func<string?> logsRoot) => layout.LogFormat switch
    {
        LogShape.PlainLog => new PlainLogLocator(layout, logsRoot),
        _ => new NativeLogLocator(layout, logsRoot)
    };
}
