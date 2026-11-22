// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

namespace Froststrap.Clients.Contracts;

internal interface IInstallReporter
{
    void SetStatus(string message);

    void SetIndeterminate(bool includeTaskbar = false);

    void SetProgress(double percent);

    void ClearProgress();

    Task DownloadAsync(string url, string destination);
}
