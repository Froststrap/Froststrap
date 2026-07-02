namespace Froststrap;

public class SoberSettingsManager : JsonManager<Dictionary<string, object>>
{
    private static readonly JsonSerializerOptions _writeOptions = new() { WriteIndented = true };
    private static readonly JsonSerializerOptions _readOptions = new() { ReadCommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true };

    public override string ClassName => nameof(SoberSettingsManager);
    public override string LOG_IDENT_CLASS => ClassName;
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
            App.Logger.WriteLine(LOG_IDENT_CLASS, $"Unknown preset '{presetName}'");
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

        App.Logger.WriteLine(LOG_IDENT_CLASS, $"Unknown preset '{name}'");
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
                int i => i.ToString(),
                long l => l.ToString(),
                double d => d.ToString(CultureInfo.InvariantCulture),
                string s => s,
                _ => val.ToString()
            };
        }
        return null;
    }

    public override bool Load(bool alertFailure = true)
    {
        string LOG_IDENT = $"{LOG_IDENT_CLASS}::Load";

        if (!OperatingSystem.IsLinux())
        {
            App.Logger.WriteLine(LOG_IDENT, "Not on Linux, Sober settings not applicable.");
            Loaded = false;
            Prop = [];
            return false;
        }

        App.Logger.WriteLine(LOG_IDENT, $"Loading from {FileLocation}...");

        if (!File.Exists(FileLocation))
        {
            App.Logger.WriteLine(LOG_IDENT, "Config file does not exist. Sober is not configured.");
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
            App.Logger.WriteLine(LOG_IDENT, "Loaded successfully!");
            return true;
        }
        catch (Exception ex)
        {
            App.Logger.WriteLine(LOG_IDENT, "Failed to load!");
            App.Logger.WriteException(LOG_IDENT, ex);
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

    public override void Save()
    {
        string LOG_IDENT = $"{LOG_IDENT_CLASS}::Save";

        if (!Loaded)
        {
            App.Logger.WriteLine(LOG_IDENT, "Save skipped – settings not loaded (non‑Linux or file missing/invalid).");
            return;
        }

        App.Logger.WriteLine(LOG_IDENT, $"Saving to {FileLocation}...");

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FileLocation)!);
            string contents = JsonSerializer.Serialize(Prop, _writeOptions);
            File.WriteAllText(FileLocation, contents);
            _savedHash = ComputeHash(Prop);
            App.Logger.WriteLine(LOG_IDENT, "Save complete!");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            App.Logger.WriteLine(LOG_IDENT, "Failed to save");
            App.Logger.WriteException(LOG_IDENT, ex);
            _ = Frontend.ShowMessageBox(string.Format(Strings.Bootstrapper_JsonManagerSaveFailed, ClassName, ex.Message), MessageBoxImage.Warning);
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
