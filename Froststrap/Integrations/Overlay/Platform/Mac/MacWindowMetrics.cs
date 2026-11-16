// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

using System.Runtime.InteropServices;

namespace Froststrap.Integrations.Overlay.Platform.Mac
{
    internal static partial class MacWindowMetrics
    {
        private const string Library = "/usr/lib/libobjc.A.dylib";

        private const nuint TitledWindowMask = (1 << 0) | (1 << 1) | (1 << 2) | (1 << 3);
        private const nuint BufferedBacking = 2;

        private const double RobloxTitleBarExtra = 4;
        private const double FallbackTitleBarHeight = 28;
        private const double FallbackCornerRadius = 10;

        private const nuint BottomCornersUnflipped = (1 << 0) | (1 << 1);
        private const nuint BottomCornersFlipped = (1 << 2) | (1 << 3);

        private static double? _titleBarHeight;
        private static double? _cornerRadius;

        public static double TitleBarHeight => _titleBarHeight ??= MeasureTitleBarHeight() + RobloxTitleBarExtra;

        public static double CornerRadius => _cornerRadius ??= MeasureCornerRadius();

        public static void ClipBottomCorners(IntPtr nsWindow, double radius)
        {
            if (nsWindow == IntPtr.Zero)
                return;

            IntPtr contentView = MacObjC.SendPtr(nsWindow, MacObjC.Sel("contentView"));
            IntPtr frameView = contentView == IntPtr.Zero ? IntPtr.Zero : MacObjC.SendPtr(contentView, MacObjC.Sel("superview"));

            if (frameView == IntPtr.Zero)
                return;

            MacObjC.SendVoidByte(frameView, MacObjC.Sel("setWantsLayer:"), 1);

            IntPtr layer = MacObjC.SendPtr(frameView, MacObjC.Sel("layer"));

            if (layer == IntPtr.Zero)
                return;

            bool flipped = MacObjC.SendByte(layer, MacObjC.Sel("isGeometryFlipped")) != 0;

            MacObjC.SendVoidDouble(layer, MacObjC.Sel("setCornerRadius:"), Math.Max(radius, 0));
            MacObjC.SendVoidUIntPtr(layer, MacObjC.Sel("setMaskedCorners:"), flipped ? BottomCornersFlipped : BottomCornersUnflipped);
            MacObjC.SendVoidByte(layer, MacObjC.Sel("setMasksToBounds:"), radius > 0 ? (byte)1 : (byte)0);
        }

        private static double MeasureTitleBarHeight()
        {
            try
            {
                var content = new CGRect { X = 0, Y = 0, Width = 400, Height = 300 };
                IntPtr windowClass = MacObjC.Class("NSWindow");
                IntPtr selector = MacObjC.Sel("frameRectForContentRect:styleMask:");

                CGRect frame;

                if (RuntimeInformation.ProcessArchitecture == Architecture.Arm64)
                    frame = SendRectForRect(windowClass, selector, content, TitledWindowMask);
                else
                    SendRectForRectStret(out frame, windowClass, selector, content, TitledWindowMask);

                double height = frame.Height - content.Height;

                return height > 0 && height < 200 ? height : FallbackTitleBarHeight;
            }
            catch (Exception ex)
            {
                App.Logger.Warn($"Could not measure the macOS title bar height: {ex.Message}");
                return FallbackTitleBarHeight;
            }
        }

        private static double MeasureCornerRadius()
        {
            IntPtr window = IntPtr.Zero;

            try
            {
                IntPtr selector = MacObjC.Sel("_cornerRadius");
                IntPtr allocated = MacObjC.SendPtr(MacObjC.Class("NSWindow"), MacObjC.Sel("alloc"));

                window = SendInitWindow(
                    allocated,
                    MacObjC.Sel("initWithContentRect:styleMask:backing:defer:"),
                    new CGRect { X = 0, Y = 0, Width = 400, Height = 300 },
                    TitledWindowMask,
                    BufferedBacking,
                    1);

                if (window == IntPtr.Zero || SendBoolPtr(window, MacObjC.Sel("respondsToSelector:"), selector) == 0)
                    return FallbackCornerRadius;

                double radius = SendDouble(window, selector);

                return radius > 0 && radius < 64 ? radius : FallbackCornerRadius;
            }
            catch (Exception ex)
            {
                App.Logger.Warn($"Could not measure the macOS window corner radius: {ex.Message}");
                return FallbackCornerRadius;
            }
            finally
            {
                if (window != IntPtr.Zero)
                    MacObjC.SendVoid(window, MacObjC.Sel("close"));
            }
        }

        [LibraryImport(Library, EntryPoint = "objc_msgSend")]
        private static partial CGRect SendRectForRect(IntPtr receiver, IntPtr selector, CGRect rect, nuint styleMask);

        [LibraryImport(Library, EntryPoint = "objc_msgSend_stret")]
        private static partial void SendRectForRectStret(out CGRect result, IntPtr receiver, IntPtr selector, CGRect rect, nuint styleMask);

        [LibraryImport(Library, EntryPoint = "objc_msgSend")]
        private static partial IntPtr SendInitWindow(IntPtr receiver, IntPtr selector, CGRect rect, nuint styleMask, nuint backing, byte defer);

        [LibraryImport(Library, EntryPoint = "objc_msgSend")]
        private static partial byte SendBoolPtr(IntPtr receiver, IntPtr selector, IntPtr argument);

        [LibraryImport(Library, EntryPoint = "objc_msgSend")]
        private static partial double SendDouble(IntPtr receiver, IntPtr selector);
    }
}
