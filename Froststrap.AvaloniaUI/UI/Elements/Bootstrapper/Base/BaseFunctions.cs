using System;
using System.Collections.Generic;
using System.Text;

namespace Froststrap.UI.Elements.Bootstrapper.Base
{
    static class BaseFunctions
    {
        public static void ShowSuccess(CustomDialog customDialog, string message, Action? callback)
        {
			Frontend.ShowMessageBox(message, MessageBoxImage.Information);

			if (callback is not null)
				callback();

			App.Terminate();
		}
	}
}
