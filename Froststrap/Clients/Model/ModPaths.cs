// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

namespace Froststrap.Clients.Model;

internal sealed record ModPaths(
    string VersionRoot,
    string ContentRoot,
    string ClientSettingsRoot,
    ModSupport Support,
    RestoreStrategy Restore);
