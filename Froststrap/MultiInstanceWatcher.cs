namespace Froststrap
{
    internal static class MultiInstanceWatcher
    {
        private static int GetOpenProcessesCount()
        {
            const string LOG_IDENT = "MultiInstanceWatcher::GetOpenProcessesCount";

            try
            {
                string robloxProcess = OperatingSystem.IsMacOS() ? "RobloxPlayer"
                    : OperatingSystem.IsLinux() ? "sober"
                    : "RobloxPlayerBeta";
                string froststrapProcess = "Froststrap";
                int count = Process.GetProcesses().Count(x => x.ProcessName == robloxProcess || x.ProcessName == froststrapProcess);
                count -= 1; // ignore the current process
                return count;
            }
            catch (Exception ex)
            {
                // everything process related can error at any time
                App.Logger.WriteException(LOG_IDENT, ex);
                return -1;
            }
        }

        private static void FireInitialisedEvent()
        {
            using EventWaitHandle initEventHandle = new EventWaitHandle(false, EventResetMode.AutoReset, "Bloxstrap-MultiInstanceWatcherInitialisationFinished");
            initEventHandle.Set();
        }

        public static void Run()
        {
            const string LOG_IDENT = "MultiInstanceWatcher::Run";

            if (OperatingSystem.IsLinux())
            {
                App.Logger.WriteLine(LOG_IDENT, "Skipping singleton mutex on Linux");
                FireInitialisedEvent();
                // Still watch for alive processes so the watcher lifecycle works correctly.
                int count;
                do
                {
                    Thread.Sleep(2500);
                    count = GetOpenProcessesCount();
                }
                while (count == -1 || count > 0);
                App.Logger.WriteLine(LOG_IDENT, "All Roblox related processes have closed, exiting!");
                return;
            }

            // try to get the mutex
            bool acquiredMutex;

            // we only need to check one of the mutexes
            using Mutex mutex = new Mutex(false, "ROBLOX_singletonMutex");

            try
            {
                acquiredMutex = mutex.WaitOne(0);
            }
            catch (AbandonedMutexException)
            {
                acquiredMutex = true;
            }

            if (!acquiredMutex)
            {
                App.Logger.WriteLine(LOG_IDENT, "Client singleton mutex is already acquired");
                FireInitialisedEvent();
                return;
            }

            App.Logger.WriteLine(LOG_IDENT, "Acquired mutex!");
            FireInitialisedEvent();

            // watch for alive processes
            int countWin;
            do
            {
                Thread.Sleep(2500);
                countWin = GetOpenProcessesCount();
            }
            while (countWin == -1 || countWin > 0); // redo if -1 (one of the Process apis failed)

            App.Logger.WriteLine(LOG_IDENT, "All Roblox related processes have closed, exiting!");
        }
    }
}
