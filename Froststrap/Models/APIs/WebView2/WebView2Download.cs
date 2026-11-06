// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

namespace Froststrap.Models.APIs.WebView2
{
    internal class WebView2Download
    {
        [JsonPropertyName("url")]
        public string? Url { get; set; }

        [JsonPropertyName("fileId")]
        public string? FileId { get; set; }
    }
}