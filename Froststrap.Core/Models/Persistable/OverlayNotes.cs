// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

namespace Froststrap.Models.Persistable
{
    internal class OverlayNotes
    {
        public string Content { get; set; } = String.Empty;

        public DateTime? UpdatedAt { get; set; }
    }
}
