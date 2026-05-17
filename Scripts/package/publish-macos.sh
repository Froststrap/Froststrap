#!/bin/bash
set -e

PROJECT_FILE=${1:-"Froststrap/Froststrap.csproj"}
BUILD_DIR=${2:-"build"}
CONFIG="Release"

# Create new environment
rm -rf "$BUILD_DIR"
mkdir -p "$BUILD_DIR/temp/arm64"
mkdir -p "$BUILD_DIR/temp/x64"
mkdir -p "$BUILD_DIR/Froststrap.app/Contents/MacOS"
mkdir -p "$BUILD_DIR/Froststrap.app/Contents/Resources"

# Publish
for arch in arm64 x64; do
    dotnet publish "$PROJECT_FILE" \
        -r "osx-$arch" \
        -c "$CONFIG" \
        --self-contained true \
        -p:PublishSingleFile=true \
        -p:IncludeNativeLibrariesForSelfExtract=true \
        -o "./$BUILD_DIR/temp/$arch"
done

# Create Universal Binary
lipo -create \
    "./$BUILD_DIR/temp/x64/Froststrap" \
    "./$BUILD_DIR/temp/arm64/Froststrap" \
    -output "./$BUILD_DIR/Froststrap.app/Contents/MacOS/Froststrap"

# Setup App Bundle
cp ./macos/Info.plist "./$BUILD_DIR/Froststrap.app/Contents/Info.plist"
chmod +x "./$BUILD_DIR/Froststrap.app/Contents/MacOS/Froststrap"

# Package DMG (We build it FIRST, then sign inside it)
create-dmg \
  --volname "Froststrap Installer" \
  --window-size 500 300 \
  --icon-size 96 \
  --icon "Froststrap.app" 125 150 \
  --app-drop-link 375 150 \
  "./$BUILD_DIR/Froststrap-macOS.dmg" \
  "./$BUILD_DIR/Froststrap.app"

echo "Mounting DMG to sign the app bundle inside its final layout..."
# Convert DMG to a read/write shadow image so we can sign inside it safely
hdiutil convert "./$BUILD_DIR/Froststrap-macOS.dmg" -format UDRW -o "./$BUILD_DIR/Froststrap-writable.dmg"
rm "./$BUILD_DIR/Froststrap-macOS.dmg"

# Mount the temporary writable disk image
MOUNT_DIR=$(mktemp -d /tmp/froststrap-mount.XXXXXX)
hdiutil attach "./$BUILD_DIR/Froststrap-writable.dmg" -mountpoint "$MOUNT_DIR" -nobrowse

# Ad-hoc sign the app bundle inside the mounted DMG container
echo "Ad-hoc signing Froststrap.app inside the DMG..."
codesign --force --deep --sign - "$MOUNT_DIR/Froststrap.app"

# Unmount the disk image safely
hdiutil detach "$MOUNT_DIR"
rm -rf "$MOUNT_DIR"

# Convert it back to a highly compressed, production-ready read-only DMG asset
hdiutil convert "./$BUILD_DIR/Froststrap-writable.dmg" -format UDZO -o "./$BUILD_DIR/Froststrap-macOS.dmg"
rm "./$BUILD_DIR/Froststrap-writable.dmg"

# Ad-hoc sign the final compressed DMG wrapper package container asset
echo "Ad-hoc signing the final output DMG wrapper container asset..."
codesign --force --sign - "./$BUILD_DIR/Froststrap-macOS.dmg"

# Cleanup
rm -rf "./$BUILD_DIR/temp"
rm -rf "./$BUILD_DIR/Froststrap.app"

echo "macOS build complete: $BUILD_DIR/Froststrap-macOS.dmg"