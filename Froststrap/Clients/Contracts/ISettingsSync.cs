// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

namespace Froststrap.Clients.Contracts
{
    internal interface ISettingsSync
    {
        bool HasUnsavedChanges { get; }

        void SyncFastFlags(IReadOnlyDictionary<string, object> flags);

        void Save();
    }
}
