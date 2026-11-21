// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

namespace Froststrap.Clients.Contracts;

internal interface IChannelWriter
{
    void Write(string robloxDomain, string? channel);
}
