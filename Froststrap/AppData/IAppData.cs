// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

namespace Froststrap.AppData;

internal interface IAppData
{
    string ProductName { get; }

    string BinaryType { get; }

    string ExecutableName { get; }

    bool IsBundle { get; }

    string StaticDirectory { get; }

    string Directory { get; }

    string ExecutablePath { get; }

    JsonManager<DistributionState> DistributionStateManager { get; }

    DistributionState DistributionState { get; }
}
