#!/usr/bin/env bash
set -euo pipefail
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
export AETHERXIV_PLATFORM_NAME=SteamOS
exec "${SCRIPT_DIR}/../Linux/build-core-only.sh" "$@"
