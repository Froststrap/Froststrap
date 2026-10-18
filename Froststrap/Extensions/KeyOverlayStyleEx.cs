// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

using Avalonia.Media;

using Froststrap.Enums;

namespace Froststrap.Extensions
{
    static class KeyOverlayStyleEx
    {
        public static KeyOverlayStyleDefinition GetDefinition(this KeyOverlayStyle keyOverlayStyle) => keyOverlayStyle switch
        {
            KeyOverlayStyle.Light => new KeyOverlayStyleDefinition(
                KeyOverlayLayout.Standard,
                Color.Parse("#E6FFFFFF"),
                Color.Parse("#FFE3E3E3"),
                Color.Parse("#FF1A1A1A"),
                Color.Parse("#33000000"),
                Color.Parse("#8C000000")),

            KeyOverlayStyle.MinecraftDark => new KeyOverlayStyleDefinition(
                KeyOverlayLayout.Minecraft,
                Color.Parse("#00000000"),
                Color.Parse("#B31B1B22"),
                Color.Parse("#F2FFFFFF"),
                Color.Parse("#66FFFFFF"),
                Color.Parse("#99FFFFFF")),

            KeyOverlayStyle.MinecraftLight => new KeyOverlayStyleDefinition(
                KeyOverlayLayout.Minecraft,
                Color.Parse("#00000000"),
                Color.Parse("#B3E3E3E3"),
                Color.Parse("#FF1A1A1A"),
                Color.Parse("#33000000"),
                Color.Parse("#8C000000")),

            _ => new KeyOverlayStyleDefinition(
                KeyOverlayLayout.Standard,
                Color.Parse("#D91B1B22"),
                Color.Parse("#59383843"),
                Color.Parse("#F2FFFFFF"),
                Color.Parse("#66FFFFFF"),
                Color.Parse("#99FFFFFF"))
        };

        public static IReadOnlyCollection<KeyOverlayStyle> Selections =>
        [
            KeyOverlayStyle.Dark,
            KeyOverlayStyle.Light,
            KeyOverlayStyle.MinecraftDark,
            KeyOverlayStyle.MinecraftLight
        ];
    }
}
