// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

using Froststrap.AppData;

namespace Froststrap.Models.Distribution
{
    internal class VNGGamesDist : CommonDist, IDistribution
    {
        public override string RobloxDomain { get; } = "robloxapp.vnggames.com";

        public override IAppData RobloxPlayerData { get; } = new RobloxPlayerVNGData();
        public override IAppData RobloxStudioData { get; } = new RobloxStudioData();
    }
}
