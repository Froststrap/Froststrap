// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

namespace Froststrap.Enums
{
    internal enum CycleFrequency
    {
        [EnumName(FromTranslation = "Enums.CycleFrequency.EveryLaunch")]
        EveryLaunch,

        [EnumName(FromTranslation = "Enums.CycleFrequency.Minutes")]
        Minutes,

        [EnumName(FromTranslation = "Enums.CycleFrequency.Hours")]
        Hours,

        [EnumName(FromTranslation = "Enums.CycleFrequency.Days")]
        Days
    }
}