#!/bin/bash
set -e

cd "$(dirname "$0")/../.."

FLATPAK_DIR="flatpak"
MANIFEST="$FLATPAK_DIR/org.froststrap.Froststrap.yml"
BUILD_DIR="$FLATPAK_DIR/build-dir"
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

# Clean previous build
rm -rf "$BUILD_DIR" "$REPO_DIR" "$STATE_DIR"

# Build
flatpak-builder \
  --user \
  --install-deps-from=flathub \
  --state-dir="$STATE_DIR" \
  "$BUILD_DIR" \
  "$MANIFEST"

# Export and create bundle
mkdir -p "$REPO_DIR"
flatpak build-export "$REPO_DIR" "$BUILD_DIR"
flatpak build-bundle "$REPO_DIR" "$FLATPAK_FILE" org.froststrap.Froststrap \
  --runtime-repo=https://flathub.org/repo/flathub.flatpakrepo

echo "Done!"
echo "Bundle: $FLATPAK_FILE"
