// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

namespace Froststrap.Extensions
{
    static class CleanerOptionsEx
    {
        public static IReadOnlyCollection<CleanerOptions> Selections =>
        [
            CleanerOptions.Never,
            CleanerOptions.OneDay,
            CleanerOptions.OneWeek,
            CleanerOptions.OneMonth,
            CleanerOptions.TwoMonths
        ];
    }
}