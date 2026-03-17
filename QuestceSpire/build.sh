#!/usr/bin/env bash
set -e

echo "================================================"
echo "  Spire Advisor - Build"
echo "================================================"
echo

cd "$(dirname "$0")"

if [ ! -f "local.props" ]; then
    echo "[ERROR] local.props not found."
    echo "Copy local.props.example to local.props and edit paths."
    exit 1
fi

echo "[INFO] Building..."
echo

if dotnet build --configuration Release; then
    echo
    echo "[OK] Deployed to game mods folder."
    echo "Start STS2 to test."
else
    echo
    echo "[ERROR] Build failed."
    exit 1
fi

echo
echo "Done."
