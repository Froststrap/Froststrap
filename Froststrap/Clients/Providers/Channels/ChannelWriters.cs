// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

using System.Runtime.Versioning;
using Froststrap.Clients.Contracts;
using Froststrap.Clients.Model;

namespace Froststrap.Clients.Providers.Channels;

[SupportedOSPlatform("windows")]
internal sealed class RegistryChannelWriter(string registryName) : IChannelWriter
{
    public void Write(string robloxDomain, string? channel)
    {
        using var key = Microsoft.Win32.Registry.CurrentUser.CreateSubKey($"SOFTWARE\\ROBLOX Corporation\\Environments\\{registryName}\\Channel");
        key.SetValueSafe("www." + robloxDomain, channel ?? "");
    }
}

[SupportedOSPlatform("macos")]
internal sealed class MacDefaultsChannelWriter(string registryName) : IChannelWriter
{
    public void Write(string robloxDomain, string? channel) =>
        MacChannelStore.Apply(registryName, robloxDomain, channel);
}

internal sealed class NullChannelWriter : IChannelWriter
{
    public void Write(string robloxDomain, string? channel)
    {
    }
}

internal static class ChannelWriters
{
    public static IChannelWriter Create(ChannelStore store, ClientTarget target)
    {
        string registryName = target == ClientTarget.Studio ? "RobloxStudio" : "RobloxPlayer";

        return store switch
        {
            ChannelStore.Registry when OperatingSystem.IsWindows() => new RegistryChannelWriter(registryName),
            ChannelStore.MacDefaults when OperatingSystem.IsMacOS() => new MacDefaultsChannelWriter(registryName),
            _ => new NullChannelWriter()
        };
    }
}
