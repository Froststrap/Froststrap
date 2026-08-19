using Avalonia.Controls;
using Froststrap.Integrations;
using Avalonia.Controls.ApplicationLifetimes;
using Froststrap.UI.Elements.Dialogs;
using Froststrap.UI.Elements.Onboarding;
using Avalonia;

namespace Froststrap
{
    public static class LaunchHandler
    {
        public static void ProcessNextAction(NextAction action)
        {
            switch (action)
            {
                case NextAction.LaunchSettings:
                    Logger.Info("Opening settings");
                    LaunchSettings();
                    break;

                case NextAction.LaunchRoblox:
                    Logger.Info("Opening Roblox");
                    LaunchRoblox(LaunchMode.Player);
                    break;

                case NextAction.LaunchRobloxStudio:
                    Logger.Info("Opening Roblox Studio");
                    LaunchRoblox(LaunchMode.Studio);
                    break;

                default:
                    Logger.Info("Closing");
                    App.Terminate(ErrorCode.ERROR_SUCCESS);
                    break;
            }
        }

        public static void ProcessLaunchArgs()
        {
            // this order is specific
            if (App.LaunchSettings.OnboardingFlag.Active)
            {
                Logger.Info("Opening uninstaller");
                LaunchOnboarding();
            }
            else if (App.LaunchSettings.MenuFlag.Active)
            {
                Logger.Info("Opening settings");
                LaunchSettings();
            }
            else if (App.LaunchSettings.WatcherFlag.Active)
            {
                Logger.Info("Opening watcher");
                LaunchWatcher();
            }
            else if (App.LaunchSettings.BackgroundUpdaterFlag.Active)
            {
                Logger.Info("Opening background updater");
                LaunchBackgroundUpdater();
            }
            else if (App.LaunchSettings.RobloxLaunchMode != LaunchMode.None)
            {
                Logger.Info($"Opening bootstrapper ({App.LaunchSettings.RobloxLaunchMode})");
                LaunchRoblox(App.LaunchSettings.RobloxLaunchMode);
            }
            else if (App.LaunchSettings.BloxshadeFlag.Active)
            {
                Logger.Info("Opening Bloxshade");
                LaunchBloxshadeConfig();
            }
            else if (!App.LaunchSettings.QuietFlag.Active)
            {
                Logger.Info("Opening menu");
                LaunchMenu();
            }
            else
            {
                Logger.Info("Closing - quiet flag active");
                App.Terminate();
            }
        }

        public static void LaunchSettings()
        {
            var interlock = new InterProcessLock("Settings");

            if (!interlock.IsAcquired)
            {
                interlock.Dispose();
                Logger.Info("Found an already existing menu window");

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
                App.Terminate();
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

            var dialog = new LanguageSelectorDialog();
            if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                desktop.MainWindow = dialog;
            }

            dialog.Closed += (sender, e) =>
            {
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
            };

            dialog.Show();
        }

        public static async void LaunchRoblox(LaunchMode launchMode)
        {
            const string LOG_IDENT = "LaunchHandler::LaunchRoblox";

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

                if (OperatingSystem.IsLinux())
                    Utilities.KillSober();
            }

            // start bootstrapper and show the bootstrapper modal if we're not running silently
            Logger.Info("Initializing bootstrapper");
            App.Bootstrapper = new Bootstrapper(launchMode);
            IBootstrapperDialog? dialog = null;

            if (!App.LaunchSettings.QuietFlag.Active)
            {
                Logger.Info("Initializing bootstrapper dialog");
                ThemeCycler.HandleLaunchCycle();
                dialog = await App.Settings.Prop.BootstrapperStyle.GetNew();
                App.Bootstrapper.Dialog = dialog;
                dialog.Bootstrapper = App.Bootstrapper;
            }

            _ = Task.Run(App.Bootstrapper.Run).ContinueWith(async t =>
            {
                Logger.Info("Bootstrapper task has finished");

                if (t.IsFaulted)
                {
                    Logger.Error("An exception occurred when running the bootstrapper");

                    if (t.Exception is not null)
                        await App.FinalizeExceptionHandling(t.Exception);
                }

                App.Terminate();
            });

            if ((OperatingSystem.IsLinux() || OperatingSystem.IsMacOS()) && !App.LaunchSettings.QuietFlag.Active)
            {
                if (Avalonia.Application.Current?.ApplicationLifetime is
                    Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop)
                    desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;
            }

            dialog?.ShowBootstrapper();

            Logger.Info("Exiting");
        }

        public static void LaunchWatcher()
        {
            // this whole topology is a bit confusing, bear with me:
            // main thread: strictly UI only, handles showing of the notification area icon, context menu, server details dialog
            // - server information task: queries server location, invoked if either the explorer notification is shown or the server details dialog is opened
            // - discord rpc thread: handles rpc connection with discord
            //    - discord rich presence tasks: handles querying and displaying of game information, invoked on activity watcher events
            // - watcher task: runs activity watcher + waiting for roblox to close, terminates when it has

            var watcher = new Watcher();

            Task watcherTask = Task.Run(watcher.Run);

            watcherTask.ContinueWith(async t =>
            {
                Logger.Info("Watcher task has finished");

                watcher.Dispose();

                if (t.IsFaulted)
                {
                    Logger.Error("An exception occurred when running the watcher");

                    if (t.Exception is not null)
                        await App.FinalizeExceptionHandling(t.Exception);
                }

                // Shouldn't this be done after client closes?
                if (App.Settings.Prop.CleanerOptions != CleanerOptions.Never)
                    Cleaner.DoCleaning();

                App.Terminate();
            });
        }

        public static void LaunchBloxshadeConfig()
        {
            Logger.Info("Showing unsupported warning");

            new BloxshadeDialog().Show();
            App.SoftTerminate();
        }

        public static void LaunchBackgroundUpdater()
        {
            // Activate some LaunchFlags we need
            App.LaunchSettings.QuietFlag.Active = true;
            App.LaunchSettings.NoLaunchFlag.Active = true;

            Logger.Info("Initializing bootstrapper");
            App.Bootstrapper = new Bootstrapper(LaunchMode.Player)
            {
                LockName = Bootstrapper.BackgroundUpdaterLockName,
                QuitIfLockExists = true
            };

            CancellationTokenSource cts = new();

            Task.Run(() =>
            {
                Logger.Info("Started event waiter");
                using (EventWaitHandle handle = new(false, EventResetMode.AutoReset, "Froststrap-BackgroundUpdaterKillEvent"))
                    handle.WaitOne();

                Logger.Info("Received close event, killing it all!");
                App.Bootstrapper.Cancel();
            }, cts.Token);

            Task.Run(App.Bootstrapper.Run).ContinueWith(async t =>
            {
                Logger.Info("Bootstrapper task has finished");
                cts.Cancel(); // stop event waiter

                if (t.IsFaulted)
                {
                    Logger.Error("An exception occurred when running the bootstrapper");

                    if (t.Exception is not null)
                        await App.FinalizeExceptionHandling(t.Exception);
                }

                App.Terminate();
            });

            Logger.Info("Exiting");
        }

        private static int _activationInFlight;

        public static void HandleActivationUri(string uri)
        {
            if (!App.LaunchSettings.TryResolveRobloxUri([uri]))
            {
                Logger.Info($"Ignoring unrecognized activation URI: {uri}");
                return;
            }

            if (Interlocked.CompareExchange(ref _activationInFlight, 1, 0) != 0)
            {
                Logger.Info("A launch is already being handled, ignoring activation");
                return;
            }

            var mode = App.LaunchSettings.RobloxLaunchMode;
            Logger.Info($"Handling activation URI as a Roblox launch ({mode})");
            Avalonia.Threading.Dispatcher.UIThread.Post(() => LaunchRoblox(mode));
        }
    }
}
