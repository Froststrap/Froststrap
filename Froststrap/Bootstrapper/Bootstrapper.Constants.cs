// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

namespace Froststrap;

internal sealed partial class Bootstrapper : IDisposable
{
    private const int ProgressBarMaximum = 10000;
    private const double TaskbarProgressMaximum = 1.0;
    private const string SoberFlatpakId = Clients.Runtimes.SoberClient.FlatpakId;
    public const string BackgroundUpdaterLockName = "BackgroundUpdater";
}
