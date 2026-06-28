using System.Text.Json.Nodes;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;

namespace Froststrap
{
    public class JsonManager<T>(string? className = null) where T : class, new()
    {
        private static readonly JsonSerializerOptions _jsonOptions = new()
        {
            WriteIndented = true,
            ReadCommentHandling = JsonCommentHandling.Skip
        };

        protected T _prop = new();

        public virtual T Prop
        {
            get => _prop;
            set => _prop = value;
        }

        public string? LastFileHash { get; private set; }

        public bool Loaded { get; protected set; } = false;

        public virtual string ClassName { get; } = string.IsNullOrEmpty(className) ? typeof(T).Name : className;

        public virtual string ProfilesLocation => Path.Combine(Paths.Base, "Profiles.json");

        public virtual string FileName => $"{ClassName}.json";

        public virtual string FileLocation => Path.Combine(Paths.Base, FileName);

        public bool IsSaved => File.Exists(FileLocation);

        public virtual string LOG_IDENT_CLASS => $"JsonManager<{ClassName}>";

        public string? _savedHash;

        protected virtual string ComputeHash(T obj)
        {
            string json = JsonSerializer.Serialize(obj, _jsonOptions);
            return MD5Hash.FromString(json);
        }

        public bool HasUnsavedChanges
        {
            get
            {
                if (_savedHash == null) return false;
                return ComputeHash(Prop) != _savedHash;
            }
        }

        public virtual bool Load(bool alertFailure = true)
        {
            string LOG_IDENT = $"{LOG_IDENT_CLASS}::Load";

            App.Logger.WriteLine(LOG_IDENT, $"Loading from {FileLocation}...");

            try
            {
                if (File.Exists(FileLocation))
                {
                    string contents = File.ReadAllText(FileLocation);

                    T settings = JsonSerializer.Deserialize<T>(contents, _jsonOptions)
                        ?? throw new InvalidOperationException($"{ClassName} deserialization returned null.");

                    _prop = settings;
                    Loaded = true;
                    LastFileHash = MD5Hash.FromString(contents);
                    _savedHash = ComputeHash(_prop);

                    App.Logger.WriteLine(LOG_IDENT, "Loaded successfully!");

                    return true;
                }
                else
                {
                    App.Logger.WriteLine(LOG_IDENT, $"Could not find {FileLocation}.");
                    Loaded = true;

                    _savedHash = ComputeHash(_prop);
                    return false;
                }
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine(LOG_IDENT, "Failed to load!");
                App.Logger.WriteException(LOG_IDENT, ex);

                if (alertFailure)
                {
                    string message = ClassName switch
                    {
                        nameof(Settings) => Strings.JsonManager_SettingsLoadFailed,
                        nameof(FastFlagManager) => Strings.JsonManager_FastFlagsLoadFailed,
                        _ => string.Empty
                    };

                    if (!string.IsNullOrEmpty(message))
                        _ = Frontend.ShowMessageBox($"{message}\n\n{ex.Message}", MessageBoxImage.Warning);

                    try
                    {
                        if (File.Exists(FileLocation))
                            File.Copy(FileLocation, FileLocation + ".bak", true);
                    }
                    catch (Exception copyEx)
                    {
                        App.Logger.WriteLine(LOG_IDENT, $"Failed to create backup file: {FileLocation}.bak");
                        App.Logger.WriteException(LOG_IDENT, copyEx);
                    }
                }

                Loaded = true;
                Save();

                return false;
            }
        }

        public virtual async void Save()
        {
            string LOG_IDENT = $"{LOG_IDENT_CLASS}::Save";

            App.Logger.WriteLine(LOG_IDENT, $"Saving to {FileLocation}...");

            Directory.CreateDirectory(Path.GetDirectoryName(FileLocation)!);

            try
            {
                string contents = JsonSerializer.Serialize(Prop, _jsonOptions);

                File.WriteAllText(FileLocation, contents);

                LastFileHash = MD5Hash.FromString(contents);
                _savedHash = ComputeHash(Prop);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                App.Logger.WriteLine(LOG_IDENT, "Failed to save");
                App.Logger.WriteException(LOG_IDENT, ex);

                string errorMessage = string.Format(Strings.Bootstrapper_JsonManagerSaveFailed, ClassName, ex.Message);
                await Frontend.ShowMessageBox(errorMessage, MessageBoxImage.Warning);

                return;
            }

            App.Logger.WriteLine(LOG_IDENT, "Save complete!");
        }

        public virtual void SaveSetting(string SettingName)
        {
            if (string.IsNullOrEmpty(SettingName))
            {
                Save();
                return;
            }

            string LOG_IDENT = $"{LOG_IDENT_CLASS}::SaveSetting";
            App.Logger.WriteLine(LOG_IDENT, $"Saving setting '{SettingName}' to {FileLocation}");

            Directory.CreateDirectory(Path.GetDirectoryName(FileLocation)!);

            try
            {
                JsonObject existingJson = [];
                if (File.Exists(FileLocation))
                {
                    string existingContent = File.ReadAllText(FileLocation);
                    if (!string.IsNullOrWhiteSpace(existingContent))
                    {
                        using var doc = JsonDocument.Parse(existingContent);
                        existingJson = JsonSerializer.SerializeToNode(doc.RootElement)?.AsObject() ?? [];
                    }
                }

                var currentJson = JsonSerializer.SerializeToNode(Prop, _jsonOptions)?.AsObject();
                 _ = currentJson ?? throw new InvalidOperationException("Failed to serialize current object.");

                if (currentJson.TryGetPropertyValue(SettingName, out JsonNode? value))
                {
                    existingJson[SettingName] = value?.DeepClone();
                    App.Logger.WriteLine(LOG_IDENT, $"Updated Setting '{SettingName}'");
                }
                else
                {
                    App.Logger.WriteLine(LOG_IDENT, $"Setting '{SettingName}' not found – aborting save.");
                    return;
                }

                string contents = existingJson.ToJsonString(_jsonOptions);
                File.WriteAllText(FileLocation, contents);
                LastFileHash = MD5Hash.FromString(contents);
                _savedHash = ComputeHash(Prop);
                App.Logger.WriteLine(LOG_IDENT, "SaveSetting complete!");
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
            {
                App.Logger.WriteLine(LOG_IDENT, "Failed to save Setting");
                App.Logger.WriteException(LOG_IDENT, ex);
            }
        }

        public virtual void Delete()
        {
            string LOG_IDENT = $"{LOG_IDENT_CLASS}::Delete";

            try
            {
                if (File.Exists(FileLocation))
                {
                    File.Delete(FileLocation);

                    Loaded = false;
                    App.Logger.WriteLine(LOG_IDENT, "Delete complete!");
                }
                else
                {
                    App.Logger.WriteLine(LOG_IDENT, "File does not exist on disk");
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                App.Logger.WriteLine(LOG_IDENT, "Failed to delete");
                App.Logger.WriteException(LOG_IDENT, ex);

                // should we notify?
            }
        }

        public async void SaveProfile(string name)
        {
            const string LOGGER_STRING = "SaveProfile::Profiles";
            string BaseDir = Paths.SavedFlagProfiles;

            try
            {
                if (string.IsNullOrEmpty(name))
                    return;

                string FileDirectory = Path.Combine(BaseDir, name);

                if (!Directory.Exists(BaseDir))
                    Directory.CreateDirectory(BaseDir);

                App.Logger.WriteLine(LOGGER_STRING, $"Writing flag profile {name}");

                if (!File.Exists(FileDirectory))
                    File.Create(FileDirectory).Dispose();

                string FastFlagsJson = JsonSerializer.Serialize(Prop, _jsonOptions);

                File.WriteAllText(FileDirectory, FastFlagsJson);
            }
            catch (Exception ex)
            {
                await Frontend.ShowMessageBox(ex.Message, MessageBoxImage.Error);
            }
        }

        public async void LoadProfile(string? name, bool? clearFlags)
        {
            const string LOGGER_STRING = "LoadProfile::Profiles";
            string BaseDir = Paths.SavedFlagProfiles;

            if (string.IsNullOrEmpty(name))
                return;

            try
            {
                if (!Directory.Exists(BaseDir))
                    Directory.CreateDirectory(BaseDir);

                string[] Files = Directory.GetFiles(BaseDir);
                string FoundFile = Files.FirstOrDefault(f => Path.GetFileName(f) == name) ?? string.Empty;

                if (string.IsNullOrEmpty(FoundFile))
                    return;

                string SavedClientSettings = File.ReadAllText(FoundFile);

                App.Logger.WriteLine(LOGGER_STRING, $"Loading {SavedClientSettings}");

                T settings = JsonSerializer.Deserialize<T>(SavedClientSettings)
                    ?? throw new JsonException($"Failed to deserialize profile: {name}");

                App.FastFlags.suspendUndoSnapshot = true;
                App.FastFlags.SaveUndoSnapshot();

                if (clearFlags == true)
                {
                    Prop = settings;
                }
                else
                {
                    if (settings is IDictionary<string, object> settingsDict && Prop is IDictionary<string, object> propDict)
                    {
                        foreach (var kvp in settingsDict)
                        {
                            if (kvp.Value != null)
                                propDict[kvp.Key] = kvp.Value;
                        }
                    }
                }

                App.FastFlags.suspendUndoSnapshot = false;
                App.FastFlags.Save();
            }
            catch (Exception ex)
            {
                await Frontend.ShowMessageBox(ex.Message, MessageBoxImage.Error);
            }
        }

        public async void LoadPresetProfile(string? name, bool? clearFlags)
        {
            const string LOGGER_STRING = "LoadProfile::Profiles";

            if (string.IsNullOrEmpty(name))
                return;

            try
            {
                string profileJson;
                var assembly = Assembly.GetExecutingAssembly();
                string resourcePrefix = "Froststrap.Resources.PresetFlags.";
                string resourceFullName = resourcePrefix + name;

                string? foundResource = assembly.GetManifestResourceNames()
                    .FirstOrDefault(r => r.Equals(resourceFullName, StringComparison.OrdinalIgnoreCase));

                if (foundResource != null)
                {
                    using Stream stream = assembly.GetManifestResourceStream(foundResource)!;
                    using StreamReader reader = new(stream);
                    profileJson = reader.ReadToEnd();

                    App.Logger.WriteLine(LOGGER_STRING, $"Loading embedded preset profile {name}");
                }
                else
                {
                    // Load from disk (user profiles)
                    string BaseDir = Paths.SavedFlagProfiles;

                    if (!Directory.Exists(BaseDir))
                        Directory.CreateDirectory(BaseDir);

                    string[] Files = Directory.GetFiles(BaseDir);
                    string FoundFile = Files.FirstOrDefault(f => Path.GetFileName(f) == name) ?? string.Empty;

                    if (string.IsNullOrEmpty(FoundFile))
                        throw new FileNotFoundException($"Profile file '{name}' not found.");

                    profileJson = File.ReadAllText(FoundFile);

                    App.Logger.WriteLine(LOGGER_STRING, $"Loading user profile from file {name}");
                }

                // Deserialize the profile JSON
                T settings = JsonSerializer.Deserialize<T>(profileJson)
                    ?? throw new ArgumentNullException(nameof(name), "Deserialization returned null");

                App.FastFlags.suspendUndoSnapshot = true;
                App.FastFlags.SaveUndoSnapshot();

                if (clearFlags == true)
                {
                    Prop = settings;
                }
                else
                {
                    if (settings is IDictionary<string, object> settingsDict && Prop is IDictionary<string, object> propDict)
                    {
                        foreach (var kvp in settingsDict)
                        {
                            if (kvp.Value != null)
                                propDict[kvp.Key] = kvp.Value;
                        }
                    }
                }

                App.FastFlags.suspendUndoSnapshot = false;

                App.FastFlags.Save();
            }
            catch (Exception ex)
            {
                await Frontend.ShowMessageBox(ex.Message, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Is the file on disk different to the one deserialised during this session?
        /// </summary>
        public bool HasFileOnDiskChanged()
        {
            // file was deleted after being loaded
            if (!File.Exists(FileLocation))
                return !string.IsNullOrEmpty(LastFileHash);

            // check if a file has been created since launch
            if (string.IsNullOrEmpty(LastFileHash) && File.Exists(FileLocation))
                return true;

            return LastFileHash != MD5Hash.FromFile(FileLocation);
        }
    }

    /// <summary>
    /// <see cref="JsonManager{T}"/> that will automatically load in the JSON if it has not been already
    /// </summary>
    /// <typeparam name="T">Class</typeparam>
    public class LazyJsonManager<T>(string? className) : JsonManager<T>(className) where T : class, new()
    {
        public override T Prop
        {
            get
            {
                if (!Loaded)
                    Load();

                return _prop;
            }
            set
            {
                _prop = value;
                Loaded = true;
            }
        }
    }
}