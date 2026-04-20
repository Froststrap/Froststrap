namespace Froststrap
{
    public class Logger
    {
        private readonly SemaphoreSlim _semaphore = new(1, 1);
        private StreamWriter? _writer;

        public readonly List<string> History = new();
        public bool Initialized = false;
        public bool NoWriteMode = false;
        public string? FileLocation;

        public string AsDocument => String.Join('\n', History);

        private static readonly string HomeVarName = OperatingSystem.IsWindows() ? "%USERPROFILE%" : "$HOME";

        public async void Initialize(bool useTempDir = false)
        {
            const string LOG_IDENT = "Logger::Initialize";

            string directory = useTempDir ? Path.Combine(Paths.TempLogs) : Path.Combine(Paths.Base, "Logs");
            string timestamp = DateTime.UtcNow.ToString("yyyyMMdd'T'HHmmss'Z'");
            string filename = $"{App.ProjectName}_{timestamp}.log";
            string location = Path.Combine(directory, filename);

            WriteLine(LOG_IDENT, $"Initializing at {location}");

            if (Initialized)
            {
                WriteLine(LOG_IDENT, "Failed to initialize because logger is already initialized");
                return;
            }

            Directory.CreateDirectory(directory);

            if (File.Exists(location))
            {
                WriteLine(LOG_IDENT, "Failed to initialize because log file already exists");
                return;
            }

            try
            {
                _writer = new StreamWriter(location, false, Encoding.UTF8)
                {
                    AutoFlush = true
                };
            }
            catch (IOException)
            {
                WriteLine(LOG_IDENT, "Failed to initialize because log file already exists");
                return;
            }
            catch (UnauthorizedAccessException)
            {
                if (NoWriteMode)
                    return;

                WriteLine(LOG_IDENT, $"Failed to initialize because Froststrap cannot write to {directory}");

                await Frontend.ShowMessageBox(
                    String.Format(Strings.Logger_NoWriteMode, directory),
                    MessageBoxImage.Warning,
                    MessageBoxButton.OK
                );

                NoWriteMode = true;

                return;
            }

            Initialized = true;

            if (History.Count > 0) foreach (var line in History) WriteToLog(line);

            WriteLine(LOG_IDENT, "Finished initializing!");

            FileLocation = location;

            // delete older logs if there are more than 15
            if (Paths.Initialized && Directory.Exists(Paths.Logs))
            {
                const int maxLogs = 15;
                FileInfo[] logs = new DirectoryInfo(Paths.Logs).GetFiles();

                if (logs.Length <= maxLogs)
                    return;

                foreach (FileInfo log in logs.OrderByDescending(log => log.LastWriteTimeUtc).Skip(maxLogs))
                {
                    WriteLine(LOG_IDENT, $"Cleaning up old log file '{log.Name}'");

                    try
                    {
                        log.Delete();
                    }
                    catch (Exception ex)
                    {
                        WriteLine(LOG_IDENT, "Failed to delete log!");
                        WriteException(LOG_IDENT, ex);
                    }
                }
            }
        }

        public void WriteLine(string identifier, string context) {
            string timestamp = DateTime.UtcNow.ToString("s") + "Z";
            string line = $"{timestamp} [{identifier}] {context}".Replace(
                Paths.UserProfile,
                HomeVarName,
                StringComparison.InvariantCultureIgnoreCase
            );

            Console.WriteLine(line);
            WriteToLog(line);
            History.Add(line);
        }

        public void WriteException(string identifier, Exception ex)
        {
            Thread.CurrentThread.CurrentUICulture = CultureInfo.InvariantCulture;

            string hresult = "0x" + ex.HResult.ToString("X8");

            WriteLine(identifier, $"({hresult}) {ex}");

            Thread.CurrentThread.CurrentUICulture = Locale.CurrentCulture;
        }

        private async void WriteToLog(string message)
        {
            if (!Initialized) return;

            try
            {
                await _semaphore.WaitAsync();
                await _writer!.WriteLineAsync(message);
            }
            finally
            {
                _semaphore.Release();
            }
        }
    }
}
