#!/usr/bin/env bash
set -e

VERSION="0.8.0"
MOD_NAME="SpireAdvisor"
SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
RELEASE_DIR="$SCRIPT_DIR/release/$MOD_NAME"
ZIP_NAME="${MOD_NAME}-v${VERSION}.zip"

echo "================================================"
echo "  $MOD_NAME v$VERSION - Release Package"
echo "================================================"

# Build first
echo "[1/4] Building..."
cd "$SCRIPT_DIR/QuestceSpire"
bash build.sh
if [ $? -ne 0 ]; then
    echo "BUILD FAILED!"
    exit 1
fi

# Find game mods folder from local.props
GAME_PATH=$(grep 'STS2GamePath' local.props | sed 's/.*>\(.*\)<.*/\1/')
MODS_SRC="$GAME_PATH/mods/$MOD_NAME"

echo "[2/4] Collecting files from $MODS_SRC..."

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

echo "[3/4] Creating $ZIP_NAME..."
cd "$SCRIPT_DIR/release"
zip -r "$SCRIPT_DIR/$ZIP_NAME" "$MOD_NAME"

echo "[4/4] Done!"
echo
echo "Release: $SCRIPT_DIR/$ZIP_NAME"
echo
echo "Upload this to GitHub Releases."
echo
ls -l "$SCRIPT_DIR/$ZIP_NAME"
