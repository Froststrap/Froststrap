// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

namespace Froststrap.UI.ViewModels.Bootstrapper
{
    internal class ClassicFluentDialogViewModel(IBootstrapperDialog dialog) : BootstrapperDialogViewModel(dialog)
    {
        public static double FooterOpacity => (OperatingSystem.IsWindows() && Environment.OSVersion.Version.Build >= 22000) ? 0.4 : 1.0;
    }
}