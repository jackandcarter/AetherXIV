#!/usr/bin/env bash
set -euo pipefail

# SteamOS is immutable. Build in an Ubuntu/Debian distrobox rather than
# mutating the gaming host. The Linux installer runs inside that container.
if ! command -v distrobox >/dev/null 2>&1; then
  echo "SteamOS builds require distrobox. Install it from Discover/Flatpak, then re-run this command." >&2
  exit 2
fi
if ! distrobox list | grep -q 'aetherxiv-build'; then
  distrobox create --yes --name aetherxiv-build --image ubuntu:24.04
fi
distrobox enter aetherxiv-build -- bash -lc 'cd "'$PWD'" && ./tools/Linux/install-build-dependencies.sh "$@"'
