// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;

namespace Froststrap.UI.Utility;

internal static class MacOSDock
{
    private const long ActivationPolicyRegular = 0;
    private const long ActivationPolicyAccessory = 1;

    private static long? _appliedPolicy;

    public static void Initialize()
    {
        if (!OperatingSystem.IsMacOS())
            return;

        Visual.IsVisibleProperty.Changed.AddClassHandler<Window>((_, _) => Refresh());
        Window.WindowClosedEvent.AddClassHandler<Window>((_, _) => Refresh(), handledEventsToo: true);
    }

    public static void Refresh()
    {
        if (!OperatingSystem.IsMacOS())
            return;

        Dispatcher.UIThread.Post(Apply, DispatcherPriority.Background);
    }

    private static void Apply()
    {
        var app = Application.Current;

        if (app?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop)
            return;

        bool hasVisibleWindow = desktop.Windows.Any(window => window.IsVisible);
        bool hasTrayIcon = TrayIcon.GetIcons(app)?.Any(icon => icon.IsVisible) == true;

        long policy = hasTrayIcon && !hasVisibleWindow ? ActivationPolicyAccessory : ActivationPolicyRegular;

        if (_appliedPolicy == policy)
            return;

        bool leavingAccessory = _appliedPolicy == ActivationPolicyAccessory;
        _appliedPolicy = policy;

        IntPtr nsApp = objc_msgSend_ret(objc_getClass("NSApplication"), sel_registerName("sharedApplication"));

        if (nsApp == IntPtr.Zero)
            return;

        objc_msgSend_long(nsApp, sel_registerName("setActivationPolicy:"), policy);

        if (leavingAccessory)
            objc_msgSend_bool(nsApp, sel_registerName("activateIgnoringOtherApps:"), true);

        App.Logger.Debug($"Dock activation policy set to {(policy == ActivationPolicyAccessory ? "accessory (menu bar only)" : "regular")}");
    }

    [DllImport("/usr/lib/libobjc.A.dylib", CharSet = CharSet.Ansi, BestFitMapping = false, ThrowOnUnmappableChar = true)]
    private static extern IntPtr objc_getClass(string name);

    [DllImport("/usr/lib/libobjc.A.dylib", CharSet = CharSet.Ansi, BestFitMapping = false, ThrowOnUnmappableChar = true)]
    private static extern IntPtr sel_registerName(string name);

    [DllImport("/usr/lib/libobjc.A.dylib", EntryPoint = "objc_msgSend")]
    private static extern IntPtr objc_msgSend_ret(IntPtr receiver, IntPtr selector);

    [DllImport("/usr/lib/libobjc.A.dylib", EntryPoint = "objc_msgSend")]
    [return: MarshalAs(UnmanagedType.I1)]
    private static extern bool objc_msgSend_long(IntPtr receiver, IntPtr selector, long arg);

    [DllImport("/usr/lib/libobjc.A.dylib", EntryPoint = "objc_msgSend")]
    private static extern void objc_msgSend_bool(IntPtr receiver, IntPtr selector, [MarshalAs(UnmanagedType.I1)] bool arg);
}
