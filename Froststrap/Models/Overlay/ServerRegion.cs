// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

namespace Froststrap.Models.Overlay
{
    internal sealed class ServerRegion
    {
        public string Code { get; set; } = String.Empty;

        public string Name { get; set; } = String.Empty;

        public bool IsAll => String.IsNullOrEmpty(Code);

        public override string ToString() => Name;
    }
}
