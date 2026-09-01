using Avalonia.Controls;
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
                    LaunchRoblox(LaunchMode.Player);
                    break;

                case NextAction.LaunchRobloxStudio:
                    App.Logger.Info("Opening Roblox Studio");
                    LaunchRoblox(LaunchMode.Studio);
                    break;

                default:
                    App.Logger.Info("Closing");
                    App.Terminate(ErrorCode.ERROR_SUCCESS);
                    break;
            }
        }

        public static async Task ProcessLaunchArgs()
        {
            // this order is specific
            if (App.LaunchSettings.OnboardingFlag.Active)
            {
                App.Logger.Info("Opening onboarding");
                LaunchOnboarding();
            }
            else if (App.LaunchSettings.MenuFlag.Active)
            {
                App.Logger.Info("Opening settings");
                LaunchSettings();
            }
            else if (App.LaunchSettings.WatcherFlag.Active)
            {
                App.Logger.Info("Opening watcher");
                LaunchWatcher();
            }
            else if (App.LaunchSettings.BackgroundUpdaterFlag.Active)
            {
                App.Logger.Info("Opening background updater");
                await LaunchBackgroundUpdater();
            }
            else if (App.LaunchSettings.RobloxLaunchMode != LaunchMode.None)
            {
                App.Logger.Info($"Opening bootstrapper ({App.LaunchSettings.RobloxLaunchMode})");
                LaunchRoblox(App.LaunchSettings.RobloxLaunchMode);
            }
            else if (App.LaunchSettings.BloxshadeFlag.Active)
            {
                App.Logger.Info("Opening Bloxshade");
                LaunchBloxshadeConfig();
            }
            else if (!App.LaunchSettings.QuietFlag.Active)
            {
                App.Logger.Info("Opening menu");
                LaunchMenu();
            }
            else
            {
                App.Logger.Info("Closing - quiet flag active");
                App.Terminate();
            }
        }

        public static void LaunchSettings()
        {
            using var interlock = new InterProcessLock("Settings");

            if (!interlock.IsAcquired)
            {
                interlock.Dispose();
                App.Logger.Info("Found an already existing menu window");

                using var activateEvent = new EventWaitHandle(false, EventResetMode.AutoReset, "Froststrap-ActivateSettingsEvent");
                activateEvent.Set();

                App.Terminate();
                return;
            }

            if (!App.PlayerState.Loaded)
                _ = App.PlayerState.Load();
            if (!App.StudioState.Loaded)
                _ = App.StudioState.Load();

            if (App.Settings.Prop.ShowUsingFroststrapRPC && App.FrostRPC == null)
            {
                App.FrostRPC = new FroststrapRichPresence();
            }

            var window = new UI.Elements.Settings.MainWindow(false);
            App.FrostRPC?.SetPage("Settings");

            window.Closed += (s, e) =>
            {
                interlock.Dispose();
                App.FrostRPC?.Dispose();
                App.FrostRPC = null;
                ProcessNextAction(window.CloseAction);
            };

            window.Show();
        }

        public static void LaunchMenu()
        {
            if (App.Settings.Prop.ShowUsingFroststrapRPC && App.FrostRPC == null)
            {
                App.FrostRPC = new FroststrapRichPresence();
            }

            var dialog = new LaunchMenuDialog();
            App.FrostRPC?.SetPage("Launch Menu");

            dialog.Closed += (sender, e) =>
            {
                App.FrostRPC?.Dispose();
                App.FrostRPC = null;
                ProcessNextAction(dialog.CloseAction);
            };

            dialog.Show();
        }

        public static void LaunchOnboarding()
        {
            if (App.Settings.Prop.ShowUsingFroststrapRPC && App.FrostRPC == null)
            {
                App.FrostRPC = new FroststrapRichPresence();
            }

            App.FrostRPC?.SetPage("Onboarding");

            var mainWindow = new MainWindow();
            if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop2)
            {
                desktop2.MainWindow = mainWindow;
            }
            mainWindow.Show();

            mainWindow.Closed += (s, ev) =>
            {
                if (App.State.Prop.IsFirstLaunch)
                {
                    App.State.Prop.IsFirstLaunch = false;
                    App.State.Save();
                }
                ProcessNextAction(mainWindow.CloseAction);
            };
        }

        public static async void LaunchRoblox(LaunchMode launchMode)
        {
            if (launchMode == LaunchMode.None)
                throw new InvalidOperationException("No Roblox launch mode set");

            if (OperatingSystem.IsWindows() && !File.Exists(Path.Combine(Paths.System, "mfplat.dll")))
            {
                await Frontend.ShowMessageBox(Strings.Bootstrapper_WMFNotFound, MessageBoxImage.Error);

                if (!App.LaunchSettings.QuietFlag.Active)
                    Utilities.ShellExecute("https://support.microsoft.com/en-us/topic/media-feature-pack-list-for-windows-n-editions-c1c6fffa-d052-8338-7a79-a4bb980a700a");

                App.Terminate(ErrorCode.ERROR_FILE_NOT_FOUND);
            }

            if (App.Settings.Prop.ConfirmLaunches && Utilities.IsRobloxRunning() && launchMode == LaunchMode.Player)
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

            if (!App.LaunchSettings.QuietFlag.Active)
            {
                App.Logger.Info("Initializing bootstrapper dialog");
                ThemeCycler.HandleLaunchCycle();
                dialog = await App.Settings.Prop.BootstrapperStyle.GetNew();
                App.Bootstrapper.Dialog = dialog;
                dialog.Bootstrapper = App.Bootstrapper;
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

                App.Terminate();
            },TaskScheduler.Default);

            if ((OperatingSystem.IsLinux() || OperatingSystem.IsMacOS()) && !App.LaunchSettings.QuietFlag.Active)
            {
                if (Avalonia.Application.Current?.ApplicationLifetime is
                    Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop)
                    desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;
            }

            dialog?.ShowBootstrapper();

            App.Logger.Info("Exiting");
        }

        public static void LaunchWatcher()
        {
            // this whole topology is a bit confusing, bear with me:
            // main thread: strictly UI only, handles showing of the notification area icon, context menu, server details dialog
            // - server information task: queries server location, invoked if either the explorer notification is shown or the server details dialog is opened
            // - discord rpc thread: handles rpc connection with discord
            //    - discord rich presence tasks: handles querying and displaying of game information, invoked on activity watcher events
            // - watcher task: runs activity watcher + waiting for roblox to close, terminates when it has

            using var watcher = new Watcher();

            Task watcherTask = Task.Run(watcher.Run);

            watcherTask.ContinueWith(async t =>
            {
                App.Logger.Info("Watcher task has finished");

                watcher.Dispose();

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
        }

        public static void LaunchBloxshadeConfig()
        {
            App.Logger.Info("Showing unsupported warning");

            new BloxshadeDialog().Show();
            App.SoftTerminate();
        }

        public static async Task LaunchBackgroundUpdater()
        {
            // Activate some LaunchFlags we need
            App.LaunchSettings.QuietFlag.Active = true;
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
            Avalonia.Threading.Dispatcher.UIThread.Post(() => LaunchRoblox(mode));
        }
    }
}
