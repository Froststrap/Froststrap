// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

namespace Froststrap.Enums
{
    internal enum LaunchMode
    {
        None,
        /// <summary>
        /// Launch mode will be determined inside the bootstrapper. Only works if the VersionFlag is set.
        /// </summary>
        Unknown,
        Player,
        Studio,
        StudioAuth
    }
}
