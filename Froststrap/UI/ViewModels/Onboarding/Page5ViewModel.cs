// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

namespace Froststrap.UI.ViewModels.Onboarding;

internal sealed class Page5ViewModel : NotifyPropertyChangedViewModel
{
    public ShortcutTask DesktopIconTask { get; } = new("Desktop", Paths.Desktop, $"{ProjectInfo.Name}.lnk");
    public ShortcutTask StartMenuIconTask { get; } = new("StartMenu", Paths.WindowsStartMenu, $"{ProjectInfo.Name}.lnk");
    public ShortcutTask PlayerIconTask { get; } = ShortcutTask.ForLaunch(LaunchMode.Player, Strings.LaunchMenu_LaunchRoblox);
    public ShortcutTask StudioIconTask { get; } = ShortcutTask.ForLaunch(LaunchMode.Studio, Strings.LaunchMenu_LaunchRobloxStudio);
    public ShortcutTask SettingsIconTask { get; } = new("Settings", Paths.Desktop, $"{Strings.Menu_Title}.lnk", "--settings");
}
