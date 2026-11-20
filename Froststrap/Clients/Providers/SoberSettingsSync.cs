// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

using Froststrap.Clients.Contracts;

namespace Froststrap.Clients.Providers
{
    internal sealed class SoberSettingsSync : ISettingsSync
    {
        public bool HasUnsavedChanges => App.SoberSettings.Loaded && App.SoberSettings.HasUnsavedChanges;

        public void Save() => App.SoberSettings.Save();

        public void SyncFastFlags(IReadOnlyDictionary<string, object> flags)
        {
            try
            {
                if (!App.SoberSettings.Loaded)
                    App.SoberSettings.Load(alertFailure: false);

                var fflagsDict = App.SoberSettings.GetOrCreateFFlagsContainer();

                foreach (var kvp in flags)
                {
                    string val = kvp.Value?.ToString() ?? "";
                    if (bool.TryParse(val, out bool boolResult))
                        fflagsDict[kvp.Key] = boolResult;
                    else if (long.TryParse(val, out long longResult))
                        fflagsDict[kvp.Key] = longResult;
                    else if (double.TryParse(val, NumberStyles.Float, CultureInfo.InvariantCulture, out double doubleResult))
                        fflagsDict[kvp.Key] = doubleResult;
                    else
                        fflagsDict[kvp.Key] = val;
                }

                App.SoberSettings.Save();
                App.Logger.Info($"Successfully synced {fflagsDict.Count} flags.");
            }
            catch (Exception ex)
            {
                App.Logger.Error($"Failed to sync flags: {ex}");
            }
        }
    }
}
