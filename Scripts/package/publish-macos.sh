#!/bin/bash
set -e

PROJECT_FILE=${1:-"Froststrap/Froststrap.csproj"}
BUILD_DIR=${2:-"build"}
PUBLISH_PROFILE_ARM64=${3:-"Publish-osx-arm64"}
PUBLISH_PROFILE_X64=${4:-"Publish-osx-x64"}
CONFIG="Release"

SIGN="${SIGN:-false}"

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

# Signing (only if SIGN=true)
if [ "$SIGN" = "true" ]; then
    echo "Importing certificates into temporary keychain..."

    security create-keychain -p temp /tmp/temp.keychain
    security default-keychain -s /tmp/temp.keychain
    security unlock-keychain -p temp /tmp/temp.keychain

    echo "$CERT_P12_BASE64" | base64 --decode > /tmp/app.p12
    security import /tmp/app.p12 -k /tmp/temp.keychain -P "$P12_PASSWORD" -T /usr/bin/codesign -T /usr/bin/productsign

    echo "$CERT_P12_INSTALLER_BASE64" | base64 --decode > /tmp/installer.p12
    security import /tmp/installer.p12 -k /tmp/temp.keychain -P "$P12_PASSWORD" -T /usr/bin/codesign -T /usr/bin/productsign

    security set-key-partition-list -S apple-tool:,apple: -s -k temp /tmp/temp.keychain

    echo "Signing .app with $DEVELOPER_ID_APP"
    codesign --force --deep --options runtime --entitlements ./macos/Froststrap.entitlements --sign "$DEVELOPER_ID_APP" "$BUILD_DIR/payload/Applications/Froststrap.app"

    codesign --verify --verbose=4 "$BUILD_DIR/payload/Applications/Froststrap.app"

    pkgbuild --root "$BUILD_DIR/payload" --install-location / --identifier xyz.froststrap.desktop "$BUILD_DIR/Froststrap-unsigned.pkg"

    echo "Signing PKG with $DEVELOPER_ID_INSTALLER"
    productsign --sign "$DEVELOPER_ID_INSTALLER" "$BUILD_DIR/Froststrap-unsigned.pkg" "$BUILD_DIR/Froststrap.pkg"

    echo "Submitting for notarization..."
    mkdir -p ~/.private_keys
    echo "$APP_STORE_CONNECT_P8_CONTENT" > ~/.private_keys/AuthKey_${APPLE_KEY_ID}.p8

    SUBMISSION_OUTPUT=$(xcrun notarytool submit "$BUILD_DIR/Froststrap.pkg" --key-id "$APPLE_KEY_ID" --issuer "$APPLE_ISSUER_ID" --key ~/.private_keys/AuthKey_${APPLE_KEY_ID}.p8 --wait 2>&1)
    echo "$SUBMISSION_OUTPUT"

    SUBMISSION_ID=$(echo "$SUBMISSION_OUTPUT" | grep -o 'id: [a-f0-9-]*' | head -1 | sed 's/id: //')

    if echo "$SUBMISSION_OUTPUT" | grep -q "status: Invalid"; then
        echo "Notarization failed. Fetching detailed log..."
        xcrun notarytool log "$SUBMISSION_ID" --key-id "$APPLE_KEY_ID" --issuer "$APPLE_ISSUER_ID" --key ~/.private_keys/AuthKey_${APPLE_KEY_ID}.p8
        exit 1
    fi

    xcrun stapler staple "$BUILD_DIR/Froststrap.pkg"

    security delete-keychain /tmp/temp.keychain
    rm -f /tmp/app.p12 /tmp/installer.p12
else
    echo "Building unsigned PKG (skipping signing)"
    pkgbuild --root "$BUILD_DIR/payload" --install-location / --identifier xyz.froststrap.desktop "$BUILD_DIR/Froststrap-unsigned.pkg"
    mv "$BUILD_DIR/Froststrap-unsigned.pkg" "$BUILD_DIR/Froststrap.pkg"
fi

# Cleanup temp folders
rm -rf "$BUILD_DIR/temp" "$BUILD_DIR/payload"

echo "macOS build complete: $BUILD_DIR/Froststrap.pkg"
