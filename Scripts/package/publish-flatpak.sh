#!/bin/bash
set -e

cd "$(dirname "$0")/../.."

FLATPAK_DIR="flatpak"
MANIFEST="$FLATPAK_DIR/org.froststrap.Froststrap.yml"
BUILD_DIR="$FLATPAK_DIR/build-dir"
REPO_DIR="$FLATPAK_DIR/repo"
STATE_DIR="$FLATPAK_DIR/.flatpak-builder"
FLATPAK_FILE="$FLATPAK_DIR/Froststrap-linux-x64.flatpak"

echo "Is flatpak and flatpak-builder installed? If not cancel and install it"
echo "There will now be a 10sec wait"
sleep 10

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
