// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

namespace Froststrap.UI.Elements.Bootstrapper.Base
{
    static class BaseFunctions
    {
        public static async void ShowSuccess(string message, Action? callback)
        {
            await Frontend.ShowMessageBox(message, MessageBoxImage.Information);

            if (callback is not null)
                callback();

            App.Terminate();
        }
    }
}