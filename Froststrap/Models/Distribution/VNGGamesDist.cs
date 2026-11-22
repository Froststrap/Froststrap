// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

namespace Froststrap.Models.Distribution
{
    internal class VNGGamesDist : CommonDist, IDistribution
    {
        public override string RobloxDomain { get; } = "robloxapp.vnggames.com";
    }
}
