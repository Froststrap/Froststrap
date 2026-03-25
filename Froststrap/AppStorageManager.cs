namespace Froststrap
{
    public class AppStorageManager : JsonManager<AppStorageSettings>
    {
        public AppStorageManager() : base("AppStorage") { }

        public override string FileLocation => Path.Combine(Paths.Roblox, "LocalStorage", "appStorage.json");

        public override bool Load(bool alertFailure = true)
        {
            string LOG_IDENT = $"{LOG_IDENT_CLASS}::Load";
            App.Logger.WriteLine(LOG_IDENT, $"Loading from {FileLocation}...");

            try
            {
                if (File.Exists(FileLocation))
                {
                    string contents = File.ReadAllText(FileLocation);

                    AppStorageSettings? settings = JsonSerializer.Deserialize<AppStorageSettings>(contents);

                    if (settings is null)
                        throw new Exception("Failed to deserialize appStorage.json");

                    _prop = settings;
                    Loaded = true;

                    App.Logger.WriteLine(LOG_IDENT, "Loaded successfully!");

                    return true;
                }

                App.Logger.WriteLine(LOG_IDENT, "appStorage.json does not exist. Roblox may not be installed.");
                return false;
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine(LOG_IDENT, "Error loading appStorage.json");
                App.Logger.WriteException(LOG_IDENT, ex);
                return false;
            }
        }

        public override void Save()
        {
            string LOG_IDENT = $"{LOG_IDENT_CLASS}::Save";
            App.Logger.WriteLine(LOG_IDENT, $"Saving to {FileLocation}...");

            try
            {
                string? directory = Path.GetDirectoryName(FileLocation);
                if (!string.IsNullOrEmpty(directory))
                    Directory.CreateDirectory(directory);

                string contents = JsonSerializer.Serialize(Prop);

                File.WriteAllText(FileLocation, contents);
                App.Logger.WriteLine(LOG_IDENT, "Save Complete!");
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine(LOG_IDENT, "Failed to save appStorage.json");
                App.Logger.WriteException(LOG_IDENT, ex);
            }
        }
    }
}