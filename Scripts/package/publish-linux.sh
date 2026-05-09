#!/bin/bash
set -e

PROJECT_FILE=${1:-"Froststrap/Froststrap.csproj"}
BUILD_DIR=${2:-"build"}
CONFIG="Release"
APP_DIR="$BUILD_DIR/AppDir"

# Clean and Publish .NET
rm -rf "$BUILD_DIR" && mkdir -p "$BUILD_DIR"
dotnet publish "$PROJECT_FILE" \
    -r linux-x64 \
    -c "$CONFIG" \
    --self-contained true \
    -p:PublishSingleFile=true \
    -p:IncludeNativeLibrariesForSelfExtract=true \
    -o "$BUILD_DIR/linux-temp"

# Setup Filesystem
mkdir -p "$APP_DIR/usr/bin"
mkdir -p "$APP_DIR/usr/share/applications"
mkdir -p "$APP_DIR/usr/share/icons/hicolor/512x512/apps"

cp "$BUILD_DIR/linux-temp/Froststrap" "$APP_DIR/usr/bin/Froststrap"
cp "./Froststrap/Froststrap.png" "$APP_DIR/froststrap.png"
cp "./Froststrap/Froststrap.png" "$APP_DIR/usr/share/icons/hicolor/512x512/apps/froststrap.png"
chmod +x "$APP_DIR/usr/bin/Froststrap"
rm -rf "$BUILD_DIR/linux-temp"

# Version
VERSION=$(echo "$(git describe --tags --always --dirty 2>/dev/null || echo "1.0.0")" | sed 's/^v//; s/-/~/g')

# Create Desktop Entry
cat > "$APP_DIR/Froststrap.desktop" <<EOF
[Desktop Entry]
Type=Application
Name=Froststrap
Comment=A fork of Fishstrap, focused on performance and customization
Exec=Froststrap %u
TryExec=Froststrap
Icon=froststrap
Terminal=false
Categories=Game;
MimeType=x-scheme-handler/roblox;x-scheme-handler/roblox-player;
X-AppImage-Version=$VERSION
EOF
cp "$APP_DIR/Froststrap.desktop" "$APP_DIR/usr/share/applications/Froststrap.desktop"

# Build AppImage
printf '#!/bin/sh\nHERE="$(dirname "$(readlink -f "$0")")"\nexec "$HERE/usr/bin/Froststrap" "$@"\n' > "$APP_DIR/AppRun"
chmod +x "$APP_DIR/AppRun"

if command -v appimagetool >/dev/null 2>&1; then
    APPIMAGE_TOOL=appimagetool
else
    curl -L --fail -o "$BUILD_DIR/appimagetool.AppImage" https://github.com/AppImage/AppImageKit/releases/download/continuous/appimagetool-x86_64.AppImage
    chmod +x "$BUILD_DIR/appimagetool.AppImage"
    APPIMAGE_TOOL="$BUILD_DIR/appimagetool.AppImage"
fi

env -u SOURCE_DATE_EPOCH ARCH=x86_64 "$APPIMAGE_TOOL" --appimage-extract-and-run "$APP_DIR" "$BUILD_DIR/Froststrap-linux-x64.AppImage"

# Build Debian Package
# Remove the global desktop file from the deb
rm -f "$APP_DIR/usr/share/applications/Froststrap.desktop"

mkdir -p "$APP_DIR/DEBIAN"
printf 'Package: froststrap\nVersion: %s\nArchitecture: amd64\nMaintainer: Froststrap-Dev\nDepends: libicu-dev\nDescription: Roblox bootstrapper and mod manager\n' "$VERSION" > "$APP_DIR/DEBIAN/control"

cp Scripts/debian/postinst "$APP_DIR/DEBIAN/postinst"
chmod 755 "$APP_DIR/DEBIAN/postinst"

dpkg-deb --build "$APP_DIR" "$BUILD_DIR/Froststrap-linux-x64.deb"

# Cleanup
rm -rf "$APP_DIR"
rm -rf "$BUILD_DIR/appimagetool.AppImage"

echo "Linux builds complete"
