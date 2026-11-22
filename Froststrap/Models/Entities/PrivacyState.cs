// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

namespace Froststrap.Models.Entities
{
    internal sealed class PrivacyState
    {
        public PrivacyLevel? Online { get; init; }

        public PrivacyLevel? Join { get; init; }

        public IReadOnlyList<PrivacyLevel> OnlineOptions { get; init; } = [];

        public IReadOnlyList<PrivacyLevel> JoinOptions { get; init; } = [];
    }
}