using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;

namespace Froststrap.UI.ViewModels
{
    public static class GlobalViewModel
    {
        public static bool IsWindows => OperatingSystem.IsWindows();
        public static bool IsMacOS => OperatingSystem.IsMacOS();
        public static bool IsLinux => OperatingSystem.IsLinux();

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