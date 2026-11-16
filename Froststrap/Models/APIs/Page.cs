// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

namespace Froststrap.Models.APIs
{
    internal class Page
    {
        [JsonPropertyName("next_cursor")]
        public string NextCursor { get; set; } = String.Empty;

        [JsonPropertyName("previous_cursor")]
        public string? PreviousCursor { get; set; } = String.Empty;
    }
}
