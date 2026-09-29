// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

using Avalonia.Controls;
using Avalonia.Threading;
using Froststrap.Integrations;
using Avalonia.Controls.ApplicationLifetimes;
using Froststrap.UI.Elements.Dialogs;
using Froststrap.UI.Elements.Onboarding;
using Avalonia;

namespace Froststrap
{
    internal static class LaunchHandler
    {
        public static void ProcessNextAction(NextAction action)
        {
            switch (action)
            {
                case NextAction.LaunchSettings:
                    App.Logger.Info("Opening settings");
                    LaunchSettings();
                    break;

                case NextAction.LaunchRoblox:
                    App.Logger.Info("Opening Roblox");
                    _ = LaunchRoblox(LaunchMode.Player);
                    break;

                case NextAction.LaunchRobloxStudio:
                    App.Logger.Info("Opening Roblox Studio");
                    _ = LaunchRoblox(LaunchMode.Studio);
                    break;

                default:
                    App.Logger.Info("Closing");
                    App.Terminate(ErrorCode.ERROR_SUCCESS);
                    break;
            }
        }

        public static async Task ProcessLaunchArgs()
        {
            LogUnknownArgs();

            if (App.State.Prop.IsFirstLaunch)
            {
                App.Logger.Info("First launch detected, launching onboarding");
                LaunchOnboarding();
                return;
            }

            if (App.LaunchSettings.SettingsFlag.Active)
            {
                App.Logger.Info("Opening settings");
                LaunchSettings(quitIfAlreadyRunning: true);
            }
            else if (App.LaunchSettings.BackgroundUpdaterFlag.Active)
            {
                App.Logger.Info("Opening background updater");
                await LaunchBackgroundUpdater();
            }
            else if (App.LaunchSettings.RobloxLaunchMode != LaunchMode.None)
            {
                App.Logger.Info($"Opening bootstrapper ({App.LaunchSettings.RobloxLaunchMode})");
                _ = LaunchRoblox(App.LaunchSettings.RobloxLaunchMode);
            }
            else
            {
                App.Logger.Error("No known launch mode was resolved, exiting");
                App.Terminate();
            }
        }

        public static void LogUnknownArgs()
        {
            var unknown = App.LaunchSettings.UnknownArgs;

            if (unknown.Count == 0)
                return;

            App.Logger.Warn($"Ignoring {unknown.Count} unrecognized argument(s): {string.Join(", ", unknown)}");
        }

        public static void LaunchSettings(bool quitIfAlreadyRunning = false)
        {
            if (App.SettingsOpen)
            {
                App.Logger.Info("Settings window is already open, ignoring the request");
                return;
            }

            var interlock = new InterProcessLock("Settings");

            if (!interlock.IsAcquired)
            {
                App.Logger.Info("Found an already existing settings window");

                RequestSettingsActivation();

                if (quitIfAlreadyRunning)
                    App.Terminate();

                return;
            }

            App.SettingsOpen = true;
            var window = new UI.Elements.Settings.MainWindow(false);

            window.Loaded += (s, e) =>
            {
                _ = Task.Run(() => App.PlayerState.Load());
                _ = Task.Run(() => App.StudioState.Load());

                if (App.Settings.Prop.ShowUsingFroststrapRPC && App.FrostRPC == null)
                {
                    App.FrostRPC = new FroststrapRichPresence();
                }
            };

            var activationCts = new CancellationTokenSource();
            CancellationToken activationToken = activationCts.Token;

            window.Closed += (s, e) =>
            {
                activationCts.Cancel();
                activationCts.Dispose();
                interlock.Dispose();
                App.FrostRPC = null;
                ProcessNextAction(window.CloseAction);
            };

            window.Show();

            _ = Task.Run(() => WaitForActivationRequestsAsync(window, activationToken), activationToken);
        }

        private static string ActivationRequestPath => Path.Combine(
            string.IsNullOrEmpty(Paths.Base) ? Path.GetTempPath() : Paths.Base,
            "Locks",
            "Froststrap-SettingsActivate.request");

        private static void RequestSettingsActivation()
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(ActivationRequestPath)!);
                File.WriteAllText(ActivationRequestPath, DateTime.UtcNow.Ticks.ToString());
            }
            catch (Exception ex)
            {
                App.Logger.Error(ex, "Failed to signal the settings activation request");
            }
        }

        private static async Task WaitForActivationRequestsAsync(Window window, CancellationToken token)
        {
            try
            {
                long? lastSeen = ReadActivationRequest();

                while (!token.IsCancellationRequested)
                {
                    await Task.Delay(250, token);

                    long? request = ReadActivationRequest();

                    if (request is null || request == lastSeen)
                        continue;

                    lastSeen = request;
                    App.Logger.Info("Settings activation requested by another process");
                    await Dispatcher.UIThread.InvokeAsync(() => RaiseWindow(window), DispatcherPriority.Normal);
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                App.Logger.Error(ex, "Settings activation listener stopped unexpectedly");
            }
        }

        private static long? ReadActivationRequest()
        {
            try
            {
                return File.Exists(ActivationRequestPath)
                    ? long.Parse(File.ReadAllText(ActivationRequestPath))
                    : null;
            }
            catch
            {
                return null;
            }
        }


        private static void RaiseWindow(Window window)
        {
            if (!window.IsVisible)
            {
                App.Logger.Info("Settings window is not visible, showing it again");
                window.Show();
            }

            if (window.WindowState == Avalonia.Controls.WindowState.Minimized)
                window.WindowState = Avalonia.Controls.WindowState.Normal;

            window.Activate();
        }

        private static LaunchMenuDialog? _launchMenu;
        private static bool _suppressMenuCloseAction;

        public static void LaunchMenu()
        {
            var dialog = new LaunchMenuDialog();

            _launchMenu = dialog;

            dialog.Loaded += (s, e) =>
            {
                if (App.Settings.Prop.ShowUsingFroststrapRPC && App.FrostRPC == null)
                {
                    App.FrostRPC = new FroststrapRichPresence();
                    App.FrostRPC.SetPage("Launch Menu");
                }
            };

            dialog.Closed += (sender, e) =>
            {
                _launchMenu = null;
                App.FrostRPC = null;

                if (_suppressMenuCloseAction)
                    return;

                ProcessNextAction(dialog.CloseAction);
            };

            dialog.Show();
        }

        public static void CloseAutoRaisedMenu()
        {
            var dialog = _launchMenu;

            if (dialog is null)
                return;

            _launchMenu = null;
            _suppressMenuCloseAction = true;

            try
            {
                App.Logger.Info("Closing the auto-raised launch menu for a cold-start activation");
                dialog.Close();
            }
            finally
            {
                _suppressMenuCloseAction = false;
            }
        }

        public static void LaunchOnboarding()
        {
            var mainWindow = new MainWindow();
            if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop2)
                desktop2.MainWindow = mainWindow;

            mainWindow.Loaded += (s, e) =>
            {
                if (App.Settings.Prop.ShowUsingFroststrapRPC && App.FrostRPC == null)
                {
                    App.FrostRPC = new FroststrapRichPresence();
                    App.FrostRPC.SetPage("Onboarding");
                }
            };

            mainWindow.Show();

            mainWindow.Closed += (s, ev) =>
            {
                App.State.Prop.IsFirstLaunch = false;
                App.State.Save();
                App.Settings.Save();
                ProcessNextAction(mainWindow.CloseAction);
            };
        }

        public static async Task LaunchRoblox(LaunchMode launchMode)
        {
            if (launchMode == LaunchMode.None)
                throw new InvalidOperationException("No Roblox launch mode set");

            if (OperatingSystem.IsWindows() && !File.Exists(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "mfplat.dll")))
            {
                await Frontend.ShowMessageBox(Strings.Bootstrapper_WMFNotFound, MessageBoxImage.Error);

                Utility.Threading.ShellExecute("https://support.microsoft.com/en-us/topic/media-feature-pack-list-for-windows-n-editions-c1c6fffa-d052-8338-7a79-a4bb980a700a");

                App.Terminate(ErrorCode.ERROR_FILE_NOT_FOUND);
            }

            if (App.Settings.Prop.ConfirmLaunches && Utility.Processes.IsRobloxRunning() && launchMode == LaunchMode.Player)
            {
                var result = await Frontend.ShowMessageBox(Strings.Bootstrapper_ConfirmLaunch, MessageBoxImage.Warning, MessageBoxButton.YesNo);

                if (result != MessageBoxResult.Yes)
                {
                    App.Terminate();
                    return;
                }
            }

            // start bootstrapper and show the bootstrapper modal if we're not running silently
            App.Logger.Info("Initializing bootstrapper");
            App.Bootstrapper = new Bootstrapper(launchMode);
            IBootstrapperDialog? dialog = null;

            try
            {
                App.Logger.Info("Initializing bootstrapper dialog");
                ThemeCycler.HandleLaunchCycle();
                dialog = await App.Settings.Prop.BootstrapperStyle.GetNew();
                App.Bootstrapper.Dialog = dialog;
                dialog.Bootstrapper = App.Bootstrapper;
            }
            catch (Exception ex)
            {
                App.Logger.Error(ex, "Failed to create the bootstrapper dialog, launching without one");
                App.Bootstrapper.Dialog = null;
                dialog = null;
            }

            _ = Task.Run(App.Bootstrapper.Run).ContinueWith(async t =>
            {
                App.Logger.Info("Bootstrapper task has finished");

                if (t.IsFaulted)
                {
                    App.Logger.Error("An exception occurred when running the bootstrapper");

                    if (t.Exception is not null)
                        await App.FinalizeExceptionHandling(t.Exception);
                }

                WatcherData? watcherData = App.Bootstrapper.PendingWatcherData;

                if (watcherData is null)
                {
                    App.Terminate();
                    return;
                }

                App.Logger.Info("Handing off to the watcher");

                App.Bootstrapper.Dispose();
                App.Bootstrapper = null;

                // the watcher owns this process from here on, so only bail out of it if it refused to start
                bool handedOff = await Dispatcher.UIThread.InvokeAsync(() => LaunchWatcher(watcherData));

                if (!handedOff)
                    App.Terminate();
            }, TaskScheduler.Default);


            try
            {
                dialog?.ShowBootstrapper();
            }
            catch (Exception ex)
            {
                App.Logger.Error(ex, "Failed to show the bootstrapper dialog, continuing without it");
                App.Bootstrapper.Dialog = null;
            }

            App.Logger.Info("Bootstrapper started, this process may hand off to the watcher when it finishes");
        }

        private static Watcher? _activeWatcher;

        public static bool LaunchWatcher(WatcherData data)
        {
            if (Volatile.Read(ref _activeWatcher) is not null)
            {
                App.Logger.Error("A watcher is already running in this process, ignoring the handoff");
                return true;
            }

            var watcher = new Watcher(data);

            if (!watcher.IsActive)
            {
                App.Logger.Error("Another process is already watching Roblox, not starting another watcher");
                watcher.Dispose();
                return false;
            }

            if (Interlocked.CompareExchange(ref _activeWatcher, watcher, null) is not null)
            {
                App.Logger.Error("A watcher is already running in this process, ignoring the handoff");
                watcher.Dispose();
                return true;
            }

            App.Logger.Info("Watcher started");

            Task watcherTask = Task.Run(watcher.Run);

            watcherTask.ContinueWith(async t =>
            {
                App.Logger.Info("Watcher task has finished");

                watcher.Dispose();

                Interlocked.CompareExchange(ref _activeWatcher, null, watcher);

                if (t.IsFaulted)
                {
                    App.Logger.Error("An exception occurred when running the watcher");

                    if (t.Exception is not null)
                        await App.FinalizeExceptionHandling(t.Exception);
                }

                // Shouldn't this be done after client closes?
                if (App.Settings.Prop.CleanerOptions != CleanerOptions.Never)
                    Cleaner.DoCleaning();

                App.Terminate();
            }, TaskScheduler.Default);

            return true;
        }

        public static async Task LaunchBackgroundUpdater()
        {
            App.LaunchSettings.NoLaunchFlag.Active = true;

            App.Logger.Info("Initializing bootstrapper");
            App.Bootstrapper = new Bootstrapper(LaunchMode.Player)
            {
                LockName = Bootstrapper.BackgroundUpdaterLockName,
                QuitIfLockExists = true
            };

            using var cts = new CancellationTokenSource();

            await Task.Run(() =>
            {
                App.Logger.Info("Started event waiter");
                using (EventWaitHandle handle = new(false, EventResetMode.AutoReset, "Froststrap-BackgroundUpdaterKillEvent"))
                    handle.WaitOne();

                App.Logger.Info("Received close event, killing it all!");
                App.Bootstrapper.Cancel();
            }, cts.Token);

            await Task.Run(App.Bootstrapper.Run).ContinueWith(async t =>
            {
                App.Logger.Info("Bootstrapper task has finished");
                await cts.CancelAsync(); // stop event waiter

                if (t.IsFaulted)
                {
                    App.Logger.Error("An exception occurred when running the bootstrapper");

                    if (t.Exception is not null)
                        await App.FinalizeExceptionHandling(t.Exception);
                }

                App.Terminate();
            }, TaskScheduler.Default);

            App.Logger.Info("Exiting");
        }

        private static int _activationInFlight;

        public static void HandleActivationUri(string uri)
        {
            if (!App.LaunchSettings.TryResolveRobloxUri([uri]))
            {
                App.Logger.Info($"Ignoring unrecognized activation URI: {uri}");
                return;
            }

            if (Interlocked.CompareExchange(ref _activationInFlight, 1, 0) != 0)
            {
                App.Logger.Info("A launch is already being handled, ignoring activation");
                return;
            }

            var mode = App.LaunchSettings.RobloxLaunchMode;
            App.Logger.Info($"Handling activation URI as a Roblox launch ({mode})");
            CloseAutoRaisedMenu();
            Avalonia.Threading.Dispatcher.UIThread.Post(() => _ = LaunchRoblox(mode));
        }
    }
}
