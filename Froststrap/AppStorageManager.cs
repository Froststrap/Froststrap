using Froststrap.Models;
using System.Text.Json;

namespace Froststrap
{
    public class AppStorageManager : JsonManager<AppStorageSettings>
    {
        private static readonly JsonSerializerOptions _jsonOptions = new() { WriteIndented = true };
        private static readonly JsonSerializerOptions _jsonReadOptions = new() { ReadCommentHandling = JsonCommentHandling.Skip };

        public override string ClassName => nameof(AppStorageManager);
        public override string LOG_IDENT_CLASS => ClassName;
        public override string FileName => "appStorage.json";
        public override string FileLocation => Path.Combine(Paths.Roblox, "LocalStorage", FileName);

        public enum AppStorageSettingTheme
        {
            Light,
            Dark
        }

        public AppStorageManager() : base("AppStorage") { }

        private static AppStorageManager Instance => App.StorageSettings;

        /// <summary>
        /// Gets a value from app storage
        /// </summary>
        public static string? GetValue(string key)
        {
            var property = typeof(AppStorageSettings).GetProperty(key);
            if (property != null)
            {
                var value = property.GetValue(Instance.Prop)?.ToString();
                return string.IsNullOrEmpty(value) ? null : value;
            }

            App.Logger.WriteLine(Instance.LOG_IDENT_CLASS, $"Property '{key}' not found");
            return null;
        }

        /// <summary>
        /// Sets a value in app storage
        /// </summary>
        public static void SetValue(string key, string? value)
        {
            const string LOG_IDENT = "AppStorageManager::SetValue";

            var property = typeof(AppStorageSettings).GetProperty(key);
            if (property == null)
            {
                App.Logger.WriteLine(LOG_IDENT, $"Property '{key}' not found");
                return;
            }

            var currentValue = property.GetValue(Instance.Prop)?.ToString();

            if (value == null)
            {
                if (!string.IsNullOrEmpty(currentValue))
                {
                    App.Logger.WriteLine(LOG_IDENT, $"Setting '{key}' to null is pending");
                    property.SetValue(Instance.Prop, "");
                }
            }
            else
            {
                if (currentValue == value)
                    return;

                App.Logger.WriteLine(LOG_IDENT, $"Changing of '{key}' from '{currentValue}' to '{value}' is pending");
                property.SetValue(Instance.Prop, value);
            }
        }

        public override void Save()
        {
            string LOG_IDENT = $"{nameof(AppStorageManager)}::Save";
            App.Logger.WriteLine(LOG_IDENT, $"Saving to {Instance.FileLocation}...");

            try
            {
                string? directory = Path.GetDirectoryName(Instance.FileLocation);
                if (!string.IsNullOrEmpty(directory))
                    Directory.CreateDirectory(directory);

                string contents = JsonSerializer.Serialize(Instance.Prop, _jsonOptions);
                File.WriteAllText(Instance.FileLocation, contents);

                App.Logger.WriteLine(LOG_IDENT, "Save Complete!");
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine(LOG_IDENT, "Failed to save appStorage.json");
                App.Logger.WriteException(LOG_IDENT, ex);
            }
        }

        public override bool Load(bool alertFailure = true)
        {
            string LOG_IDENT = $"{nameof(AppStorageManager)}::Load";
            App.Logger.WriteLine(LOG_IDENT, $"Loading from {Instance.FileLocation}...");

            try
            {
                if (File.Exists(Instance.FileLocation))
                {
                    string contents = File.ReadAllText(Instance.FileLocation);

                    Instance._prop = JsonSerializer.Deserialize<AppStorageSettings>(contents, _jsonReadOptions)
                            ?? new AppStorageSettings();

                    Instance.Loaded = true;

                    App.Logger.WriteLine(LOG_IDENT, "Loaded successfully!");
                    return true;
                }

                App.Logger.WriteLine(LOG_IDENT, "appStorage.json does not exist. Roblox may not be installed.");
                Instance.Loaded = false;
                return false;
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine(LOG_IDENT, "Error loading appStorage.json");
                App.Logger.WriteException(LOG_IDENT, ex);

                if (alertFailure)
                    _ = Frontend.ShowMessageBox($"Failed to load appStorage.json: {ex.Message}", MessageBoxImage.Error);

                return false;
            }
        }

        public static void SetBoolValue(string key, bool value) => SetValue(key, value.ToString().ToLower());
        public static bool GetBoolValue(string key) => GetValue(key)?.ToLower() == "true";

        public static void SetTheme(AppStorageSettingTheme theme)
        {
            string userId = Instance.Prop.UserId ?? "0";
            string themeValue = theme == AppStorageSettingTheme.Dark ? "dark" : "light";
            string themeJson = $"{{\"{userId}\":\"{themeValue}\"}}";
            SetValue("DeviceLevelTheme", themeJson);
        }

        public static AppStorageSettingTheme GetTheme()
        {
            var json = Instance.Prop.DeviceLevelTheme;
            if (string.IsNullOrEmpty(json))
                return AppStorageSettingTheme.Dark;
            return json.Contains("dark") ? AppStorageSettingTheme.Dark : AppStorageSettingTheme.Light;
        }
    }
}