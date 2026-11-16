// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

using System.Collections.Concurrent;
using System.Runtime.InteropServices;

namespace Froststrap.Integrations.Overlay.Platform.Mac
{
    internal static unsafe partial class MacAccessibility
    {
        private const string Library = "/usr/lib/libobjc.A.dylib";

        private static readonly ConcurrentDictionary<IntPtr, byte> HiddenWindows = new();
        private static readonly ConcurrentDictionary<(IntPtr Class, IntPtr Selector), IntPtr> OriginalImplementations = new();
        private static readonly HashSet<IntPtr> HookedClasses = [];
        private static readonly Lock Gate = new();

        private static readonly IntPtr IsAccessibilityElementSelector = sel_registerName("isAccessibilityElement");
        private static readonly IntPtr AccessibilityIsIgnoredSelector = sel_registerName("accessibilityIsIgnored");
        private static readonly IntPtr AccessibilityChildrenSelector = sel_registerName("accessibilityChildren");
        private static readonly IntPtr ClassSelector = sel_registerName("class");

        public static bool HideWindow(IntPtr nsWindow)
        {
            if (nsWindow == IntPtr.Zero)
                return false;

            IntPtr windowClass = MacObjC.SendPtr(nsWindow, ClassSelector);

            if (windowClass == IntPtr.Zero)
                return false;

            lock (Gate)
            {
                if (HookedClasses.Add(windowClass))
                    HookClass(windowClass);
            }

            HiddenWindows[nsWindow] = 0;

            return true;
        }

        public static void ForgetWindow(IntPtr nsWindow)
        {
            if (nsWindow != IntPtr.Zero)
                HiddenWindows.TryRemove(nsWindow, out _);
        }

        private static void HookClass(IntPtr windowClass)
        {
            Hook(windowClass, IsAccessibilityElementSelector, (IntPtr)(delegate* unmanaged<IntPtr, IntPtr, byte>)&IsAccessibilityElement);
            Hook(windowClass, AccessibilityIsIgnoredSelector, (IntPtr)(delegate* unmanaged<IntPtr, IntPtr, byte>)&AccessibilityIsIgnored);
            Hook(windowClass, AccessibilityChildrenSelector, (IntPtr)(delegate* unmanaged<IntPtr, IntPtr, IntPtr>)&AccessibilityChildren);
        }

        private static void Hook(IntPtr windowClass, IntPtr selector, IntPtr replacement)
        {
            IntPtr original = class_getMethodImplementation(windowClass, selector);

            if (original == IntPtr.Zero || original == replacement)
                return;

            IntPtr method = class_getInstanceMethod(windowClass, selector);

            if (method == IntPtr.Zero)
                return;

            OriginalImplementations[(windowClass, selector)] = original;
            class_replaceMethod(windowClass, selector, replacement, method_getTypeEncoding(method));
        }

        private static IntPtr FindOriginal(IntPtr self, IntPtr selector)
        {
            for (IntPtr cls = MacObjC.SendPtr(self, ClassSelector); cls != IntPtr.Zero; cls = class_getSuperclass(cls))
            {
                if (OriginalImplementations.TryGetValue((cls, selector), out IntPtr original))
                    return original;
            }

            return IntPtr.Zero;
        }

        [UnmanagedCallersOnly]
        private static byte IsAccessibilityElement(IntPtr self, IntPtr selector)
        {
            if (HiddenWindows.ContainsKey(self))
                return 0;

            IntPtr original = FindOriginal(self, selector);

            return original == IntPtr.Zero ? (byte)1 : ((delegate* unmanaged<IntPtr, IntPtr, byte>)original)(self, selector);
        }

        [UnmanagedCallersOnly]
        private static byte AccessibilityIsIgnored(IntPtr self, IntPtr selector)
        {
            if (HiddenWindows.ContainsKey(self))
                return 1;

            IntPtr original = FindOriginal(self, selector);

            return original == IntPtr.Zero ? (byte)0 : ((delegate* unmanaged<IntPtr, IntPtr, byte>)original)(self, selector);
        }

        [UnmanagedCallersOnly]
        private static IntPtr AccessibilityChildren(IntPtr self, IntPtr selector)
        {
            if (HiddenWindows.ContainsKey(self))
                return IntPtr.Zero;

            IntPtr original = FindOriginal(self, selector);

            return original == IntPtr.Zero ? IntPtr.Zero : ((delegate* unmanaged<IntPtr, IntPtr, IntPtr>)original)(self, selector);
        }

        [LibraryImport(Library, StringMarshalling = StringMarshalling.Utf8)]
        private static partial IntPtr sel_registerName(string name);

        [LibraryImport(Library)]
        private static partial IntPtr class_getMethodImplementation(IntPtr cls, IntPtr selector);

        [LibraryImport(Library)]
        private static partial IntPtr class_getInstanceMethod(IntPtr cls, IntPtr selector);

        [LibraryImport(Library)]
        private static partial IntPtr class_getSuperclass(IntPtr cls);

        [LibraryImport(Library)]
        private static partial IntPtr method_getTypeEncoding(IntPtr method);

        [LibraryImport(Library)]
        private static partial IntPtr class_replaceMethod(IntPtr cls, IntPtr selector, IntPtr implementation, IntPtr types);
    }
}
