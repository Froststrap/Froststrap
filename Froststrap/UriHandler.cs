// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

using System.Runtime.Versioning;
using Microsoft.Win32;

namespace Froststrap
{
    internal static class UriHandler
    {
        static readonly string[] Schemes =
        [
            "roblox",
            "roblox-player",
            "roblox-studio",
            "roblox-studio-auth"
        ];

        // Only Windows registers at runtime; non-Windows registers via hints during install.
        public static void RegisterProtocolHandlers() => Safely(WindowsHandler.Register);

        public static void UnregisterProtocolHandlers() => Safely(WindowsHandler.Unregister);

        public static void EnsureRegistered() => Safely(() =>
        {
            if (!WindowsHandler.IsRegistered())
                WindowsHandler.Register();
        });

        // Velopack doesn't like exceptions in install/uninstall phase- so handle it properly
        static void Safely(Action action)
        {
            if (!OperatingSystem.IsWindows())
                return;

            try { action(); }
            catch (Exception ex) { App.Logger.Error(ex, "UriHandler failed"); }
        }

        [SupportedOSPlatform("windows")]
        static class WindowsHandler
        {
            public static void Register()
            {
                string exe = Environment.ProcessPath!;
                foreach (string scheme in Schemes)
                {
                    using var key = Registry.CurrentUser.CreateSubKey($@"Software\Classes\{scheme}");
                    key.SetValue("", $"URL: {scheme} Protocol");
                    key.SetValue("URL Protocol", "");
                    using var command = key.CreateSubKey(@"shell\open\command");
                    command.SetValue("", $"\"{exe}\" \"%1\"");
                }
            }

            public static void Unregister()
            {
                foreach (string scheme in Schemes)
                    Registry.CurrentUser.DeleteSubKeyTree($@"Software\Classes\{scheme}", throwOnMissingSubKey: false);
            }

            public static bool IsRegistered()
            {
                using var key = Registry.CurrentUser.OpenSubKey(@"Software\Classes\roblox-player\shell\open\command");
                return key?.GetValue("") as string == $"\"{Environment.ProcessPath}\" \"%1\"";
            }
        }
    }
}
