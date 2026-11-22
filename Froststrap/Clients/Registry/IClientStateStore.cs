// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

using Froststrap.Models.Persistable;

namespace Froststrap.Clients.Registry;

/// <summary>
/// Maps a client to its distribution state file. Existing file names are kept stable so
/// moving a client behind this seam requires no data migration.
/// </summary>
internal interface IClientStateStore
{
    JsonManager<DistributionState> For(string stateFileName);
}

/// <summary>
/// Uses the existing <see cref="App.PlayerState"/> and <see cref="App.StudioState"/>
/// instances so hashes and in-memory changes stay shared with the rest of the app. Any
/// future client gets its own file, named after its descriptor.
/// </summary>
internal sealed class AppClientStateStore : IClientStateStore
{
    private const string PlayerStateFileName = nameof(App.PlayerState);
    private const string StudioStateFileName = nameof(App.StudioState);

    private readonly Dictionary<string, JsonManager<DistributionState>> _extra = new(StringComparer.Ordinal);

    public JsonManager<DistributionState> For(string stateFileName)
    {
        if (stateFileName == PlayerStateFileName)
            return App.PlayerState;

        if (stateFileName == StudioStateFileName)
            return App.StudioState;

        if (!_extra.TryGetValue(stateFileName, out JsonManager<DistributionState>? manager))
        {
            manager = new LazyJsonManager<DistributionState>(stateFileName);
            _extra[stateFileName] = manager;
        }

        return manager;
    }
}
