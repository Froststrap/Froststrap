// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

namespace Froststrap.Integrations.Discord
{
    internal static class DiscordPresenceHelper
    {
        public static string Truncate(string? value, int maxLength)
            => value is null || value.Length <= maxLength ? value ?? string.Empty : value[..maxLength];
    }
}
