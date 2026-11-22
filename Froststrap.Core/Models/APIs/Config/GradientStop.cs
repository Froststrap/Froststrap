// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

namespace Froststrap.Models.APIs.Config
{
    internal class GradientStop
    {
        [JsonPropertyName("offset")]
        public double Offset { get; set; }

        [JsonPropertyName("color")]
        public string Color { get; set; } = "#FFFFFF";
    }
}