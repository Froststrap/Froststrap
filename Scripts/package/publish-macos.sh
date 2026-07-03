#!/bin/bash
set -e

PROJECT_FILE=${1:-"Froststrap/Froststrap.csproj"}
BUILD_DIR=${2:-"build"}
PUBLISH_PROFILE_ARM64=${3:-"Publish-osx-arm64"}
PUBLISH_PROFILE_X64=${4:-"Publish-osx-x64"}
CONFIG="Release"

# Create new environment
rm -rf "$BUILD_DIR"
mkdir -p "$BUILD_DIR/temp/arm64"
mkdir -p "$BUILD_DIR/temp/x64"
mkdir -p "$BUILD_DIR/Froststrap.app/Contents/MacOS"
mkdir -p "$BUILD_DIR/Froststrap.app/Contents/Resources"

# Publish
dotnet publish "$PROJECT_FILE" \
    -c "$CONFIG" \
    -p:PublishProfile="$PUBLISH_PROFILE_ARM64" \
    -o "./$BUILD_DIR/temp/arm64" \
    --configfile "$(pwd)/nuget.config"

dotnet publish "$PROJECT_FILE" \
    -c "$CONFIG" \
    -p:PublishProfile="$PUBLISH_PROFILE_X64" \
    -o "./$BUILD_DIR/temp/x64" \
    --configfile "$(pwd)/nuget.config"

# Create Universal Binary
lipo -create \
    "./$BUILD_DIR/temp/x64/Froststrap" \
    "./$BUILD_DIR/temp/arm64/Froststrap" \
    -output "./$BUILD_DIR/Froststrap.app/Contents/MacOS/Froststrap"

# Setup App Bundle
cp ./macos/Info.plist "./$BUILD_DIR/Froststrap.app/Contents/Info.plist"
cp ./Froststrap/Froststrap.icns "./$BUILD_DIR/Froststrap.app/Contents/Resources/Froststrap.icns"
chmod +x "./$BUILD_DIR/Froststrap.app/Contents/MacOS/Froststrap"

# Ad-hoc sign
codesign --force --deep --sign - "./$BUILD_DIR/Froststrap.app"

# Package DMG
create-dmg \
  --volname "Froststrap Installer" \
  --window-size 500 300 \
  --icon-size 96 \
  --icon "Froststrap.app" 125 150 \
  --app-drop-link 375 150 \
  "./$BUILD_DIR/Froststrap-macOS.dmg" \
  "./$BUILD_DIR/Froststrap.app"

# Cleanup
rm -rf "./$BUILD_DIR/temp"
rm -rf "./$BUILD_DIR/Froststrap.app"

echo "macOS build complete: $BUILD_DIR/Froststrap-macOS.dmg"