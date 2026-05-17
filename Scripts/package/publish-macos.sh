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
