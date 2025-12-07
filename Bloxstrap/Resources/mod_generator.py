# Froststrap
# Copyright (c) Froststrap Team
#
# This file is part of Froststrap and is distributed under the terms of the
# GNU Affero General Public License, version 3 or later.
#
# SPDX-License-Identifier: AGPL-3.0-or-later
#
# Description: Nix flake for shipping for Nix-darwin, Nix, NixOS, and modules
#              of the Nix ecosystem.

import argparse
import os
import sys
from pathlib import Path

from fontTools.ttLib import TTFont
from fontTools.ttLib.tables.C_O_L_R_ import LayerRecord, table_C_O_L_R_
from fontTools.ttLib.tables.C_P_A_L_ import table_C_P_A_L_

try:
    from fontTools.ttLib.tables.C_P_A_L_ import Color
except ImportError:

    class Color(object):
        def __init__(self, red=0, green=0, blue=0, alpha=255):
            self.red = red
            self.green = green
            self.blue = blue
            self.alpha = alpha


# Probably remove this since it was just for me to test stuff easier
COLOR_OPTIONS = {
    "1": ("Red", (255, 0, 0)),
    "2": ("Orange", (255, 165, 0)),
    "3": ("Yellow", (255, 255, 0)),
    "4": ("Green", (0, 128, 0)),
    "5": ("Blue", (0, 0, 255)),
    "6": ("Indigo", (75, 0, 130)),
    "7": ("Violet", (238, 130, 238)),
    "8": ("Dark Blue", (0, 0, 139)),
    "9": ("Black", (0, 0, 0)),
    "0": ("Custom Hex", None),
}


def hex_to_rgb(hex_str):
    """Converts a hex string (e.g. #FF0000 or FF0000) to an (r, g, b) tuple."""
    hex_str = hex_str.strip().lstrip("#")

    if len(hex_str) != 6:
        raise ValueError("Hex color must be 6 characters long (e.g. FF0000).")

    try:
        r = int(hex_str[0:2], 16)
        g = int(hex_str[2:4], 16)
        b = int(hex_str[4:6], 16)
        return (r, g, b)
    except ValueError:
        raise ValueError("Invalid hex characters provided.")


def get_user_color_choice():
    print("\n--- Select a Color ---")
    for key, (name, _) in COLOR_OPTIONS.items():
        print(f"[{key}] {name}")

    while True:
        choice = input("\nEnter choice number: ").strip()

        if choice in COLOR_OPTIONS:
            name, rgb = COLOR_OPTIONS[choice]

            # Handle Pre-defined colors
            if rgb is not None:
                print(f"Selected: {name} {rgb}")
                return rgb

            # Handle Custom Hex via Menu
            else:
                while True:
                    hex_val = input("Enter Hex Code (e.g., #FF00FF): ").strip()
                    try:
                        return hex_to_rgb(hex_val)
                    except ValueError as e:
                        print(f"Error: {e}")
        else:
            print("Invalid selection, try again.")


def convert_ttf_to_colr(file_path, rgb_color):
    r, g, b = rgb_color
    alpha = 255

    try:
        input_path = Path(file_path)
        
        output_path = input_path.with_suffix(".otf")

        print(f"Processing: {input_path.name} -> COLR ({r},{g},{b})")

        font = TTFont(input_path)
        glyph_order = font.getGlyphOrder()

        # Setup the Palette (CPAL)
        cpal = table_C_P_A_L_()
        cpal.version = 0

        my_color = Color(red=r, green=g, blue=b, alpha=alpha)
        cpal.palettes = [[my_color]]
        cpal.numPaletteEntries = 1
        font["CPAL"] = cpal

        # Setup the Layers (COLR)
        colr = table_C_O_L_R_()
        colr.version = 0
        layer_map = {}
        glyph_count = 0

        for glyph_name in glyph_order:
            if glyph_name == ".notdef":
                continue

            layer = LayerRecord()
            layer.name = glyph_name
            layer.colorID = 0

            layer_map[glyph_name] = [layer]
            glyph_count += 1

        colr.ColorLayers = layer_map
        font["COLR"] = colr

        # Save
        font.save(output_path)
        print(f" -> Success! Saved OTF to: {output_path}")
        
        # Delete original TTF file
        try:
            os.remove(input_path)
            print(f" -> Deleted original TTF: {input_path.name}")
        except Exception as e:
            print(f" -> Warning: Could not delete TTF file: {e}")

    except Exception as e:
        print(f" -> Error processing {file_path}: {e}")


def process_directory(target_dir, rgb_color):
    if not os.path.isdir(target_dir):
        print(f"Error: The directory '{target_dir}' does not exist.")
        sys.exit(1)

    print(f"Scanning directory: {target_dir}")
    print(f"Applying Color: RGB{rgb_color}")
    print("-" * 40)

    count = 0
    for root, dirs, files in os.walk(target_dir):
        for file in files:
            if file.lower().endswith(".ttf"):
                full_path = os.path.join(root, file)
                convert_ttf_to_colr(full_path, rgb_color)
                count += 1

    if count == 0:
        print("No .ttf files found in this directory.")
    else:
        print(f"\nBatch processing complete. Processed {count} files.")


if __name__ == "__main__":
    parser = argparse.ArgumentParser(
        description="Batch convert TTF fonts to Solid Color OTF (COLR v0)."
    )

    parser.add_argument(
        "--path", required=True, help="Path to the directory containing .ttf files."
    )

    parser.add_argument(
        "--color",
        required=True,
        help="Hex color code (e.g. #00008B).",
    )

    args = parser.parse_args()

    try:
        final_color = hex_to_rgb(args.color)
    except ValueError as e:
        print(f"Argument Error: {e}")
        sys.exit(1)
    
    # Run Process
    process_directory(args.path, final_color)