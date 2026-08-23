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
mkdir -p "$BUILD_DIR/payload/Applications/Froststrap.app/Contents/MacOS"
mkdir -p "$BUILD_DIR/payload/Applications/Froststrap.app/Contents/Resources"

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
    -output "./$BUILD_DIR/payload/Applications/Froststrap.app/Contents/MacOS/Froststrap"

# Setup App Bundle
cp ./macos/Info.plist "./$BUILD_DIR/payload/Applications/Froststrap.app/Contents/Info.plist"
cp ./Froststrap/Froststrap.icns "./$BUILD_DIR/payload/Applications/Froststrap.app/Contents/Resources/Froststrap.icns"
chmod +x "./$BUILD_DIR/payload/Applications/Froststrap.app/Contents/MacOS/Froststrap"

pkgbuild --root "$BUILD_DIR/payload" --install-location / --identifier xyz.froststrap.desktop "$BUILD_DIR/Froststrap.pkg"

# Cleanup
rm -rf "./$BUILD_DIR/temp"
rm -rf "./$BUILD_DIR/payload"

echo "macOS build complete: $BUILD_DIR/Froststrap.pkg"
