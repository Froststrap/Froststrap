// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

using Froststrap.Clients.Model;

namespace Froststrap.Clients.Layouts;

internal sealed class RosemaryPlayerLayout : MacBundlePlayerLayout
{
    public override string ClientId => "rosemary";

    public override string? SigningTeamId => null;

    public override IReadOnlyList<string> ProcessNames => ["rosemary"];

    public override string? CrashHandlerProcessName => null;

    public override CookieFormat CookieFormat => CookieFormat.RosemaryJar;

    public override ChannelStore ChannelStore => ChannelStore.PreferencesPlist;

    public override ClientInstall Resolve(ClientResolveContext context) => base.Resolve(context) with
    {
        CookiesDirectory = Path.Combine(context.UserProfile, "Library", "Cookies")
    };
}
