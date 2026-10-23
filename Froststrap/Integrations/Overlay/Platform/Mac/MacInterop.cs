// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

using System.Collections.Concurrent;
using System.Runtime.InteropServices;

namespace Froststrap.Integrations.Overlay.Platform.Mac
{
    [StructLayout(LayoutKind.Sequential)]
    internal struct CGRect
    {
        public double X;
        public double Y;
        public double Width;
        public double Height;

        public readonly bool IsEmpty => Width <= 0 || Height <= 0;
        public readonly double Area => Width * Height;

        public override readonly string ToString() => $"({X}, {Y}, {Width}, {Height})";
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct CGPoint
    {
        public double X;
        public double Y;
    }

    internal static class MacObjC
    {
        private const string Library = "/usr/lib/libobjc.A.dylib";

        private static readonly ConcurrentDictionary<string, IntPtr> Selectors =
            new(StringComparer.Ordinal);

        public static IntPtr Class(string name) => objc_getClass(name);

        public static IntPtr Sel(string name)
        {
            if (Selectors.TryGetValue(name, out IntPtr selector))
                return selector;

            selector = sel_registerName(name);
            Selectors[name] = selector;
            return selector;
        }

        [DllImport(Library, CharSet = CharSet.Ansi, BestFitMapping = false, ThrowOnUnmappableChar = true)]
        private static extern IntPtr objc_getClass(string name);

        [DllImport(Library, CharSet = CharSet.Ansi, BestFitMapping = false, ThrowOnUnmappableChar = true)]
        private static extern IntPtr sel_registerName(string name);

        [DllImport(Library, EntryPoint = "objc_msgSend")]
        public static extern IntPtr SendPtr(IntPtr receiver, IntPtr selector);

        [DllImport(Library, EntryPoint = "objc_msgSend")]
        public static extern IntPtr SendPtrInt(IntPtr receiver, IntPtr selector, int arg);

        [DllImport(Library, EntryPoint = "objc_msgSend")]
        public static extern void SendVoid(IntPtr receiver, IntPtr selector);

        [DllImport(Library, EntryPoint = "objc_msgSend")]
        public static extern void SendVoidPtr(IntPtr receiver, IntPtr selector, IntPtr arg);

        [DllImport(Library, EntryPoint = "objc_msgSend")]
        public static extern void SendVoidLong(IntPtr receiver, IntPtr selector, long arg);

        [DllImport(Library, EntryPoint = "objc_msgSend")]
        public static extern void SendVoidUIntPtr(IntPtr receiver, IntPtr selector, nuint arg);

        [DllImport(Library, EntryPoint = "objc_msgSend")]
        public static extern void SendVoidInt(IntPtr receiver, IntPtr selector, int arg);

        [DllImport(Library, EntryPoint = "objc_msgSend")]
        public static extern void SendVoidDouble(IntPtr receiver, IntPtr selector, double arg);

        [DllImport(Library, EntryPoint = "objc_msgSend")]
        public static extern void SendVoidByte(IntPtr receiver, IntPtr selector, byte arg);

        [DllImport(Library, EntryPoint = "objc_msgSend")]
        public static extern byte SendByte(IntPtr receiver, IntPtr selector);

        [DllImport(Library, EntryPoint = "objc_msgSend")]
        public static extern byte SendByteUIntPtr(IntPtr receiver, IntPtr selector, nuint arg);

        [DllImport(Library, EntryPoint = "objc_msgSend")]
        public static extern int SendInt(IntPtr receiver, IntPtr selector);
    }

    internal static class MacCF
    {
        private const string Library =
            "/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation";

        public const uint Utf8Encoding = 0x08000100;

        public const int NumberSInt32Type = 3;

        public static IntPtr String(string value) =>
            CFStringCreateWithCString(IntPtr.Zero, value, Utf8Encoding);

        public static void Release(IntPtr value)
        {
            if (value != IntPtr.Zero)
                CFRelease(value);
        }

        [DllImport(Library, CharSet = CharSet.Ansi, BestFitMapping = false, ThrowOnUnmappableChar = true)]
        public static extern IntPtr CFStringCreateWithCString(IntPtr allocator, string value, uint encoding);

        [DllImport(Library)]
        public static extern void CFRelease(IntPtr value);

        [DllImport(Library)]
        public static extern nint CFArrayGetCount(IntPtr array);

        [DllImport(Library)]
        public static extern IntPtr CFArrayGetValueAtIndex(IntPtr array, nint index);

        [DllImport(Library)]
        public static extern IntPtr CFDictionaryGetValue(IntPtr dictionary, IntPtr key);

        [DllImport(Library)]
        public static extern byte CFNumberGetValue(IntPtr number, int type, out int value);

        [DllImport(Library)]
        public static extern byte CFBooleanGetValue(IntPtr boolean);

        [DllImport(Library)]
        public static extern IntPtr CFMachPortCreateRunLoopSource(IntPtr allocator, IntPtr port, nint order);

        [DllImport(Library)]
        public static extern IntPtr CFRunLoopGetCurrent();

        [DllImport(Library)]
        public static extern void CFRunLoopAddSource(IntPtr runLoop, IntPtr source, IntPtr mode);

        public const int RunLoopRunFinished = 1;

        [DllImport(Library)]
        public static extern int CFRunLoopRunInMode(IntPtr mode, double seconds, byte returnAfterSourceHandled);

        [DllImport(Library)]
        public static extern void CFRunLoopStop(IntPtr runLoop);
    }

    internal static class MacCG
    {
        private const string Library =
            "/System/Library/Frameworks/CoreGraphics.framework/CoreGraphics";

        public const uint WindowListOptionAll = 0;
        public const uint WindowListExcludeDesktopElements = 1 << 4;

        public const uint EventTapLocationSession = 1;
        public const uint EventTapPlacementHead = 0;
        public const uint EventTapOptionListenOnly = 1;

        public const uint EventKeyDown = 10;
        public const uint EventKeyUp = 11;
        public const uint EventFlagsChanged = 12;
        public const uint EventLeftMouseDown = 1;
        public const uint EventLeftMouseUp = 2;
        public const uint EventRightMouseDown = 3;
        public const uint EventRightMouseUp = 4;
        public const uint EventTapDisabledByTimeout = 0xFFFFFFFE;
        public const uint EventTapDisabledByUserInput = 0xFFFFFFFF;

        public const uint KeyboardEventKeycode = 9;
        public const uint MouseEventButtonNumber = 3;

        [DllImport(Library)]
        public static extern IntPtr CGWindowListCopyWindowInfo(uint option, uint relativeToWindow);

        [DllImport(Library)]
        public static extern byte CGRectMakeWithDictionaryRepresentation(IntPtr dictionary, out CGRect rect);

        [DllImport(Library)]
        public static extern int CGGetDisplaysWithRect(CGRect rect, uint maxDisplays, [Out] uint[]? displays, out uint matchingDisplayCount);

        [DllImport(Library)]
        public static extern CGRect CGDisplayBounds(uint display);

        [DllImport(Library)]
        public static extern uint CGMainDisplayID();

        [DllImport(Library)]
        public static extern IntPtr CGEventTapCreate(
            uint tap,
            uint place,
            uint options,
            ulong eventsOfInterest,
            IntPtr callback,
            IntPtr userInfo);

        [DllImport(Library)]
        public static extern void CGEventTapEnable(IntPtr tap, byte enable);

        [DllImport(Library)]
        public static extern long CGEventGetIntegerValueField(IntPtr @event, uint field);
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct EventHotKeyID
    {
        public uint Signature;
        public uint Id;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct EventTypeSpec
    {
        public uint EventClass;
        public uint EventKind;
    }

    internal static class MacCarbon
    {
        private const string Library =
            "/System/Library/Frameworks/Carbon.framework/Carbon";

        public const uint EventClassKeyboard = 0x6B657962;
        public const uint EventHotKeyPressed = 5;
        public const uint EventParamDirectObject = 0x2D2D2D2D;
        public const uint TypeEventHotKeyID = 0x686B6964;

        public const uint CmdKey = 0x0100;
        public const uint ShiftKey = 0x0200;
        public const uint OptionKey = 0x0800;
        public const uint ControlKey = 0x1000;

        [DllImport(Library)]
        public static extern IntPtr GetApplicationEventTarget();

        [DllImport(Library)]
        public static extern int RegisterEventHotKey(
            uint hotKeyCode,
            uint hotKeyModifiers,
            EventHotKeyID hotKeyId,
            IntPtr target,
            uint options,
            out IntPtr hotKeyRef);

        [DllImport(Library)]
        public static extern int UnregisterEventHotKey(IntPtr hotKeyRef);

        [DllImport(Library)]
        public static extern int InstallEventHandler(
            IntPtr target,
            IntPtr handler,
            nuint numTypes,
            ref EventTypeSpec typeList,
            IntPtr userData,
            out IntPtr handlerRef);

        [DllImport(Library)]
        public static extern int GetEventParameter(
            IntPtr @event,
            uint name,
            uint desiredType,
            IntPtr actualType,
            nuint bufferSize,
            IntPtr actualSize,
            ref EventHotKeyID data);
    }

    internal static class MacPermissions
    {
        private const string ApplicationServices =
            "/System/Library/Frameworks/ApplicationServices.framework/ApplicationServices";

        private const string IoKit =
            "/System/Library/Frameworks/IOKit.framework/IOKit";

        public const uint HidRequestTypeListenEvent = 1;

        [DllImport(ApplicationServices)]
        public static extern byte AXIsProcessTrusted();

        [DllImport(IoKit)]
        public static extern int IOHIDCheckAccess(uint requestType);

        [DllImport(IoKit)]
        public static extern byte IOHIDRequestAccess(uint requestType);
    }
}
