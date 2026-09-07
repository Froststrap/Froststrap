// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

namespace Froststrap.Extensions
{
    static class BootstrapperStyleEx
    {
        public static async Task<IBootstrapperDialog> GetNew(this BootstrapperStyle bootstrapperStyle) => await Frontend.GetBootstrapperDialog(bootstrapperStyle);

        public static IReadOnlyCollection<BootstrapperStyle> Selections =>
        [
            BootstrapperStyle.FluentAeroDialog,
            BootstrapperStyle.FluentDialog,
            BootstrapperStyle.ClassicFluentDialog,
            BootstrapperStyle.TwentyFiveDialog,
            BootstrapperStyle.ByfronDialog,
            BootstrapperStyle.ModernDialog,
            BootstrapperStyle.CustomDialog
        ];
    }
}