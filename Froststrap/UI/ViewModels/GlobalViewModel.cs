using System.Windows.Input;
using System.Runtime.InteropServices;
using CommunityToolkit.Mvvm.Input;

namespace Froststrap.UI.ViewModels
{
    public static class GlobalViewModel
    {
        public static bool IsWindows => RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
        public static bool IsMacOS => RuntimeInformation.IsOSPlatform(OSPlatform.OSX);
        public static bool IsLinux => RuntimeInformation.IsOSPlatform(OSPlatform.Linux);

        public static bool IsWindowsOrLinux => IsWindows || IsLinux;
        public static bool IsWindowsOrMacOS => IsWindows || IsMacOS;

        public static ICommand OpenWebpageCommand => new RelayCommand<string>(OpenWebpage);

        private static void OpenWebpage(string? location)
        {
            if (string.IsNullOrEmpty(location))
                return;

            Utilities.ShellExecute(location);
        }
    }
}