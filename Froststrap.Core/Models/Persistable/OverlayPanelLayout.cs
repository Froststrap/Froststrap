// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

namespace Froststrap.Models.Persistable
{
    internal class OverlayPanelLayout
    {
        public bool Open { get; set; }
        public bool Pinned { get; set; }
        public double Left { get; set; }
        public double Top { get; set; }
        public double Width { get; set; }
        public double Height { get; set; }
        public int Depth { get; set; }
        public double SurfaceWidth { get; set; }
        public double SurfaceHeight { get; set; }
    }
}