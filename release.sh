#!/usr/bin/env bash
set -e

MOD_NAME="SpireAdvisor"
SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"

# Read version from csproj (single source of truth)
VERSION=$(grep '<ModVersion>' "$SCRIPT_DIR/QuestceSpire/QuestceSpire.csproj" | sed 's/.*<ModVersion>\(.*\)<\/ModVersion>.*/\1/')
RELEASE_DIR="$SCRIPT_DIR/release/$MOD_NAME"
ZIP_NAME="${MOD_NAME}-v${VERSION}.zip"

echo "================================================"
echo "  $MOD_NAME v$VERSION - Release Package"
echo "================================================"

# Build first
echo "[1/5] Building..."
cd "$SCRIPT_DIR/QuestceSpire"
bash build.sh
if [ $? -ne 0 ]; then
    echo "BUILD FAILED!"
    exit 1
fi

# Find game mods folder from local.props
if [ ! -f local.props ]; then
    echo "ERROR: local.props not found. Copy local.props.example and configure paths."
    exit 1
fi
GAME_PATH=$(grep 'STS2GamePath' local.props | sed 's/.*>\(.*\)<.*/\1/')
MODS_SRC="$GAME_PATH/mods/$MOD_NAME"

echo "[2/5] Collecting files from $MODS_SRC..."

# Clean and create release dir
rm -rf "$SCRIPT_DIR/release"
mkdir -p "$RELEASE_DIR"

# Copy DLL
cp "$MODS_SRC/$MOD_NAME.dll" "$RELEASE_DIR/"
# Copy PCK
cp "$MODS_SRC/$MOD_NAME.pck" "$RELEASE_DIR/"
# Copy all NuGet/native DLLs (except the main mod DLL)
for f in "$MODS_SRC"/*.dll; do
    [ "$(basename "$f")" = "$MOD_NAME.dll" ] && continue
    cp "$f" "$RELEASE_DIR/"
done
# Copy Data
cp -r "$MODS_SRC/Data" "$RELEASE_DIR/Data"

# Validate release artifacts
echo "[3/5] Validating..."
MISSING=0
for f in "$RELEASE_DIR/$MOD_NAME.dll" "$RELEASE_DIR/$MOD_NAME.pck" "$RELEASE_DIR/Data"; do
    if [ ! -e "$f" ]; then
        echo "ERROR: Missing required file: $f"
        MISSING=1
    fi
done
if [ $MISSING -eq 1 ]; then
    echo "Release validation failed!"
    exit 1
fi
echo "All required files present."

echo "[4/5] Creating $ZIP_NAME..."
cd "$SCRIPT_DIR/release"
zip -r "$SCRIPT_DIR/$ZIP_NAME" "$MOD_NAME"

echo "[5/5] Done!"
echo
echo "Release: $SCRIPT_DIR/$ZIP_NAME"
echo
echo "Upload this to GitHub Releases."
echo
ls -l "$SCRIPT_DIR/$ZIP_NAME"
