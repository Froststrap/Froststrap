#!/bin/bash
set -e

XCODE_PROJECT_DIR="macApp"
XCODE_PROJECT="macApp.xcodeproj"
XCODE_SCHEME="Froststrap"
ENTITLEMENTS_PATH="$XCODE_PROJECT_DIR/Froststrap.entitlements"

CONFIG="Release"
BUILD_DIR="build"
SIGN="${SIGN:-false}"

rm -rf "$BUILD_DIR"
mkdir -p "$BUILD_DIR"

echo "Building via xcodebuild ($XCODE_SCHEME, $CONFIG)..."
xcodebuild \
    -project "$XCODE_PROJECT_DIR/$XCODE_PROJECT" \
    -scheme "$XCODE_SCHEME" \
    -configuration "$CONFIG" \
    CODE_SIGNING_ALLOWED=NO \
    build

APP_PATH="$XCODE_PROJECT_DIR/build/$CONFIG/Froststrap.app"

if [ ! -d "$APP_PATH" ]; then
    echo "ERROR: expected .app not found at $APP_PATH"
    echo "Check SYMROOT / CONFIGURATION_BUILD_DIR in the Xcode project's build settings,"
    echo "or pass -derivedDataPath to xcodebuild to pin the output location explicitly."
    exit 1
fi

cp -R "$APP_PATH" "$BUILD_DIR/Froststrap.app"

if [ "$SIGN" = "true" ]; then
    echo "Signing .app with $DEVELOPER_ID_APP"
    codesign --force --options runtime --entitlements "$ENTITLEMENTS_PATH" \
        --sign "$DEVELOPER_ID_APP" "$BUILD_DIR/Froststrap.app"
    codesign --verify --verbose=4 "$BUILD_DIR/Froststrap.app"

    mkdir -p "$BUILD_DIR/payload/Applications"
    cp -R "$BUILD_DIR/Froststrap.app" "$BUILD_DIR/payload/Applications/Froststrap.app"

    pkgbuild --root "$BUILD_DIR/payload" --install-location / \
        --identifier xyz.froststrap.desktop "$BUILD_DIR/Froststrap-unsigned.pkg"

    echo "Signing PKG with $DEVELOPER_ID_INSTALLER"
    productsign --sign "$DEVELOPER_ID_INSTALLER" \
        "$BUILD_DIR/Froststrap-unsigned.pkg" "$BUILD_DIR/Froststrap.pkg"

    echo "Submitting for notarization..."
    mkdir -p ~/.private_keys
    echo "$APP_STORE_CONNECT_P8_CONTENT" > ~/.private_keys/AuthKey_${APPLE_KEY_ID}.p8

    SUBMISSION_OUTPUT=$(xcrun notarytool submit "$BUILD_DIR/Froststrap.pkg" \
        --key-id "$APPLE_KEY_ID" --issuer "$APPLE_ISSUER_ID" \
        --key ~/.private_keys/AuthKey_${APPLE_KEY_ID}.p8 --wait 2>&1)
    echo "$SUBMISSION_OUTPUT"

    SUBMISSION_ID=$(echo "$SUBMISSION_OUTPUT" | grep -o 'id: [a-f0-9-]*' | head -1 | sed 's/id: //')
    if echo "$SUBMISSION_OUTPUT" | grep -q "status: Invalid"; then
        echo "Notarization failed. Fetching detailed log..."
        xcrun notarytool log "$SUBMISSION_ID" --key-id "$APPLE_KEY_ID" --issuer "$APPLE_ISSUER_ID" \
            --key ~/.private_keys/AuthKey_${APPLE_KEY_ID}.p8
        exit 1
    fi

    xcrun stapler staple "$BUILD_DIR/Froststrap.pkg"
    rm -f ~/.private_keys/AuthKey_${APPLE_KEY_ID}.p8
    rm -rf "$BUILD_DIR/payload" "$BUILD_DIR/Froststrap-unsigned.pkg"
else
    echo "Building unsigned PKG (skipping signing)"
    mkdir -p "$BUILD_DIR/payload/Applications"
    cp -R "$BUILD_DIR/Froststrap.app" "$BUILD_DIR/payload/Applications/Froststrap.app"
    pkgbuild --root "$BUILD_DIR/payload" --install-location / \
        --identifier xyz.froststrap.desktop "$BUILD_DIR/Froststrap.pkg"
    rm -rf "$BUILD_DIR/payload"
fi

echo "macOS build complete: $BUILD_DIR/Froststrap.pkg"
