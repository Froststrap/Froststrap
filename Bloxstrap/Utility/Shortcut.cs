using System.Windows;

namespace Bloxstrap.Utility
{
    internal static class Shortcut
    {
        private static GenericTriState _loadStatus = GenericTriState.Unknown;

        public static void Create(string exePath, string exeArgs, string lnkPath, string? iconPath = null)
        {
            const string LOG_IDENT = "Shortcut::Create";

            if (File.Exists(lnkPath))
                return;

            try
            {
                string finalIconPath = string.IsNullOrEmpty(iconPath) ? exePath : iconPath;

                ShellLink.Shortcut.CreateShortcut(exePath, exeArgs, finalIconPath, 0).WriteToFile(lnkPath);

                if (_loadStatus != GenericTriState.Successful)
                    _loadStatus = GenericTriState.Successful;
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine(LOG_IDENT, $"Failed to create a shortcut for {lnkPath}!");
                App.Logger.WriteException(LOG_IDENT, ex);

                if (_loadStatus == GenericTriState.Failed)
                    return;

                _loadStatus = GenericTriState.Failed;

                Frontend.ShowMessageBox(Strings.Dialog_CannotCreateShortcuts, MessageBoxImage.Warning);
            }
        }
    }
}