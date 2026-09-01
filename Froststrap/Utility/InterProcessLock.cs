namespace Froststrap.Utility
{
    internal class InterProcessLock : IDisposable
    {
        private readonly string _lockFilePath;
        private readonly FileStream? _lockFileStream;
        public bool IsAcquired { get; private set; }

        public InterProcessLock(string name) : this(name, TimeSpan.Zero) { }

        public InterProcessLock(string name, TimeSpan timeout)
        {
            string baseDir = Paths.Base;
            if (string.IsNullOrEmpty(baseDir))
                baseDir = Path.GetTempPath();

            string lockDir = Path.Combine(baseDir, "Locks");
            try
            {
                Directory.CreateDirectory(lockDir);
            }
            catch
            {
                lockDir = Path.Combine(Path.GetTempPath(), "FroststrapLocks");
                Directory.CreateDirectory(lockDir);
            }

            _lockFilePath = Path.Combine(lockDir, $"Froststrap-{name}.lock");

            DateTime start = DateTime.UtcNow;
            while (true)
            {
                try
                {
                    _lockFileStream = File.Open(
                        _lockFilePath,
                        FileMode.Create,
                        FileAccess.ReadWrite,
                        FileShare.None
                    );
                    IsAcquired = true;
                    break;
                }
                catch (IOException) when (timeout > TimeSpan.Zero)
                {
                    if (DateTime.UtcNow - start >= timeout)
                        break;
                    Thread.Sleep(50);
                }
                catch (IOException)
                {
                    break;
                }
                catch
                {
                    break;
                }
            }
        }

        public void Dispose()
        {
            if (IsAcquired)
            {
                _lockFileStream?.Dispose();
                IsAcquired = false;
            }
            GC.SuppressFinalize(this);
        }
    }
}