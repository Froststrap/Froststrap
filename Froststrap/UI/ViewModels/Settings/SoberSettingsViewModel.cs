namespace Froststrap.UI.ViewModels.Settings
{
    public class SoberSettingsViewModel : NotifyPropertyChangedViewModel
    {
        public static bool SoberAllowGamepadPermission
        {
            get => App.SoberSettings.Prop.AllowGamepadPermission;
            set => App.SoberSettings.Prop.AllowGamepadPermission = value;
        }

        public static bool SoberEnableGamemode
        {
            get => App.SoberSettings.Prop.EnableGamemode;
            set => App.SoberSettings.Prop.EnableGamemode = value;
        }

        public static bool SoberEnableHiDpi
        {
            get => App.SoberSettings.Prop.EnableHiDpi;
            set => App.SoberSettings.Prop.EnableHiDpi = value;
        }

        public static bool SoberTouchMode
        {
            get => App.SoberSettings.Prop.TouchMode == "on";
            set => App.SoberSettings.Prop.TouchMode = value ? "on" : "off";
        }

        public static bool SoberUseConsoleExperience
        {
            get => App.SoberSettings.Prop.UseConsoleExperience;
            set => App.SoberSettings.Prop.UseConsoleExperience = value;
        }

        public static bool SoberUseLibsecret
        {
            get => App.SoberSettings.Prop.UseLibsecret;
            set => App.SoberSettings.Prop.UseLibsecret = value;
        }

        public static bool SoberUseOpengl
        {
            get => App.SoberSettings.Prop.UseOpengl;
            set => App.SoberSettings.Prop.UseOpengl = value;
        }
    }
}