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

            if (!shouldCycle)
                return;

            var validThemes = App.Settings.Prop.CycleEnabledCustomThemes
                .Where(name => Directory.Exists(Path.Combine(Paths.CustomThemes, name)))
                .ToList();

            if (validThemes.Count != App.Settings.Prop.CycleEnabledCustomThemes.Count)
            {
                App.Settings.Prop.CycleEnabledCustomThemes = validThemes;
                if (App.Settings.Prop.CycleCurrentIndex >= validThemes.Count)
                    App.Settings.Prop.CycleCurrentIndex = 0;
                App.Settings.Save();
            }

            if (App.Settings.Prop.CycleEnabledCustomThemes.Count == 0)
            {
                App.Settings.Prop.CycleEnabled = false;
                App.Settings.Prop.SelectedCustomTheme = null;
                App.Settings.Save();
                App.Logger.WriteLine("ThemeCycler", "No valid custom themes found – cycling disabled.");
                return;
            }

            int newIndex = (App.Settings.Prop.CycleCurrentIndex + 1) % App.Settings.Prop.CycleEnabledCustomThemes.Count;
            App.Settings.Prop.CycleCurrentIndex = newIndex;
            App.Settings.Prop.SelectedCustomTheme = App.Settings.Prop.CycleEnabledCustomThemes[newIndex];
            App.Settings.Prop.CycleLastCycleTime = DateTime.Now;

            App.Settings.Save();
            App.Logger.WriteLine("ThemeCycler", $"Changed to '{App.Settings.Prop.SelectedCustomTheme}'");
        }
    }
}