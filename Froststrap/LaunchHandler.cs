using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Froststrap.Integrations;
using Froststrap.UI.Elements.Dialogs;

namespace Froststrap
{
    public static class LaunchHandler
    {
        public static void ProcessNextAction(NextAction action, bool isUnfinishedInstall = false)
        {
            const string LOG_IDENT = "LaunchHandler::ProcessNextAction";

            switch (action)
            {
                case NextAction.LaunchSettings:
                    App.Logger.WriteLine(LOG_IDENT, "Opening settings");
                    LaunchSettings();
                    break;

                case NextAction.LaunchRoblox:
                    App.Logger.WriteLine(LOG_IDENT, "Opening Roblox");
                    LaunchRoblox(LaunchMode.Player);
                    break;

                case NextAction.LaunchRobloxStudio:
                    App.Logger.WriteLine(LOG_IDENT, "Opening Roblox Studio");
                    LaunchRoblox(LaunchMode.Studio);
                    break;

                default:
                    App.Logger.WriteLine(LOG_IDENT, "Closing");
                    App.Terminate(isUnfinishedInstall ? ErrorCode.ERROR_INSTALL_USEREXIT : ErrorCode.ERROR_SUCCESS);
                    break;
            }
        }

        public static void ProcessLaunchArgs()
        {
            const string LOG_IDENT = "LaunchHandler::ProcessLaunchArgs";

            // this order is specific
            if (App.LaunchSettings.UninstallFlag.Active)
            {
                App.Logger.WriteLine(LOG_IDENT, "Opening uninstaller");
                _ = LaunchUninstaller();
            }
            else if (App.LaunchSettings.MenuFlag.Active)
            {
                App.Logger.WriteLine(LOG_IDENT, "Opening settings");
                LaunchSettings();
            }
            else if (App.LaunchSettings.AccountManagerFlag.Active)
            {
                App.Logger.WriteLine(LOG_IDENT, "Opening account manager");
                LaunchAccountManager();
            }
            else if (App.LaunchSettings.WatcherFlag.Active)
            {
                App.Logger.WriteLine(LOG_IDENT, "Opening watcher");
                LaunchWatcher();
                if (App.LaunchSettings.PostLaunchFlag.Active)
                {
                    App.Logger.WriteLine(LOG_IDENT, "Opening post-launch");
                    LaunchPostLaunch();
                }
            }
            else if (App.LaunchSettings.BackgroundUpdaterFlag.Active)
            {
                App.Logger.WriteLine(LOG_IDENT, "Opening background updater");
                LaunchBackgroundUpdater();
            }
            else if (App.LaunchSettings.MultiInstanceWatcherFlag.Active)
            {
                App.Logger.WriteLine(LOG_IDENT, "Opening multi-instance watcher");
                LaunchMultiInstanceWatcher();
            }
            else if (App.LaunchSettings.RobloxLaunchMode != LaunchMode.None)
            {
                App.Logger.WriteLine(LOG_IDENT, $"Opening bootstrapper ({App.LaunchSettings.RobloxLaunchMode})");
                LaunchRoblox(App.LaunchSettings.RobloxLaunchMode);
            }
            else if (App.LaunchSettings.BloxshadeFlag.Active)
            {
                App.Logger.WriteLine(LOG_IDENT, "Opening Bloxshade");
				LaunchBloxshadeConfig();
			}
			else if (App.LaunchSettings.PostLaunchFlag.Active)
			{
				App.Logger.WriteLine(LOG_IDENT, "Opening post-launch");
				LaunchPostLaunch();
			}
			else if (!App.LaunchSettings.QuietFlag.Active)
            {
                App.Logger.WriteLine(LOG_IDENT, "Opening menu");
                LaunchMenu();
            }
            else
            {
                App.Logger.WriteLine(LOG_IDENT, "Closing - quiet flag active");
                App.Terminate();
            }
        }

        public static async Task LaunchInstaller()
        {
            using var interlock = new InterProcessLock("Installer");

            if (!interlock.IsAcquired)
            {
                await Frontend.ShowMessageBox(Strings.Dialog_AlreadyRunning_Installer, MessageBoxImage.Error);
                App.Terminate();
                return;
            }

            if (App.LaunchSettings.UninstallFlag.Active)
            {
                await Frontend.ShowMessageBox(Strings.Bootstrapper_FirstRunUninstall, MessageBoxImage.Error);
                App.Terminate(ErrorCode.ERROR_INVALID_FUNCTION);
                return;
            }

            if (App.LaunchSettings.QuietFlag.Active)
            {
                var installer = new Installer();

                if (!await installer.CheckInstallLocation())
                    App.Terminate(ErrorCode.ERROR_INSTALL_FAILURE);

                await installer.DoInstall();
                interlock.Dispose();
                ProcessLaunchArgs();
            }
            else
            {
#if QA_BUILD
        await Frontend.ShowMessageBox("...", MessageBoxImage.Information);
#endif

                var langSelector = new LanguageSelectorDialog();
                var langTcs = new TaskCompletionSource();
                langSelector.Closed += (s, e) => langTcs.SetResult();
                langSelector.Show();
                await langTcs.Task;

                var installerWindow = new UI.Elements.Installer.MainWindow();
                var installerTcs = new TaskCompletionSource();
                installerWindow.Closed += (s, e) => installerTcs.SetResult();
                installerWindow.Show();
                await installerTcs.Task;

                interlock.Dispose();

                ProcessNextAction(installerWindow.CloseAction, !installerWindow.Finished);
            }
        }

        public static async Task LaunchUninstaller()
        {
            using var interlock = new InterProcessLock("Uninstaller");

            if (!interlock.IsAcquired)
            {
                await Frontend.ShowMessageBox(Strings.Dialog_AlreadyRunning_Uninstaller, MessageBoxImage.Error);
                App.Terminate();
                return;
            }

            bool confirmed = false;
            bool keepData = true;

            if (App.LaunchSettings.QuietFlag.Active)
            {
                confirmed = true;
            }
            else
            {
                var dialog = new UninstallerDialog();

                var tcs = new TaskCompletionSource();
                dialog.Closed += (s, e) => tcs.SetResult();

                dialog.Show();
                await tcs.Task;

                confirmed = dialog.Confirmed;
                keepData = dialog.KeepData;
            }

            if (!confirmed)
            {
                App.Terminate();
                return;
            }

            await Installer.DoUninstall(keepData);
            await Frontend.ShowMessageBox(Strings.Bootstrapper_SuccessfullyUninstalled, MessageBoxImage.Information);
            App.Terminate();
        }

        public static void LaunchSettings()
        {
            const string LOG_IDENT = "LaunchHandler::LaunchSettings";

            using var interlock = new InterProcessLock("Settings");

            if (interlock.IsAcquired)
            {
                bool showAlreadyRunningWarning = Process.GetProcessesByName(App.ProjectName).Length > 1;

                // before we open the window, force load the distribution states
                // some menu viewmodels require the distribution states, which will result in a short freeze once the page is opened
                if (!App.PlayerState.Loaded)
                    _ = App.PlayerState.Load();
                if (!App.StudioState.Loaded)
                    _ = App.StudioState.Load();

                if (App.Settings.Prop.ShowUsingFroststrapRPC && App.FrostRPC == null)
                {
                    App.FrostRPC = new FroststrapRichPresence();
                }

                var window = new UI.Elements.Settings.MainWindow(showAlreadyRunningWarning);

                App.FrostRPC?.SetPage("Settings");

                window.Show();
            }
            else
            {
                App.Logger.WriteLine(LOG_IDENT, "Found an already existing menu window");

                using var activateEvent = new EventWaitHandle(false, EventResetMode.AutoReset, "Froststrap-ActivateSettingsEvent");
                activateEvent.Set();

                App.Terminate();
            }
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
                ProcessNextAction(dialog.CloseAction);
            };

            dialog.Show();
        }

        public static void LaunchAccountManager()
        {
            if (App.Settings.Prop.ShowUsingFroststrapRPC && App.FrostRPC == null)
            {
                App.FrostRPC = new FroststrapRichPresence();
            }

            var dialog = new UI.Elements.AccountManagers.MainWindow();

            App.FrostRPC?.SetPage("Launch Menu");

            dialog.Show();
        }

        public static async void LaunchRoblox(LaunchMode launchMode)
        {
            const string LOG_IDENT = "LaunchHandler::LaunchRoblox";

            if (launchMode == LaunchMode.None)
                throw new InvalidOperationException("No Roblox launch mode set");

            if (!File.Exists(Path.Combine(Paths.System, "mfplat.dll")))
            {
                await Frontend.ShowMessageBox(Strings.Bootstrapper_WMFNotFound, MessageBoxImage.Error);

                if (!App.LaunchSettings.QuietFlag.Active)
                    Utilities.ShellExecute("https://support.microsoft.com/en-us/topic/media-feature-pack-list-for-windows-n-editions-c1c6fffa-d052-8338-7a79-a4bb980a700a");

                App.Terminate(ErrorCode.ERROR_FILE_NOT_FOUND);
            }

			if (App.Settings.Prop.ConfirmLaunches && Utilities.IsRobloxRunning() && !App.Settings.Prop.MultiInstanceLaunching)
			{
                var result = await Frontend.ShowMessageBox(Strings.Bootstrapper_ConfirmLaunch, MessageBoxImage.Warning, MessageBoxButton.YesNo);

                if (result != MessageBoxResult.Yes)
                {
                    App.Terminate();
                    return;
                }
            }

            // start bootstrapper and show the bootstrapper modal if we're not running silently
            App.Logger.WriteLine(LOG_IDENT, "Initializing bootstrapper");
            App.Bootstrapper = new Bootstrapper(launchMode);
            IBootstrapperDialog? dialog = null;

            if (!App.LaunchSettings.QuietFlag.Active)
            {
                App.Logger.WriteLine(LOG_IDENT, "Initializing bootstrapper dialog");
                dialog = await App.Settings.Prop.BootstrapperStyle.GetNew();
                App.Bootstrapper.Dialog = dialog;
                dialog.Bootstrapper = App.Bootstrapper;
            }

            _ = Task.Run(App.Bootstrapper.Run).ContinueWith(async t =>
            {
                App.Logger.WriteLine(LOG_IDENT, "Bootstrapper task has finished");

                if (t.IsFaulted)
                {
                    App.Logger.WriteLine(LOG_IDENT, "An exception occurred when running the bootstrapper");

                    if (t.Exception is not null)
                        await App.FinalizeExceptionHandling(t.Exception);
                }

                App.Terminate();
            });

            dialog?.ShowBootstrapper();

            App.Logger.WriteLine(LOG_IDENT, "Exiting");
        }

        public static void LaunchWatcher()
        {
            const string LOG_IDENT = "LaunchHandler::LaunchWatcher";

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
                App.Logger.WriteLine(LOG_IDENT, "Watcher task has finished");

                watcher.Dispose();

                if (t.IsFaulted)
                {
                    App.Logger.WriteLine(LOG_IDENT, "An exception occurred when running the watcher");

                    if (t.Exception is not null)
                        await App.FinalizeExceptionHandling(t.Exception);
                }

                // Shouldn't this be done after client closes?
                if (App.Settings.Prop.CleanerOptions != CleanerOptions.Never)
                    Cleaner.DoCleaning();

                App.Terminate();
            });
        }

        public static void LaunchMultiInstanceWatcher()
        {
            const string LOG_IDENT = "LaunchHandler::LaunchMultiInstanceWatcher";

            App.Logger.WriteLine(LOG_IDENT, "Starting multi-instance watcher");

            Task.Run(MultiInstanceWatcher.Run).ContinueWith(async t =>
            {
                App.Logger.WriteLine(LOG_IDENT, "Multi instance watcher task has finished");

                if (t.IsFaulted)
                {
                    App.Logger.WriteLine(LOG_IDENT, "An exception occurred when running the multi-instance watcher");

                    if (t.Exception is not null)
                        await App.FinalizeExceptionHandling(t.Exception);
                }

                App.Terminate();
            });
        }

        public static void LaunchBloxshadeConfig()
        {
            const string LOG_IDENT = "LaunchHandler::LaunchBloxshade";

            App.Logger.WriteLine(LOG_IDENT, "Showing unsupported warning");

            new BloxshadeDialog().Show();
            App.SoftTerminate();
        }

		public static void LaunchPostLaunch()
		{
			const string LOG_IDENT = "LaunchHandler::LaunchPostLaunch";

			if (!int.TryParse(App.LaunchSettings.PostLaunchFlag.Data, out int robloxPid))
			{
				App.Logger.WriteLine(LOG_IDENT, "Failed to parse Roblox PID");
				App.Terminate();
				return;
			}

			Task.Run(async () =>
			{
				await Task.Delay(20000);

				try
				{
					var proc = Process.GetProcessById(robloxPid);
					if (!proc.HasExited)
					{
						if (App.Settings.Prop.SelectedProcessPriority != ProcessPriorityOption.Normal)
						{
							ProcessPriorityClass priorityClass = App.Settings.Prop.SelectedProcessPriority switch
							{
								ProcessPriorityOption.Low => ProcessPriorityClass.Idle,
								ProcessPriorityOption.BelowNormal => ProcessPriorityClass.BelowNormal,
								ProcessPriorityOption.AboveNormal => ProcessPriorityClass.AboveNormal,
								ProcessPriorityOption.High => ProcessPriorityClass.High,
								ProcessPriorityOption.RealTime => ProcessPriorityClass.RealTime,
								_ => ProcessPriorityClass.Normal
							};
							proc.PriorityClass = priorityClass;
							App.Logger.WriteLine(LOG_IDENT, $"Set priority to {priorityClass}");
						}

						if (App.Settings.Prop.AutoCloseCrashHandler)
						{
							foreach (var crashProc in Process.GetProcessesByName("RobloxCrashHandler"))
							{
								try { crashProc.Kill(); } catch { }
							}
						}
					}
				}
				catch (Exception ex)
				{
					App.Logger.WriteLine(LOG_IDENT, $"Worker error: {ex.Message}");
				}

				// do this so that if the watcher is open, it dosent close
				if (!App.LaunchSettings.WatcherFlag.Active)
					App.Terminate();
			});
		}

		public static void LaunchBackgroundUpdater()
        {
            const string LOG_IDENT = "LaunchHandler::LaunchBackgroundUpdater";

            // Activate some LaunchFlags we need
            App.LaunchSettings.QuietFlag.Active = true;
            App.LaunchSettings.NoLaunchFlag.Active = true;

            App.Logger.WriteLine(LOG_IDENT, "Initializing bootstrapper");
            App.Bootstrapper = new Bootstrapper(LaunchMode.Player)
            {
                MutexName = "Froststrap-BackgroundUpdater",
                QuitIfMutexExists = true
            };

            CancellationTokenSource cts = new CancellationTokenSource();

            Task.Run(() =>
            {
                App.Logger.WriteLine(LOG_IDENT, "Started event waiter");
                using (EventWaitHandle handle = new EventWaitHandle(false, EventResetMode.AutoReset, "Froststrap-BackgroundUpdaterKillEvent"))
                    handle.WaitOne();

                App.Logger.WriteLine(LOG_IDENT, "Received close event, killing it all!");
                App.Bootstrapper.Cancel();
            }, cts.Token);

            Task.Run(App.Bootstrapper.Run).ContinueWith(async t =>
            {
                App.Logger.WriteLine(LOG_IDENT, "Bootstrapper task has finished");
                cts.Cancel(); // stop event waiter

                if (t.IsFaulted)
                {
                    App.Logger.WriteLine(LOG_IDENT, "An exception occurred when running the bootstrapper");

                    if (t.Exception is not null)
                        await App.FinalizeExceptionHandling(t.Exception);
                }

                App.Terminate();
            });

            App.Logger.WriteLine(LOG_IDENT, "Exiting");
        }
    }
}
