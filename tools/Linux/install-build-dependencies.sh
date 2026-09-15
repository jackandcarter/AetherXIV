#!/usr/bin/env bash
set -euo pipefail

# Explicitly opt-in prerequisite installer. Build scripts never invoke a
# package manager unless their caller passes --install-dependencies.
DRY_RUN=0
[[ "${1:-}" == "--dry-run" ]] && DRY_RUN=1
run() { if (( DRY_RUN )); then printf '+ %q ' "$@"; printf '\n'; else "$@"; fi; }

packages=(bison build-essential flex g++-mingw-w64-i686 gcc-mingw-w64-i686
  gcc-mingw-w64-x86-64 libfreetype-dev libgl-dev libgnutls28-dev libx11-dev
  libxext-dev libxfixes-dev libxi-dev libxrandr-dev libxrender-dev python3
  xz-utils xvfb)

if command -v apt-get >/dev/null; then
  run sudo apt-get update
  run sudo apt-get install --yes "${packages[@]}"
elif command -v dnf >/dev/null; then
  run sudo dnf install --assumeyes dotnet-sdk-10.0 python3 wine-core mingw32-gcc-c++ mingw64-gcc
elif command -v pacman >/dev/null; then
  run sudo pacman -Sy --needed --noconfirm dotnet-sdk python mingw-w64-gcc wine
else
  echo "Unsupported Linux package manager. Install the dependencies listed in docs/build/LINUX.md." >&2
  exit 2
fi

echo "Dependency installation completed. Re-run the build so it can verify the pinned .NET SDK and toolchain names."
