// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

namespace Froststrap.Clients.Contracts;

internal interface IInstallReporter
{
    void SetStatus(string message);

    void SetIndeterminate(bool includeTaskbar = false);

    Task DownloadAsync(string url, string destination);
}
