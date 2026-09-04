namespace Froststrap;

internal class SoberSettingsManager : JsonManager<Dictionary<string, object>>
{
    private static readonly JsonSerializerOptions _writeOptions = new() { WriteIndented = true };
    private static readonly JsonSerializerOptions _readOptions = new() { ReadCommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true };

    public override string ClassName => nameof(SoberSettingsManager);
    public override string FileName => "config.json";
    public override string FileLocation => Path.Combine(Paths.SoberConfig, FileName);

    public static readonly IReadOnlyDictionary<string, string> PresetKeys = new Dictionary<string, string>
    {
        ["AllowGamepadPermission"] = "allow_gamepad_permission",
        ["EnableGamemode"] = "enable_gamemode",
        ["EnableHiDpi"] = "enable_hidpi",
        ["TouchMode"] = "touch_mode",
        ["UseConsoleExperience"] = "use_console_experience",
        ["UseLibsecret"] = "use_libsecret",
        ["UseOpengl"] = "use_opengl",
        ["ServerLocationIndicatorEnabled"] = "server_location_indicator_enabled",
        ["DiscordRpcEnabled"] = "discord_rpc_enabled",
        ["DiscordRpcShowJoinButton"] = "discord_rpc_show_join_button",
        ["CloseOnLeave"] = "close_on_leave",
        ["FFlagsContainer"] = "fflags"
    };

    public void SetPreset(string presetName, object? value)
    {
        if (!PresetKeys.TryGetValue(presetName, out string? actualKey))
        {
            App.Logger.Warn($"Unknown preset '{presetName}'");
            return;
        }

        // Convert string values to appropriate types for Sober config
        object? convertedValue = value switch
        {
            string s when s.Equals("true", StringComparison.OrdinalIgnoreCase) => true,
            string s when s.Equals("false", StringComparison.OrdinalIgnoreCase) => false,
            string s when long.TryParse(s, out long l) => l,
            string s when double.TryParse(s, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double d) => d,
            _ => value
        };

        SetValue(actualKey, convertedValue);
    }

    public string? GetPreset(string name)
    {
        if (PresetKeys.TryGetValue(name, out string? actualKey))
            return GetValue(actualKey);

        App.Logger.Warn($"Unknown preset '{name}'");
        return null;
    }

    public void SetValue(string key, object? value)
    {
        if (value is null)
            Prop.Remove(key);
        else
            Prop[key] = value;
    }

    public string? GetValue(string key)
    {
        if (Prop.TryGetValue(key, out object? val) && val is not null)
        {
            return val switch
            {
                bool b => b ? "true" : "false",
                int i => i.ToString(CultureInfo.InvariantCulture),
                long l => l.ToString(CultureInfo.InvariantCulture),
                double d => d.ToString(CultureInfo.InvariantCulture),
                string s => s,
                _ => val.ToString()
            };
        }
        return null;
    }

    public override bool Load(bool alertFailure = true)
    {
        if (!OperatingSystem.IsLinux())
        {
            App.Logger.Warn("Not on Linux, Sober settings not applicable.");
            Loaded = false;
            Prop = [];
            return false;
        }

        App.Logger.Info($"Loading from {FileLocation}...");

        if (!File.Exists(FileLocation))
        {
            App.Logger.Warn("Config file does not exist. Sober is not configured.");
            Loaded = false;
            Prop = [];
            return false;
        }

        try
        {
            string contents = File.ReadAllText(FileLocation);
            var settings = JsonSerializer.Deserialize<Dictionary<string, object>>(contents, _readOptions)
                           ?? [];
            Prop = settings;
            Loaded = true;
            _savedHash = ComputeHash(Prop);
            App.Logger.Info("Loaded successfully!");
            return true;
        }
        catch (Exception ex)
        {
            App.Logger.Error($"Failed to load! {ex}");
            Loaded = false;
            Prop = [];

            if (alertFailure)
            {
                string message = Strings.JsonManager_SettingsLoadFailed;
                _ = Frontend.ShowMessageBox($"{message}\n\n{ex.Message}", MessageBoxImage.Warning);
            }
            return false;
        }
    }

    public override bool Save()
    {
        if (!HasUnsavedChanges)
        {
            App.Logger.Info("No changes, skipping save.");
            return false;
        }

        if (!Loaded)
        {
            App.Logger.Warn("Save skipped – settings not loaded (non‑Linux or file missing/invalid).");
            return true;
        }

        App.Logger.Info($"Saving to {FileLocation}...");

        try
        {
            string? directory = Path.GetDirectoryName(FileLocation);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            string contents = JsonSerializer.Serialize(Prop, _writeOptions);
            File.WriteAllText(FileLocation, contents);
            _savedHash = ComputeHash(Prop);
            App.Logger.Info("Save Complete!");
            return true;
        }
        catch (Exception ex)
        {
            App.Logger.Error("Failed to save appStorage.json");
            App.Logger.Error(ex);
            return false;
        }
    }

    public Dictionary<string, object> GetOrCreateFFlagsContainer()
    {
        string containerKey = PresetKeys["FFlagsContainer"];

        if (!Prop.TryGetValue(containerKey, out object? obj) || obj is not Dictionary<string, object> dict)
        {
            dict = [];
            Prop[containerKey] = dict;
        }
        return dict;
    }
}
