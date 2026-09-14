// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

namespace Froststrap.Models
{
    internal class LaunchFlag(string identifiers)
    {
        public string Identifiers { get; private set; } = identifiers;

        public bool Active;
        public string? Data;
    }
}