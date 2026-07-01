#!/usr/bin/env python3
"""
Generate LucideIconNames.cs and ResourcesIcons.axaml from Lucide SVG icons.

Usage:
  python tools/generate_icons.py --version 0.473.0
  python tools/generate_icons.py --icons-dir ./path/to/svgs
"""

import argparse
import io
import os
import re
import sys
import xml.etree.ElementTree as ET
import zipfile
import urllib.request
import tempfile
from pathlib import Path

SVG_NS = "http://www.w3.org/2000/svg"

# Stats tracking
element_counts = {}


def kebab_to_pascal(name: str) -> str:
    """Convert kebab-case to PascalCase. e.g. 'arrow-up-right' -> 'ArrowUpRight'"""
    parts = name.split("-")
    result = "".join(part.capitalize() for part in parts)
    # C# identifiers can't start with a digit
    if result and result[0].isdigit():
        result = "_" + result
    return result


def parse_points(points_str: str) -> list[tuple[str, str]]:
    """Parse SVG points attribute into coordinate pairs."""
    nums = points_str.replace(",", " ").split()
    return [(nums[i], nums[i + 1]) for i in range(0, len(nums), 2)]


def escape_xml_attr(s: str) -> str:
    """Escape special characters for XML attribute values."""
    return s.replace("&", "&amp;").replace('"', "&quot;").replace("<", "&lt;").replace(">", "&gt;")


def convert_element(elem) -> str | None:
    """Convert an SVG element to an Avalonia GeometryDrawing XAML string."""
    tag = elem.tag.replace(f"{{{SVG_NS}}}", "")

    # Track stats
    element_counts[tag] = element_counts.get(tag, 0) + 1

    pen_xml = (
        '                    <GeometryDrawing.Pen>\n'
        '                        <Pen Brush="Black" Thickness="2" LineCap="Round" LineJoin="Round" />\n'
        '                    </GeometryDrawing.Pen>'
    )

    if tag == "path":
        d = elem.get("d", "")
        if not d:
            return None
        geom = escape_xml_attr(f"F1 {d}")
        return (
            f'                <GeometryDrawing Geometry="{geom}">\n'
            f'{pen_xml}\n'
            f'                </GeometryDrawing>'
        )

    elif tag == "circle":
        cx = elem.get("cx", "0")
        cy = elem.get("cy", "0")
        r = elem.get("r", "0")
        return (
            f'                <GeometryDrawing>\n'
            f'{pen_xml}\n'
            f'                    <GeometryDrawing.Geometry>\n'
            f'                        <EllipseGeometry RadiusX="{r}" RadiusY="{r}" Center="{cx},{cy}" />\n'
            f'                    </GeometryDrawing.Geometry>\n'
            f'                </GeometryDrawing>'
        )

    elif tag == "ellipse":
        cx = elem.get("cx", "0")
        cy = elem.get("cy", "0")
        rx = elem.get("rx", "0")
        ry = elem.get("ry", "0")
        # SVG spec: if only one radius is specified, the other defaults to it
        if elem.get("rx") and not elem.get("ry"):
            ry = rx
        elif elem.get("ry") and not elem.get("rx"):
            rx = ry
        return (
            f'                <GeometryDrawing>\n'
            f'{pen_xml}\n'
            f'                    <GeometryDrawing.Geometry>\n'
            f'                        <EllipseGeometry RadiusX="{rx}" RadiusY="{ry}" Center="{cx},{cy}" />\n'
            f'                    </GeometryDrawing.Geometry>\n'
            f'                </GeometryDrawing>'
        )

    elif tag == "rect":
        x = elem.get("x", "0")
        y = elem.get("y", "0")
        w = elem.get("width", "0")
        h = elem.get("height", "0")
        rx = elem.get("rx", "0")
        ry = elem.get("ry", "0")
        # SVG spec: if only one radius is specified, the other defaults to it
        if elem.get("rx") and not elem.get("ry"):
            ry = rx
        elif elem.get("ry") and not elem.get("rx"):
            rx = ry
        return (
            f'                <GeometryDrawing>\n'
            f'{pen_xml}\n'
            f'                    <GeometryDrawing.Geometry>\n'
            f'                        <RectangleGeometry RadiusX="{rx}" RadiusY="{ry}" Rect="{x},{y},{w},{h}" />\n'
            f'                    </GeometryDrawing.Geometry>\n'
            f'                </GeometryDrawing>'
        )

    elif tag == "line":
        x1 = elem.get("x1", "0")
        y1 = elem.get("y1", "0")
        x2 = elem.get("x2", "0")
        y2 = elem.get("y2", "0")
        return (
            f'                <GeometryDrawing>\n'
            f'{pen_xml}\n'
            f'                    <GeometryDrawing.Geometry>\n'
            f'                        <LineGeometry StartPoint="{x1},{y1}" EndPoint="{x2},{y2}" />\n'
            f'                    </GeometryDrawing.Geometry>\n'
            f'                </GeometryDrawing>'
        )

    elif tag == "polyline":
        points = parse_points(elem.get("points", ""))
        if not points:
            return None
        path_data = f"M{points[0][0]} {points[0][1]}"
        for px, py in points[1:]:
            path_data += f" L{px} {py}"
        geom = escape_xml_attr(f"F1 {path_data}")
        return (
            f'                <GeometryDrawing Geometry="{geom}">\n'
            f'{pen_xml}\n'
            f'                </GeometryDrawing>'
        )

    elif tag == "polygon":
        points = parse_points(elem.get("points", ""))
        if not points:
            return None
        path_data = f"M{points[0][0]} {points[0][1]}"
        for px, py in points[1:]:
            path_data += f" L{px} {py}"
        path_data += " Z"
        geom = escape_xml_attr(f"F1 {path_data}")
        return (
            f'                <GeometryDrawing Geometry="{geom}">\n'
            f'{pen_xml}\n'
            f'                </GeometryDrawing>'
        )

    else:
        return None


def collect_elements(parent) -> list:
    """Recursively collect drawable SVG elements, descending into <g> groups."""
    elements = []
    for child in parent:
        tag = child.tag.replace(f"{{{SVG_NS}}}", "")
        if tag == "g":
            elements.extend(collect_elements(child))
        elif tag in ("path", "circle", "ellipse", "rect", "line", "polyline", "polygon"):
            elements.append(child)
    return elements


def parse_svg(filepath: Path) -> list[str]:
    """Parse an SVG file and return list of GeometryDrawing XAML strings."""
    tree = ET.parse(filepath)
    root = tree.getroot()
    elements = collect_elements(root)
    drawings = []
    for elem in elements:
        xaml = convert_element(elem)
        if xaml:
            drawings.append(xaml)
    return drawings


def generate_axaml(icons: dict[str, list[str]]) -> str:
    """Generate the ResourcesIcons.axaml content."""
    lines = [
        '<ResourceDictionary xmlns="https://github.com/avaloniaui"',
        '                    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">',
    ]

    for name in sorted(icons.keys()):
        drawings = icons[name]
        if not drawings:
            continue
        lines.append(f'    <DrawingImage x:Key="{name}">')
        lines.append(f'        <DrawingImage.Drawing>')
        lines.append(f'            <DrawingGroup ClipGeometry="M 0,0 L 24,0 24,24 0,24 Z">')
        for drawing in drawings:
            lines.append(drawing)
        lines.append(f'            </DrawingGroup>')
        lines.append(f'        </DrawingImage.Drawing>')
        lines.append(f'    </DrawingImage>')

    lines.append('</ResourceDictionary>')
    return "\n".join(lines) + "\n"


def generate_enum(names: list[str]) -> str:
    """Generate the LucideIconNames.cs content."""
    sorted_names = sorted(names)
    members = ",\n    ".join(sorted_names)
    return (
        "namespace LucideAvalonia.Enum;\n"
        "\n"
        "public enum LucideIconNames\n"
        "{\n"
        f"    {members}\n"
        "}\n"
    )


def download_icons(version: str, dest: Path) -> Path:
    """Download and extract Lucide icons from a GitHub release."""
    url = f"https://github.com/lucide-icons/lucide/archive/refs/tags/{version}.zip"
    print(f"Downloading {url} ...")
    response = urllib.request.urlopen(url)
    data = response.read()
    print(f"Downloaded {len(data) / 1024 / 1024:.1f} MB")

    zip_path = dest / "lucide.zip"
    zip_path.write_bytes(data)

    print("Extracting icons...")
    icons_dir = dest / "icons"
    icons_dir.mkdir(exist_ok=True)

    with zipfile.ZipFile(zip_path) as zf:
        # Icons are in lucide-{version}/icons/*.svg
        prefix = None
        for name in zf.namelist():
            # Find the icons directory
            if "/icons/" in name and name.endswith(".svg"):
                if prefix is None:
                    prefix = name.split("/icons/")[0]
                svg_name = name.split("/icons/")[-1]
                # Only top-level SVGs (skip subdirectories)
                if "/" not in svg_name:
                    target = icons_dir / svg_name
                    target.write_bytes(zf.read(name))

    svg_count = len(list(icons_dir.glob("*.svg")))
    print(f"Extracted {svg_count} SVG files")
    return icons_dir


def main():
    parser = argparse.ArgumentParser(description="Generate Avalonia icon resources from Lucide SVGs")
    group = parser.add_mutually_exclusive_group(required=True)
    group.add_argument("--version", help="Lucide release version to download (e.g. 0.473.0)")
    group.add_argument("--icons-dir", help="Local directory containing SVG files")
    parser.add_argument(
        "--output-dir",
        default=None,
        help="Output directory (default: LucideAvalonia/ relative to repo root)",
    )
    args = parser.parse_args()

    # Determine output directory
    if args.output_dir:
        output_dir = Path(args.output_dir)
    else:
        # Default: LucideAvalonia/ relative to this script's location
        script_dir = Path(__file__).parent
        output_dir = script_dir.parent / "LucideAvalonia"

    enum_path = output_dir / "Enum" / "LucideIconNames.cs"
    axaml_path = output_dir / "Lucide" / "ResourcesIcons.axaml"

    # Get icons directory
    temp_dir = None
    if args.version:
        temp_dir = Path(tempfile.mkdtemp())
        icons_dir = download_icons(args.version, temp_dir)
    else:
        icons_dir = Path(args.icons_dir)

    if not icons_dir.is_dir():
        print(f"Error: Icons directory not found: {icons_dir}", file=sys.stderr)
        sys.exit(1)

    svg_files = sorted(icons_dir.glob("*.svg"))
    if not svg_files:
        print(f"Error: No SVG files found in {icons_dir}", file=sys.stderr)
        sys.exit(1)

    print(f"\nProcessing {len(svg_files)} SVG files...")

    icons = {}
    skipped = []
    for svg_file in svg_files:
        name = kebab_to_pascal(svg_file.stem)
        drawings = parse_svg(svg_file)
        if drawings:
            icons[name] = drawings
        else:
            skipped.append(svg_file.name)

    if skipped:
        print(f"\nWarning: Skipped {len(skipped)} SVGs with no drawable elements:")
        for s in skipped[:10]:
            print(f"  - {s}")
        if len(skipped) > 10:
            print(f"  ... and {len(skipped) - 10} more")

    # Check for duplicate PascalCase names
    name_map = {}
    for svg_file in svg_files:
        pascal = kebab_to_pascal(svg_file.stem)
        name_map.setdefault(pascal, []).append(svg_file.stem)
    dupes = {k: v for k, v in name_map.items() if len(v) > 1}
    if dupes:
        print(f"\nWarning: Duplicate PascalCase names detected:")
        for pascal, originals in dupes.items():
            print(f"  {pascal} <- {originals}")

    # Generate output
    print(f"\nGenerating {len(icons)} icons...")

    axaml_content = generate_axaml(icons)
    enum_content = generate_enum(list(icons.keys()))

    # Write files
    enum_path.parent.mkdir(parents=True, exist_ok=True)
    axaml_path.parent.mkdir(parents=True, exist_ok=True)

    enum_path.write_text(enum_content, encoding="utf-8")
    axaml_path.write_text(axaml_content, encoding="utf-8")

    print(f"\nOutput:")
    print(f"  {enum_path} ({len(icons)} enum entries)")
    print(f"  {axaml_path} ({len(axaml_content) / 1024:.0f} KB)")

    print(f"\nSVG element breakdown:")
    for tag, count in sorted(element_counts.items()):
        print(f"  <{tag}>: {count}")

    # Cleanup
    if temp_dir:
        import shutil
        shutil.rmtree(temp_dir, ignore_errors=True)

    print("\nDone!")


if __name__ == "__main__":
    main()
