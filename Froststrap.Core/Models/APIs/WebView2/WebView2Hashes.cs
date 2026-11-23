// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

namespace Froststrap.Models.APIs.WebView2
{
    internal class WebView2Hashes
    {
        [JsonPropertyName("sha256")]
        public string? Sha256 { get; set; }
    }
}