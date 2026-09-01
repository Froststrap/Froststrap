namespace Froststrap;

internal class AppStorageManager : JsonManager<Dictionary<string, object>>
{
    private static readonly JsonSerializerOptions _writeOptions = new() { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
    private static readonly JsonSerializerOptions _readOptions = new() { ReadCommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true };

    public override string ClassName => nameof(AppStorageManager);
    public override string FileName => "appStorage.json";
    public override string FileLocation => Path.Combine(Paths.Roblox, "LocalStorage", FileName);

    public static readonly IReadOnlyDictionary<string, string> PresetKeys = new Dictionary<string, string>
    {
        { "System.LaunchAtStartup", "LaunchAtStartup" },
        { "System.MinimizeToTray", "MinimizeToTray" },
        { "UI.Theme", "DeviceLevelTheme" },
        { "User.UserId", "UserId" },
    };

    public static IReadOnlyDictionary<Enums.AppStoragePresets.Theme, string> ThemeValues => new Dictionary<Enums.AppStoragePresets.Theme, string>
    {
        { Enums.AppStoragePresets.Theme.Light, "light" },
        { Enums.AppStoragePresets.Theme.Dark, "dark" }
    };

    public void SetValue(string key, object? value)
    {
        if (value is null)
        {
            if (Prop.ContainsKey(key))
                App.Logger.Info($"Deletion of '{key}' pending");
            Prop.Remove(key);
        }
        else
        {
            string newVal = value.ToString()!;
            if (Prop.TryGetValue(key, out object? existing) && existing?.ToString() == newVal)
                return;

            App.Logger.Info($"Setting '{key}' to '{newVal}'");
            Prop[key] = newVal;
        }
    }

    public void SetPreset(string friendlyName, object? value)
    {
        if (!PresetKeys.TryGetValue(friendlyName, out string? actualKey))
        {
            App.Logger.Error($"Unknown preset '{friendlyName}'");
            return;
        }
        SetValue(actualKey, value);
    }

    public string? GetPreset(string friendlyName)
    {
        if (!PresetKeys.TryGetValue(friendlyName, out string? actualKey))
        {
            App.Logger.Error($"Unknown preset '{friendlyName}'");
            return null;
        }
        return GetValue(actualKey);
    }

    public void SetRawValue(string key, object? value)
    {
        if (value is null)
        {
            if (Prop.ContainsKey(key))
                App.Logger.Info($"Deletion of '{key}' pending");
            Prop.Remove(key);
        }
        else
        {
            if (Prop.TryGetValue(key, out object? existing) && Equals(existing, value))
                return;
            App.Logger.Info($"Setting '{key}' (raw)");
            Prop[key] = value;
        }
    }

    public string? GetValue(string key) => Prop.TryGetValue(key, out object? val) ? val?.ToString() : null;
    public T? GetRawValue<T>(string key) where T : class => Prop.TryGetValue(key, out object? val) ? val as T : null;

    public void SetBoolPreset(string friendlyName, bool value) => SetPreset(friendlyName, value ? "true" : "false");

    public bool GetBoolPreset(string friendlyName) => string.Equals(GetPreset(friendlyName), "true", StringComparison.OrdinalIgnoreCase);

    // using jsonmanager save messes up  app theme formatting
    public override void Save()
    {
        if (!File.Exists(FileLocation))
        {
            App.Logger.Info("Save skipped – file does not exist.");
            return;
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
        }
        catch (Exception ex)
        {
            App.Logger.Error("Failed to save appStorage.json");
            App.Logger.Error(ex);
        }
    }

    public override bool Load(bool alertFailure = true)
    {
        App.Logger.Info($"Loading from {FileLocation}...");

        if (!File.Exists(FileLocation))
        {
            App.Logger.Error("File does not exist. No storage loaded.");
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
            App.Logger.Error("Failed to load!");
            App.Logger.Error(ex);
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
}
