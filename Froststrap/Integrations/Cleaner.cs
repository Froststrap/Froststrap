namespace Froststrap.Integrations
{
    public class Cleaner
    {
        private const int MaxFiles = 200;

        public static readonly IReadOnlyDictionary<string, string?> Directories = new Dictionary<string, string?>
        {
            { "FroststrapLogs", Paths.Logs },
            { "FroststrapCache", Paths.Downloads },
            { "RobloxLogs", Paths.RobloxLogs },
            { "RobloxCache", Paths.RobloxCache }
        };

        public static void DoCleaning()
        {
            const string LOG_IDENT = "Cleaner::DoCleaning";

            App.Logger.WriteLine(LOG_IDENT, "Cleaner has started");

            var maxFileAge = App.Settings.Prop.CleanerOptions switch
            {
                CleanerOptions.OneDay => 1,
                CleanerOptions.OneWeek => 7,
                CleanerOptions.OneMonth => 30,
                CleanerOptions.TwoMonths => 60,
                CleanerOptions.Never => int.MaxValue,
                _ => int.MaxValue,
            };

            var threshold = DateTime.Now.AddHours(-maxFileAge);

            foreach (var directory in Directories)
            {
                string? folder = directory.Value;
                string type = directory.Key;

                int deletedItems = 0;

                if (!App.Settings.Prop.CleanerDirectories.Contains(type))
                {
                    App.Logger.WriteLine(LOG_IDENT, $"Skipping {type}");
                    continue;
                }

                if (string.IsNullOrEmpty(folder) || !Directory.Exists(folder))
                    continue;

                try
                {
                    string[] files = RecursivlyGetFiles(folder);

                    App.Logger.WriteLine(LOG_IDENT, $"Running cleaner in {type}, {files.Length} files found");

                    foreach (string file in files)
                    {
                        if (!VerifyFile(file, threshold))
                            continue;

                        if (deletedItems >= MaxFiles)
                        {
                            App.Logger.WriteLine(LOG_IDENT, $"Reached file threshold in {type}, continuing to next directory");
                            break;
                        }

                        try
                        {
                            File.Delete(file);
                            deletedItems++;
                        }
                        catch (Exception ex)
                        {
                            App.Logger.WriteLine(LOG_IDENT, $"Unable to delete {file}");
                            App.Logger.WriteException(LOG_IDENT, ex);
                        }
                    }
                }
                catch (Exception ex)
                {
                    App.Logger.WriteLine(LOG_IDENT, $"Failed to clean up {folder}");
                    App.Logger.WriteException(LOG_IDENT, ex);
                }
            }

            App.Logger.WriteLine(LOG_IDENT, "Cleaner finished");
        }

        private static bool VerifyFile(string file, DateTime threshold)
        {
            if (!File.Exists(file))
                return false;

            if (File.GetCreationTime(file) > threshold)
                return false;

            if (!file.Contains("Roblox") && !file.Contains(App.ProjectName) && !file.Contains(Paths.Base))
                throw new Exception($"{file} was in disallowed directory");

            if (file.Contains("Windows"))
                throw new Exception($"{file} was in Windows directory");

            return true;
        }

        private static string[] RecursivlyGetFiles(string folder)
        {
            if (string.IsNullOrEmpty(folder) || !Directory.Exists(folder))
                throw new Exception("Folder was not found");

            return [.. Directory.EnumerateFiles(folder, "*.*", SearchOption.AllDirectories)];
        }
    }
}