// Copyright (C) 2015-present MaximumADHD
// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

namespace Froststrap.Models.Manifest
{
    internal class PackageManifest : List<Package>
    {
        public PackageManifest(string data)
        {
            using var reader = new StringReader(data);
            string? version = reader.ReadLine();

            if (version != "v0")
                throw new NotSupportedException($"Unexpected package manifest version: {version} (expected v0!)");

            while (true)
            {
                string? fileName = reader.ReadLine();
                string? signature = reader.ReadLine();

                string? rawPackedSize = reader.ReadLine();
                string? rawSize = reader.ReadLine();

                if (string.IsNullOrEmpty(fileName) ||
                    string.IsNullOrEmpty(signature) ||
                    string.IsNullOrEmpty(rawPackedSize) ||
                    string.IsNullOrEmpty(rawSize))
                    break;

                // ignore launcher
                if (fileName == "RobloxPlayerLauncher.exe")
                    break;

                int packedSize = int.Parse(rawPackedSize, CultureInfo.InvariantCulture);
                int size = int.Parse(rawSize, CultureInfo.InvariantCulture);

                Add(new Package
                {
                    Name = fileName,
                    Signature = signature,
                    PackedSize = packedSize,
                    Size = size
                });
            }
        }
    }
}
