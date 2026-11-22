// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

using Froststrap.Clients.Contracts;

namespace Froststrap.Clients.Providers;

internal sealed class SoberSettingsSync : ISettingsSync
{
    private readonly SoberSettingsManager _settings = new();

    public bool Loaded => _settings.Loaded;

    public string? GetPreset(string name) => _settings.GetPreset(name);

    public void SetPreset(string name, object? value) => _settings.SetPreset(name, value);

    public bool HasUnsavedChanges => _settings.Loaded && _settings.HasUnsavedChanges;

    public void Load() => _settings.Load(alertFailure: false);

    public void Save() => _settings.Save();

    public void SyncFastFlags(IReadOnlyDictionary<string, object> flags)
    {
        try
        {
            if (!_settings.Loaded)
                _settings.Load(alertFailure: false);

            var fflagsDict = _settings.GetOrCreateFFlagsContainer();

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

            _settings.Save();
            App.Logger.Info($"Successfully synced {fflagsDict.Count} flags.");
        }
        catch (Exception ex)
        {
            App.Logger.Error($"Failed to sync flags: {ex}");
        }
    }
}
