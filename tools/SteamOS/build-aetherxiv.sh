#!/usr/bin/env bash
set -euo pipefail

# SteamOS shares Linux's runtime ABI, but has its own supported build entry and
# release directory. Reusing the Linux implementation also keeps the Wine/DXVK
# bundle audit identical across both Linux release packages.
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
export AETHERXIV_PLATFORM_NAME="SteamOS"
exec "${SCRIPT_DIR}/../Linux/build-aetherxiv.sh" "$@"
