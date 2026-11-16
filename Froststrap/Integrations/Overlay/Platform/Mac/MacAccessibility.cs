// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

using System.Runtime.InteropServices;

namespace Froststrap.Integrations.Overlay.Platform.Mac
{
    internal static unsafe partial class MacAccessibility
    {
        private const string Library = "/usr/lib/libobjc.A.dylib";
        private const string SubclassPrefix = "FroststrapAXHidden_";

        private static readonly Dictionary<IntPtr, IntPtr> Subclasses = [];
        private static readonly Lock Gate = new();

        public static bool HideWindow(IntPtr nsWindow)
        {
            if (nsWindow == IntPtr.Zero)
                return false;

            IntPtr windowClass = object_getClass(nsWindow);
            string? className = Marshal.PtrToStringUTF8(class_getName(windowClass));

            if (className is null)
                return false;

            if (className.StartsWith(SubclassPrefix, StringComparison.Ordinal))
                return true;

            if (className.StartsWith("NSKVONotifying_", StringComparison.Ordinal))
            {
                App.Logger.Warn($"Not hiding overlay window from accessibility, class {className} is observed");
                return false;
            }

            IntPtr subclass;

            lock (Gate)
            {
                if (!Subclasses.TryGetValue(windowClass, out subclass))
                {
                    subclass = CreateSubclass(windowClass, className);

                    if (subclass == IntPtr.Zero)
                        return false;

                    Subclasses[windowClass] = subclass;
                }
            }

            object_setClass(nsWindow, subclass);

            return true;
        }

        private static IntPtr CreateSubclass(IntPtr baseClass, string baseName)
        {
            string name = SubclassPrefix + baseName;

            IntPtr existing = objc_getClass(name);

            if (existing != IntPtr.Zero)
                return existing;

            IntPtr subclass = objc_allocateClassPair(baseClass, name, 0);

            if (subclass == IntPtr.Zero)
            {
                App.Logger.Warn($"Failed to create accessibility-hidden subclass of {baseName}");
                return IntPtr.Zero;
            }

            AddBoolMethod(subclass, baseClass, "isAccessibilityElement", (IntPtr)(delegate* unmanaged<IntPtr, IntPtr, byte>)&ReturnNo);
            AddBoolMethod(subclass, baseClass, "accessibilityIsIgnored", (IntPtr)(delegate* unmanaged<IntPtr, IntPtr, byte>)&ReturnYes);
            AddMethod(subclass, baseClass, "accessibilityChildren", (IntPtr)(delegate* unmanaged<IntPtr, IntPtr, IntPtr>)&ReturnNil, "@@:");

            objc_registerClassPair(subclass);

            return subclass;
        }

        private static void AddBoolMethod(IntPtr subclass, IntPtr baseClass, string selectorName, IntPtr implementation) =>
            AddMethod(subclass, baseClass, selectorName, implementation, RuntimeInformation.ProcessArchitecture == Architecture.Arm64 ? "B@:" : "c@:");

        private static void AddMethod(IntPtr subclass, IntPtr baseClass, string selectorName, IntPtr implementation, string fallbackTypes)
        {
            IntPtr selector = sel_registerName(selectorName);
            IntPtr method = class_getInstanceMethod(baseClass, selector);
            string? types = method != IntPtr.Zero ? Marshal.PtrToStringUTF8(method_getTypeEncoding(method)) : null;

            class_addMethod(subclass, selector, implementation, types ?? fallbackTypes);
        }

        [UnmanagedCallersOnly]
        private static byte ReturnNo(IntPtr self, IntPtr selector) => 0;

        [UnmanagedCallersOnly]
        private static byte ReturnYes(IntPtr self, IntPtr selector) => 1;

        [UnmanagedCallersOnly]
        private static IntPtr ReturnNil(IntPtr self, IntPtr selector) => IntPtr.Zero;

        [LibraryImport(Library)]
        private static partial IntPtr object_getClass(IntPtr instance);

        [LibraryImport(Library)]
        private static partial IntPtr object_setClass(IntPtr instance, IntPtr newClass);

        [LibraryImport(Library)]
        private static partial IntPtr class_getName(IntPtr cls);

        [LibraryImport(Library, StringMarshalling = StringMarshalling.Utf8)]
        private static partial IntPtr objc_getClass(string name);

        [LibraryImport(Library, StringMarshalling = StringMarshalling.Utf8)]
        private static partial IntPtr objc_allocateClassPair(IntPtr superclass, string name, nuint extraBytes);

        [LibraryImport(Library)]
        private static partial void objc_registerClassPair(IntPtr cls);

        [LibraryImport(Library, StringMarshalling = StringMarshalling.Utf8)]
        private static partial IntPtr sel_registerName(string name);

        [LibraryImport(Library)]
        private static partial IntPtr class_getInstanceMethod(IntPtr cls, IntPtr selector);

        [LibraryImport(Library)]
        private static partial IntPtr method_getTypeEncoding(IntPtr method);

        [LibraryImport(Library, StringMarshalling = StringMarshalling.Utf8)]
        [return: MarshalAs(UnmanagedType.U1)]
        private static partial bool class_addMethod(IntPtr cls, IntPtr selector, IntPtr implementation, string types);
    }
}
