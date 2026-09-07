// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

namespace Froststrap.UI.ViewModels.Bootstrapper
{
    internal class TwentyFiveDialogViewModel(IBootstrapperDialog dialog) : BootstrapperDialogViewModel(dialog)
    {
        public bool CancelButtonVisibility => CancelEnabled;
    }
}