// Copyright (C) 2015-present MaximumADHD
// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

namespace Froststrap.Models.Manifest
{
    internal class Package
    {
        public string Name { get; set; } = "";

        public string Signature { get; set; } = "";

        public int PackedSize { get; set; }

        public int Size { get; set; }

        public string DownloadPath => Path.Combine(Paths.Downloads, Signature);

        public override string ToString()
        {
            return $"[{Signature}] {Name}";
        }
    }
}
