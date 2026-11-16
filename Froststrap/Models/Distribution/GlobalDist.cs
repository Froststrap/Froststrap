// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

using Froststrap.AppData;

namespace Froststrap.Models.Distribution
{
    internal class GlobalDist : CommonDist, IDistribution
    {
        public override IAppData RobloxPlayerData { get; } = new RobloxPlayerData();
        public override IAppData RobloxStudioData { get; } = new RobloxStudioData();
    }
}
