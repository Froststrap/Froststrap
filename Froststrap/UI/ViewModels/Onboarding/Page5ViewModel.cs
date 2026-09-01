namespace Froststrap.UI.ViewModels.Onboarding
{
    internal class Page5ViewModel : NotifyPropertyChangedViewModel
    {
        public ShortcutTask DesktopIconTask { get; } = new("Desktop", Paths.Desktop, $"{App.ProjectName}.lnk");
        public ShortcutTask StartMenuIconTask { get; } = new("StartMenu", Paths.WindowsStartMenu, $"{App.ProjectName}.lnk");
        public ShortcutTask PlayerIconTask { get; } = new("RobloxPlayer", Paths.Desktop, $"{Strings.LaunchMenu_LaunchRoblox}.lnk", "-player");
        public ShortcutTask StudioIconTask { get; } = new("RobloxStudio", Paths.Desktop, $"{Strings.LaunchMenu_LaunchRobloxStudio}.lnk", "-studio");
        public ShortcutTask SettingsIconTask { get; } = new("Settings", Paths.Desktop, $"{Strings.Menu_Title}.lnk", "-settings");
    }
}
