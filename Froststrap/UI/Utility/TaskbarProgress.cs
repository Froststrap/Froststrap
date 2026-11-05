// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

using System.Runtime.InteropServices;

namespace Froststrap.UI.Utility
{
    internal static unsafe class TaskbarProgress
    {
        private enum TaskbarStates
        {
            NoProgress = 0,
            Indeterminate = 0x1,
            Normal = 0x2,
            Error = 0x4,
            Paused = 0x8,
        }

        private static readonly Guid TaskbarListClassId = new("56fdf344-fd6d-11d0-958a-006097c9a090");
        private static readonly Guid TaskbarList3InterfaceId = new("ea1afb91-9e28-4b86-90e9-9e9f8a5eefaf");
        private static readonly Lazy<nint> TaskbarInstance = new(CreateTaskbarInstance);

        [DllImport("ole32.dll", PreserveSig = true)]
        private static extern int CoCreateInstance(
            in Guid rclsid,
            nint pUnkOuter,
            uint dwClsContext,
            in Guid riid,
            out nint ppv);

        private static nint CreateTaskbarInstance()
        {
            int result = CoCreateInstance(
                in TaskbarListClassId,
                nint.Zero,
                1,
                in TaskbarList3InterfaceId,
                out nint instance);

            Marshal.ThrowExceptionForHR(result);

            try
            {
                nint* vtable = *(nint**)instance;
                var initialize = (delegate* unmanaged[Stdcall]<nint, int>)vtable[3];
                Marshal.ThrowExceptionForHR(initialize(instance));
                return instance;
            }
            catch
            {
                Release(instance);
                throw;
            }
        }

        private static void Release(nint instance)
        {
            nint* vtable = *(nint**)instance;
            var release = (delegate* unmanaged[Stdcall]<nint, uint>)vtable[2];
            release(instance);
        }

        private static TaskbarStates ConvertEnum(TaskbarItemProgressState state)
        {
            return state switch
            {
                TaskbarItemProgressState.None => TaskbarStates.NoProgress,
                TaskbarItemProgressState.Indeterminate => TaskbarStates.Indeterminate,
                TaskbarItemProgressState.Normal => TaskbarStates.Normal,
                TaskbarItemProgressState.Error => TaskbarStates.Error,
                TaskbarItemProgressState.Paused => TaskbarStates.Paused,
                _ => throw new InvalidOperationException($"Unrecognised TaskbarItemProgressState: {state}")
            };
        }

        public static int SetProgressState(nint windowHandle, TaskbarItemProgressState taskbarState)
        {
            nint instance = TaskbarInstance.Value;
            nint* vtable = *(nint**)instance;
            var setProgressState = (delegate* unmanaged[Stdcall]<nint, nint, int, int>)vtable[10];
            return setProgressState(instance, windowHandle, (int)ConvertEnum(taskbarState));
        }

        public static int SetProgressValue(nint windowHandle, int progressValue, int progressMax)
        {
            nint instance = TaskbarInstance.Value;
            nint* vtable = *(nint**)instance;
            var setProgressValue = (delegate* unmanaged[Stdcall]<nint, nint, ulong, ulong, int>)vtable[9];
            return setProgressValue(instance, windowHandle, (ulong)progressValue, (ulong)progressMax);
        }
    }
}
