// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

namespace Froststrap.Clients.Layouts;

internal sealed class MacBundleVngPlayerLayout : MacBundlePlayerLayout
{
    public override string ClientId => "roblox-mac-vng";

    public override DistributorType Distributor => DistributorType.VNGGames;

    public override string CdnPath => "/vng";

    public override bool SupportsCustomChannels => false;
}
