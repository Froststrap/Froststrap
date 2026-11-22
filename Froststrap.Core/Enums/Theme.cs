// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

namespace Froststrap.Enums
{
    internal enum Theme
    {
        [EnumName(FromTranslation = "Common.SystemDefault")]
        Default,
        Dark,
        Light,
        [EnumName(FromTranslation = "Enums.Theme.Custom")]
        Custom,

        [EnumName(FromTranslation = "Enums.Theme.Image")]
        Image
    }
}
