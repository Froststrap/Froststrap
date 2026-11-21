// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

namespace Froststrap.Clients.Contracts;

internal interface ISettingsSync
{
    bool Loaded { get; }

    bool HasUnsavedChanges { get; }

    string? GetPreset(string name);

    void SetPreset(string name, object? value);

    void Load();

    void SyncFastFlags(IReadOnlyDictionary<string, object> flags);

    void Save();
}
