// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

namespace Froststrap.UI.ViewModels.Onboarding
{
    internal class Page5ViewModel : NotifyPropertyChangedViewModel
    {
        public ShortcutTask DesktopIconTask { get; } = new("Desktop", Paths.Desktop, $"{App.ProjectName}.lnk");
        public ShortcutTask StartMenuIconTask { get; } = new("StartMenu", Paths.WindowsStartMenu, $"{App.ProjectName}.lnk");
        public ShortcutTask PlayerIconTask { get; } = new("RobloxPlayer", Paths.Desktop, $"{Localizer.Instance.Get("LaunchMenu_LaunchRoblox")}.lnk", LaunchEntry.GetCliArgs(LaunchMode.Player));
        public ShortcutTask StudioIconTask { get; } = new("RobloxStudio", Paths.Desktop, $"{Localizer.Instance.Get("LaunchMenu_LaunchRobloxStudio")}.lnk", LaunchEntry.GetCliArgs(LaunchMode.Studio));
        public ShortcutTask SettingsIconTask { get; } = new("Settings", Paths.Desktop, $"{Localizer.Instance.Get("Menu_Title")}.lnk", "--settings");
    }
}
