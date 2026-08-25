#!/bin/bash
set -e

# Resolve SRCROOT: use Xcode's injected value if present, otherwise
# compute the repo root from this script's own location (so it still
# works when run manually or from a CI job that doesn't set SRCROOT).
if [ -z "${SRCROOT:-}" ]; then
  SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
  SRCROOT="$(cd "$SCRIPT_DIR/.." && pwd)"
fi
export SRCROOT

# 1. Self-bootstrap into the Nix development environment if not already inside one
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
  unset TARGETNAME
  exec "$NIX_BIN" develop --command "$0" "$@"
fi

# --- 2. Inside the Nix development environment ---
CONFIG="${1:-Release}"
PROJECT_FILE="${2:-"$SRCROOT/../Froststrap/Froststrap.csproj"}"
OUTPUT_DIR="$SRCROOT/build/dotnet"

ARCH="arm64"
[ "$(uname -m)" = "x86_64" ] && ARCH="x64"
PUBLISH_PROFILE="Publish-osx-$ARCH"

echo "Publishing Froststrap binary for osx-$ARCH into $OUTPUT_DIR..."

rm -rf "$OUTPUT_DIR"
mkdir -p "$OUTPUT_DIR"

dotnet publish "$PROJECT_FILE" \
    -c "$CONFIG" \
    -p:PublishProfile="$PUBLISH_PROFILE" \
    -p:PublishSingleFile=true \
    -p:SelfContained=true \
    -o "$OUTPUT_DIR" \
    --configfile "$SRCROOT/../nuget.config"

if [ -f "$OUTPUT_DIR/Froststrap" ]; then
    chmod +x "$OUTPUT_DIR/Froststrap"
    echo "Successfully built binary at: $OUTPUT_DIR/Froststrap"
else
    echo "error: expected binary not found at $OUTPUT_DIR/Froststrap" >&2
    exit 1
fi
