// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

using Froststrap.Clients.Contracts;

namespace Froststrap.Clients.Providers;

internal sealed class FlatpakProcessService(IClientLayout layout, string flatpakId) : ClientProcessService(layout)
{
    public string FlatpakId { get; } = flatpakId;

    public override void Kill(int pid) => KillApp();

    public void KillApp()
    {
        try
        {
            using Process? process = Process.Start(new ProcessStartInfo
            {
                FileName = "flatpak",
                Arguments = $"kill {FlatpakId}",
                UseShellExecute = false,
                CreateNoWindow = true
            });

            process?.WaitForExit(1000);
        }
        catch
        {
        }
    }
}
