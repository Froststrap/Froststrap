using System.Runtime.InteropServices;

namespace Froststrap.Utility
{
    public class InterProcessLock : IDisposable
    {
        private readonly string _lockName;
        private Mutex? _windowsMutex;
        private FileStream? _unixLockFile;

        public bool IsAcquired { get; private set; }

        public InterProcessLock(string name) : this(name, TimeSpan.Zero) { }

        public InterProcessLock(string name, TimeSpan timeout)
        {
            _lockName = "Froststrap-" + name;

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                _windowsMutex = new Mutex(false, _lockName);
                try
                {
                    IsAcquired = _windowsMutex.WaitOne(timeout);
                }
                catch (AbandonedMutexException)
                {
                    IsAcquired = true;
                }
            }
            else
            {
                // Use file-based locking on macOS/Linux
                try
                {
                    string lockDir = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                        "Froststrap"
                    );
                    Directory.CreateDirectory(lockDir);

                    string lockFile = Path.Combine(lockDir, $"{_lockName}.lock");

                    // Try to open with exclusive access
                    _unixLockFile = File.Open(
                        lockFile,
                        FileMode.Create,
                        FileAccess.ReadWrite,
                        FileShare.None
                    );

                    IsAcquired = true;
                }
                catch (IOException)
                {
                    // Lock file is already in use
                    IsAcquired = false;
                }
            }
        }

        public void Dispose()
        {
            if (IsAcquired)
            {
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    _windowsMutex?.ReleaseMutex();
                }
                else
                {
                    _unixLockFile?.Dispose();
                }

                IsAcquired = false;
            }

            GC.SuppressFinalize(this);
        }
    }
}
