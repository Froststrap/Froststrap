namespace Froststrap.Utility
{
    public static class ThemeCycler
    {
        public static void HandleLaunchCycle()
        {
            if (!App.Settings.Prop.CycleEnabled)
                return;

            if (App.Settings.Prop.BootstrapperStyle != BootstrapperStyle.CustomDialog)
                return;

            if (App.Settings.Prop.CycleEnabledCustomThemes == null || App.Settings.Prop.CycleEnabledCustomThemes.Count == 0)
                return;

            bool shouldCycle;

            if (App.Settings.Prop.CycleFrequency == CycleFrequency.EveryLaunch)
            {
                shouldCycle = true;
            }
            else
            {
                TimeSpan elapsed = DateTime.Now - App.Settings.Prop.CycleLastCycleTime;
                shouldCycle = App.Settings.Prop.CycleFrequency switch
                {
                    CycleFrequency.Minutes => elapsed.TotalMinutes >= App.Settings.Prop.CycleIntervalValue,
                    CycleFrequency.Hours => elapsed.TotalHours >= App.Settings.Prop.CycleIntervalValue,
                    CycleFrequency.Days => elapsed.TotalDays >= App.Settings.Prop.CycleIntervalValue,
                    _ => false
                };
            }

            if (shouldCycle)
            {
                App.Settings.Prop.CycleCurrentIndex = (App.Settings.Prop.CycleCurrentIndex + 1) % App.Settings.Prop.CycleEnabledCustomThemes.Count;

                App.Settings.Prop.SelectedCustomTheme = App.Settings.Prop.CycleEnabledCustomThemes[App.Settings.Prop.CycleCurrentIndex];
                App.Settings.Prop.CycleLastCycleTime = DateTime.Now;

                App.Settings.Save();

                App.Logger.WriteLine("ThemeCycler",$"Changed to '{App.Settings.Prop.SelectedCustomTheme}'");
            }
        }
    }
}