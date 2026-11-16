// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

using Froststrap.Enums.AppStoragePresets;

namespace Froststrap.Extensions;

internal sealed class RobloxThemeEx
{
    public static IReadOnlyCollection<RobloxTheme> Selections =>
    [
        RobloxTheme.Default,
        RobloxTheme.Light,
        RobloxTheme.Dark
    ];
}
