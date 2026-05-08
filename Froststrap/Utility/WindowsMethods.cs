// TODO: for the sake of mantainability, move everything that only works on windows to be here

#if WINDOWS
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace Froststrap;

[SupportedOSPlatform("windows")]
internal static class WindowsMethods
{
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool FlashWindowEx(ref FLASHWINFO pwfi);

    [StructLayout(LayoutKind.Sequential)]
    internal struct FLASHWINFO
    {
        public uint cbSize;
        public IntPtr hwnd;
        public uint dwFlags;
        public uint uCount;
        public uint dwTimeout;
    }
}
#endif
