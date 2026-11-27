// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

using Velopack;

namespace Froststrap.Startup;

internal static class VelopackSetup
{
    public static void Run()
    {
        var builder = VelopackApp.Build()
            .SetAutoApplyOnStartup(false);

        if (OperatingSystem.IsWindows())
        {
            builder = builder
                .OnAfterInstallFastCallback(_ => UriHandler.RegisterProtocolHandlers())
                .OnAfterUpdateFastCallback(_ => UriHandler.RegisterProtocolHandlers())
                .OnBeforeUninstallFastCallback(_ => UriHandler.UnregisterProtocolHandlers());
        }

        builder.Run();
    }
}
