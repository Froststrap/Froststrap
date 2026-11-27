// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

using Froststrap.AppData;
using Froststrap.Clients;
using Froststrap.Clients.Model;

namespace Froststrap.Utility;

internal static class VersionsCleaner
{
    private static bool TryDeleteRobloxInDirectory(string dir)
    {
        IAppData[] clients = [App.Clients.PlayerDeployment, App.Clients.StudioDeployment];

        foreach (IAppData client in clients)
        {
            string path = Path.Combine(dir, client.ExecutableName);

            bool exists = client.IsBundle ? Directory.Exists(path) : File.Exists(path);
            if (!exists) continue;

            try
            {
                if (client.IsBundle)
                    Directory.Delete(path, true);
                else
                {
                    File.SetAttributes(path, FileAttributes.Normal);
                    File.Delete(path);
                }
            }
            catch (Exception)
            {
                return false;
            }
        }

        return true;
    }

    public static void Run()
    {
        bool externalPlayer = !App.Clients.Player.Capabilities.RequiresDeploymentApi;

        if (externalPlayer)
        {
            bool isStudio = App.Bootstrapper?.IsStudioLaunch ?? false;
            if (!isStudio)
            {
                App.Logger.Info($"Skipping cleanup on {App.Clients.Player.DisplayName} launches to protect its data directory.");
                return;
            }
        }

        if (BackgroundUpdate.IsRunning)
        {
            App.Logger.Info("A background update is in progress, skipping cleanup");
            return;
        }

        if (!Directory.Exists(Paths.Versions))
        {
            App.Logger.Info("Versions directory does not exist, skipping cleanup.");
            return;
        }

        foreach (string dir in Directory.GetDirectories(Paths.Versions))
        {
            string dirName = Path.GetFileName(dir);

            // to make static directory work on studio linux
            if (externalPlayer && dirName == Path.GetFileName(App.Clients.Player.Install.InstallRoot))
                continue;

            bool shouldDelete = App.Settings.Prop.StaticDirectory
                ? !ClientCatalog.StaticDirectoryNames.Contains(dirName)
                : App.Clients.AllStates.All(state => dirName != state.VersionGuid);

            if (!shouldDelete)
                continue;

            // check if it's still being used first
            // we dont want to accidentally delete the files of a running roblox instance
            if (!TryDeleteRobloxInDirectory(dir))
                continue;

            try
            {
                Directory.Delete(dir, true);
            }
            catch (UnauthorizedAccessException)
            {
                try
                {
                    Filesystem.AssertReadOnlyDirectory(dir);
                    Directory.Delete(dir, true);
                }
                catch (Exception ex)
                {
                    App.Logger.Error(ex, $"Failed to delete {dir}");
                }
            }
            catch (IOException ex)
            {
                App.Logger.Error(ex, $"Failed to delete {dir}");
            }
        }
    }
}
