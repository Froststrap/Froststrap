// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

namespace Froststrap.Enums
{
    internal enum BackgroundStretch
    {
        [EnumName(FromTranslation = "Enums.BackgroundStretch.None")]
        None,

        [EnumName(FromTranslation = "Enums.BackgroundStretch.Fill")]
        Fill,

        [EnumName(FromTranslation = "Enums.BackgroundStretch.Uniform")]
        Uniform,

        [EnumName(FromTranslation = "Enums.BackgroundStretch.UniformToFill")]
        UniformToFill
    }
}