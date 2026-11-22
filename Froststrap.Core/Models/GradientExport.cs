// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

namespace Froststrap.Models
{
    internal sealed class GradientExport
    {
        public List<GradientStops> GradientStops { get; set; } = [];
        public double? GradientAngle { get; set; }
    }
}