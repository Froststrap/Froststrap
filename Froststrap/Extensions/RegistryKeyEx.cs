using Microsoft.Win32;
using System.Runtime.Versioning;

namespace Froststrap.Extensions
{
    [SupportedOSPlatform("windows")]
    internal static class RegistryKeyHelpers
    {
        [SupportedOSPlatform("windows")]
        public static async void SetValueSafe(this RegistryKey registryKey, string? name, object value)
        {
            ArgumentNullException.ThrowIfNull(registryKey);
            ArgumentNullException.ThrowIfNull(value);

            try
            {
                App.Logger.Info($"Writing '{value}' to {registryKey}\\{name}");
                registryKey.SetValue(name, value);
            }
            catch (UnauthorizedAccessException)
            {
                await Frontend.ShowMessageBox(Strings.Dialog_RegistryWriteError, MessageBoxImage.Error);
                App.Terminate(ErrorCode.ERROR_INSTALL_FAILURE);
            }
            catch (ArgumentNullException) when (name is null)
            {
                App.Logger.Error("Cannot set registry value with null name");
                await Frontend.ShowMessageBox(Strings.Dialog_RegistryWriteError, MessageBoxImage.Error);
                App.Terminate(ErrorCode.ERROR_INSTALL_FAILURE);
            }
        }

        [SupportedOSPlatform("windows")]
        public static async void DeleteValueSafe(this RegistryKey registryKey, string name)
        {
            ArgumentNullException.ThrowIfNull(registryKey);
            ArgumentNullException.ThrowIfNull(name);

            try
            {
                App.Logger.Info($"Deleting {registryKey}\\{name}");
                registryKey.DeleteValue(name);
            }
            catch (UnauthorizedAccessException)
            {
                await Frontend.ShowMessageBox(Strings.Dialog_RegistryWriteError, MessageBoxImage.Error);
                App.Terminate(ErrorCode.ERROR_INSTALL_FAILURE);
            }
            catch (ArgumentException)
            {
                App.Logger.Error($"Registry value '{name}' does not exist in {registryKey}");
            }
        }
    }
}