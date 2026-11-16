// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

namespace Froststrap.Models.Manifest
{
    internal class ManifestFile
    {
        public string Name { get; set; } = "";
        public string Signature { get; set; } = "";

        public override string ToString()
        {
            return $"[{Signature}] {Name}";
        }
    }
}
