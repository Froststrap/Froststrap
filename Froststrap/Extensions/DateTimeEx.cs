// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

namespace Froststrap.Extensions
{
    static class DateTimeEx
    {
        public static string ToFriendlyString(this DateTime dateTime)
        {
            return dateTime.ToString("dddd, d MMMM yyyy 'at' h:mm:ss tt", CultureInfo.InvariantCulture);
        }
    }
}
