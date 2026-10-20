// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

namespace Froststrap
{
    internal static class LaunchEntry
    {
        public static bool IsStudio(LaunchMode mode) =>
            mode is LaunchMode.Studio or LaunchMode.StudioAuth;

        public static string GetCliArgs(LaunchMode mode, string? payload = null)
        {
            string flag = IsStudio(mode) ? "--studio" : "--player";

            return string.IsNullOrEmpty(payload) ? flag : $"{flag} \"{payload}\"";
        }

        public static NextAction ToNextAction(LaunchMode mode) =>
            IsStudio(mode) ? NextAction.LaunchRobloxStudio : NextAction.LaunchRoblox;

        public static LaunchMode ToLaunchMode(NextAction action) =>
            action == NextAction.LaunchRobloxStudio ? LaunchMode.Studio : LaunchMode.Player;

        public static void LaunchInChildProcess(LaunchMode mode)
        {
            Process.Start(Paths.Process, GetCliArgs(mode));
        }
    }
}
