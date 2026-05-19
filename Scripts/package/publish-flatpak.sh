#!/bin/bash
set -e

cd "$(dirname "$0")/../.."

PROJECT_FILE=${1:-"Froststrap/Froststrap.csproj"}
BUILD_DIR=${2:-"build"}
CONFIG="Release"

FLATPAK_DIR="flatpak"
MANIFEST="$FLATPAK_DIR/io.github.froststrap.yml"
FLATPAK_BUILD_DIR="$FLATPAK_DIR/build-dir"
REPO_DIR="$FLATPAK_DIR/repo"
STATE_DIR="$FLATPAK_DIR/.flatpak-builder"
FLATPAK_FILE="$FLATPAK_DIR/Froststrap-linux-x64.flatpak"

if ! command -v flatpak >/dev/null 2>&1; then
  echo "Error: flatpak is required but was not found in PATH." >&2
  exit 1
fi

if ! command -v flatpak-builder >/dev/null 2>&1; then
  echo "Error: flatpak-builder is required but was not found in PATH." >&2
  exit 1
fi

echo "Building Flatpak..."
echo "  Project: $PROJECT_FILE"
echo "  Build Dir: $BUILD_DIR"
echo "  Config: $CONFIG"

# Clean previous build
rm -rf "$FLATPAK_BUILD_DIR" "$REPO_DIR" "$STATE_DIR"

# Build
flatpak-builder \
  --user \
  --install-deps-from=flathub \
  --state-dir="$STATE_DIR" \
  "$FLATPAK_BUILD_DIR" \
  "$MANIFEST"

# Export and create bundle
mkdir -p "$REPO_DIR"
flatpak build-export "$REPO_DIR" "$FLATPAK_BUILD_DIR"
flatpak build-bundle "$REPO_DIR" "$FLATPAK_FILE" io.github.froststrap \
  --runtime-repo=https://flathub.org/repo/flathub.flatpakrepo

echo "Done!"
echo "Bundle: $FLATPAK_FILE"
