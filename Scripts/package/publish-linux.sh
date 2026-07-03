#!/bin/bash
set -e

PROJECT_FILE=${1:-"Froststrap/Froststrap.csproj"}
BUILD_DIR=${2:-"build"}
PUBLISH_PROFILE=${3:-"Publish-linux-x64"}
CONFIG="Release"
APP_DIR="$BUILD_DIR/AppDir"
SCRIPT_DIR=$(cd "$(dirname "$0")" && pwd)
REPO_ROOT=$(cd "$SCRIPT_DIR/../.." && pwd)

# Clean and Publish .NET
rm -rf "$BUILD_DIR" && mkdir -p "$BUILD_DIR"
dotnet publish "$PROJECT_FILE" \
    -c "$CONFIG" \
    -p:PublishProfile="$PUBLISH_PROFILE" \
    -o "$BUILD_DIR/linux-temp" \
    --configfile "$REPO_ROOT/nuget.config"

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
RPM_VERSION=$(echo "$VERSION" | sed 's/+/_/g')

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

RPM_TOPDIR="$REPO_ROOT/$BUILD_DIR/rpmbuild"
mkdir -p "$RPM_TOPDIR/BUILD" "$RPM_TOPDIR/BUILDROOT" "$RPM_TOPDIR/RPMS" "$RPM_TOPDIR/SOURCES" "$RPM_TOPDIR/SPECS" "$RPM_TOPDIR/SRPMS"

rpmbuild -bb "$REPO_ROOT/Scripts/fedora/froststrap-rpm.spec" \
    --define "_topdir $RPM_TOPDIR" \
    --define "_froststrap_appdir $REPO_ROOT/$APP_DIR" \
    --define "froststrap_version $RPM_VERSION"

RPM_OUTPUT=$(find "$RPM_TOPDIR/RPMS" -type f -name "*.rpm" | head -n 1)
cp "$RPM_OUTPUT" "$BUILD_DIR/Froststrap-linux-x64.rpm"

# Build Debian Package
mkdir -p "$APP_DIR/DEBIAN"
printf 'Package: froststrap\nVersion: %s\nArchitecture: amd64\nMaintainer: Froststrap-Dev\nDepends: libicu-dev\nDescription: Roblox bootstrapper and mod manager\n' "$VERSION" > "$APP_DIR/DEBIAN/control"

cp Scripts/debian/postinst "$APP_DIR/DEBIAN/postinst"
chmod 755 "$APP_DIR/DEBIAN/postinst"

dpkg-deb --build "$APP_DIR" "$BUILD_DIR/Froststrap-linux-x64.deb"

# Cleanup
rm -rf "$APP_DIR"
rm -rf "$BUILD_DIR/appimagetool.AppImage"
rm -rf "$RPM_TOPDIR"

echo "Linux builds complete"
