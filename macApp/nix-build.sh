#!/bin/bash
set -e

if [ -z "${SRCROOT:-}" ]; then
  SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
  SRCROOT="$(cd "$SCRIPT_DIR/.." && pwd)"
fi
export SRCROOT

if [ -z "${IN_NIX_SHELL:-}" ]; then
  if [ -n "$SKIP_DOTNET_BUILD" ]; then
    echo "SKIP_DOTNET_BUILD set — skipping build"
    exit 0
  fi
  cd "$SRCROOT" 2>/dev/null || true
  NIX_BIN=""
  for candidate in \
    /run/current-system/sw/bin/nix \
    /nix/var/nix/profiles/default/bin/nix \
    /usr/local/bin/nix \
    "$(command -v nix 2>/dev/null)"
  do
    if [ -x "$candidate" ]; then
      NIX_BIN="$candidate"
      break
    fi
  done
  if [ -z "$NIX_BIN" ]; then
    echo "error: could not locate nix binary" >&2
    exit 1
  fi

  exec "$NIX_BIN" develop --accept-flake-config --command "$0" "$@"
fi

CONFIG="${1:-Release}"
PROJECT_FILE="${2:-"$SRCROOT/../Froststrap/Froststrap.csproj"}"
OUTPUT_DIR="$SRCROOT/build/dotnet"

unset TARGETNAME TARGET_NAME
rm -rf "$OUTPUT_DIR"
mkdir -p "$OUTPUT_DIR"

dotnet publish "$PROJECT_FILE" \
    -c "$CONFIG" \
    -p:PublishProfile="Publish-osx-arm64" \
    -o "$OUTPUT_DIR/arm64" \
    --configfile "$SRCROOT/../nuget.config"

dotnet publish "$PROJECT_FILE" \
    -c "$CONFIG" \
    -p:PublishProfile="Publish-osx-x64" \
    -o "$OUTPUT_DIR/x64" \
    --configfile "$SRCROOT/../nuget.config"

if [ -d "./virtualdisplay/.build/out" ]; then
  BUILD_DIR="out"
elif [ -d "./virtualdisplay/.build/apple" ]; then
  BUILD_DIR="apple"
else
  echo "error: neither .build/out nor .build/apple exists" >&2
  exit 1
fi

cp "./virtualdisplay/.build/$BUILD_DIR/Products/Release/libvirtualdisplay.dylib" "$OUTPUT_DIR/libvirtualdisplay.dylib"

lipo -create \
    "$OUTPUT_DIR/arm64/Froststrap" \
    "$OUTPUT_DIR/x64/Froststrap" \
    -output "$OUTPUT_DIR/Froststrap"

if [ -f "$OUTPUT_DIR/Froststrap" ]; then
    chmod +x "$OUTPUT_DIR/Froststrap"
    echo "Successfully built binary at: $OUTPUT_DIR/Froststrap"
else
    echo "error: expected binary not found at $OUTPUT_DIR/Froststrap" >&2
    exit 1
fi
