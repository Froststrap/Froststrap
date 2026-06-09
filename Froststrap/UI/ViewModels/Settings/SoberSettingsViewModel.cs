namespace Froststrap.UI.ViewModels.Settings
{
    public class SoberSettingsViewModel : NotifyPropertyChangedViewModel
    {
        public static bool SoberAllowGamepadPermission
        {
            get => App.SoberSettings.GetPreset("AllowGamepadPermission") == "true";
            set => App.SoberSettings.SetPreset("AllowGamepadPermission", value);
        }

        public static bool SoberEnableGamemode
        {
            get => App.SoberSettings.GetPreset("EnableGamemode") == "true";
            set => App.SoberSettings.SetPreset("EnableGamemode", value);
        }

        public static bool SoberEnableHiDpi
        {
            get => App.SoberSettings.GetPreset("EnableHiDpi") == "true";
            set => App.SoberSettings.SetPreset("EnableHiDpi", value);
        }

        public static bool SoberTouchMode
        {
            get => App.SoberSettings.GetPreset("TouchMode") == "on";
            set => App.SoberSettings.SetPreset("TouchMode", value ? "on" : "off");
        }

        public static bool SoberUseConsoleExperience
        {
            get => App.SoberSettings.GetPreset("UseConsoleExperience") == "true";
            set => App.SoberSettings.SetPreset("UseConsoleExperience", value);
        }

        public static bool SoberUseLibsecret
        {
            get => App.SoberSettings.GetPreset("UseLibsecret") == "true";
            set => App.SoberSettings.SetPreset("UseLibsecret", value);
        }

        public static bool SoberUseOpengl
        {
            get => App.SoberSettings.GetPreset("UseOpengl") == "true";
            set => App.SoberSettings.SetPreset("UseOpengl", value);
        }
    }
}
