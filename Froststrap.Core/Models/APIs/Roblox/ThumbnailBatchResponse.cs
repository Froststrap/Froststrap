// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

namespace Froststrap.Models.APIs.Roblox
{
    internal class ThumbnailBatchResponse
    {
        [JsonPropertyName("data")]
        public ThumbnailResponse[] Data { get; set; } = Array.Empty<ThumbnailResponse>();
    }
}
