// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

namespace Froststrap.UI
{
    internal interface IBootstrapperDialog
    {
        public Bootstrapper? Bootstrapper { get; set; }
        string Message { get; set; }
        string CancelButtonText { get; set; }
        bool ProgressIndeterminate { get; set; }
        int ProgressValue { get; set; }
        int ProgressMaximum { get; set; }
        TaskbarItemProgressState TaskbarProgressState { get; set; }
        double TaskbarProgressValue { get; set; }
        bool CancelEnabled { get; set; }

        void ShowBootstrapper();
        void CloseBootstrapper();
        void ShowSuccess(string message, Action? callback = null);
    }
}