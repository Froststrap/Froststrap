// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

namespace Froststrap.Models.APIs.WebView2
{
    internal class WebView2LatestResponse
    {
        [JsonPropertyName("contentId")]
        public WebView2ContentId? ContentId { get; set; }
    }
}