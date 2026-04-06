using Microsoft.Win32;
using System.Runtime.Versioning;

namespace Froststrap.Extensions
{
    [SupportedOSPlatform("windows")]
    public static class RegistryKeyEx
    {
        [SupportedOSPlatform("windows")]
        public static async void SetValueSafe(this RegistryKey registryKey, string? name, object value)
        {
            try
            {
                App.Logger.WriteLine("RegistryKeyEx::SetValueSafe", $"Writing '{value}' to {registryKey}\\{name}");
                registryKey.SetValue(name, value);
            }
            catch (UnauthorizedAccessException)
            {
                await Frontend.ShowMessageBox(Strings.Dialog_RegistryWriteError, MessageBoxImage.Error);
                App.Terminate(ErrorCode.ERROR_INSTALL_FAILURE);
            }
        }

        [SupportedOSPlatform("windows")]
        public static async void DeleteValueSafe(this RegistryKey registryKey, string name)
        {
            try
            {
                App.Logger.WriteLine("RegistryKeyEx::DeleteValueSafe", $"Deleting {registryKey}\\{name}");
                registryKey.DeleteValue(name);
            }
            catch (UnauthorizedAccessException)
            {
                await Frontend.ShowMessageBox(Strings.Dialog_RegistryWriteError, MessageBoxImage.Error);
                App.Terminate(ErrorCode.ERROR_INSTALL_FAILURE);
            }
        }
    }
}
