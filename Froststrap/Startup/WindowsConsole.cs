// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

using System.Runtime.InteropServices;

namespace Froststrap.Startup;

internal static partial class WindowsConsole
{
    private const int AttachParentProcess = -1;

    public static void Allocate()
    {
        if (OperatingSystem.IsWindows())
            _ = AllocConsole();
    }

    public static void AttachToParent()
    {
        if (OperatingSystem.IsWindows())
            _ = AttachConsole(AttachParentProcess);
    }

    [LibraryImport("kernel32.dll")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static partial int AllocConsole();

    [LibraryImport("kernel32.dll")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static partial int AttachConsole(int dwProcessId);
}
