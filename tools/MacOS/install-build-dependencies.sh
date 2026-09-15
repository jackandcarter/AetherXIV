#!/usr/bin/env bash
set -euo pipefail

DRY_RUN=0
[[ "${1:-}" == "--dry-run" ]] && DRY_RUN=1
if ! command -v brew >/dev/null 2>&1; then
  echo "Homebrew is required to provision a macOS build host; install it first from https://brew.sh/." >&2
  exit 2
fi
run() { if (( DRY_RUN )); then printf '+ %q ' "$@"; printf '\n'; else "$@"; fi; }
run brew install python@3.13 mingw-w64 bison flex pkg-config
echo "Install the pinned .NET SDK from global.json, then re-run the build for verification."
