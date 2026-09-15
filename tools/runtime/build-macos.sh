#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
WINE_SOURCE_URL="https://dl.winehq.org/wine/source/11.0/wine-11.0.tar.xz"
WINE_SOURCE_SHA256="c07a6857933c1fc60dff5448d79f39c92481c1e9db5aa628db9d0358446e0701"
WINE_RUNTIME_URL="https://github.com/Gcenx/macOS_Wine_builds/releases/download/11.0_1/wine-stable-11.0_1-osx64.tar.xz"
WINE_RUNTIME_SHA256="b50dc50ec7f41d58b115a6b685d4d1315ba3c797bd3aa0f49213f2703cb82388"
PATCH_PATH="${ROOT_DIR}/tools/runtime/patches/wine-11.0-rosetta-wow64.patch"

usage() {
  echo "Usage: $0 <package-output-root>" >&2
  echo "Optional: AETHERXIV_WINE_WORK_ROOT, AETHERXIV_WINE_SOURCE_ARCHIVE, AETHERXIV_WINE_RUNTIME_ARCHIVE" >&2
}

if (($# != 1)); then
  usage
  exit 2
fi

package_output="$1"
remove_work_root=0
if [[ -n "${AETHERXIV_WINE_WORK_ROOT:-}" ]]; then
  work_root="${AETHERXIV_WINE_WORK_ROOT}"
else
  work_root="$(mktemp -d)"
  remove_work_root=1
fi
cleanup_build_work() {
  if [[ "${remove_work_root}" == 1 ]]; then
    rm -rf "${work_root}"
  fi
}
trap cleanup_build_work EXIT

source_archive="${AETHERXIV_WINE_SOURCE_ARCHIVE:-${work_root}/wine-11.0.tar.xz}"
runtime_archive="${AETHERXIV_WINE_RUNTIME_ARCHIVE:-${work_root}/wine-stable-11.0_1-osx64.tar.xz}"
source_parent="${work_root}/source"
source_root="${source_parent}/wine-11.0"
runtime_parent="${work_root}/runtime"
runtime_root="${runtime_parent}/Wine Stable.app/Contents/Resources/wine"
build_root="${work_root}/build"

download_archive() {
  local url="$1"
  local destination="$2"
  if [[ ! -f "${destination}" ]]; then
    curl --fail --location --retry 3 --output "${destination}.download" "${url}"
    mv "${destination}.download" "${destination}"
  fi
}

mkdir -p "${work_root}"
download_archive "${WINE_SOURCE_URL}" "${source_archive}"
download_archive "${WINE_RUNTIME_URL}" "${runtime_archive}"
echo "${WINE_SOURCE_SHA256}  ${source_archive}" | shasum -a 256 --check
echo "${WINE_RUNTIME_SHA256}  ${runtime_archive}" | shasum -a 256 --check

rm -rf "${source_parent}" "${runtime_parent}" "${build_root}"
mkdir -p "${source_parent}" "${runtime_parent}" "${build_root}"
tar -xJf "${source_archive}" -C "${source_parent}"
tar -xJf "${runtime_archive}" -C "${runtime_parent}"
patch -d "${source_root}" -p1 --forward < "${PATCH_PATH}"

if [[ ! -x "${runtime_root}/bin/wine" \
      || ! -f "${runtime_root}/lib/wine/x86_64-windows/wow64cpu.dll" ]]; then
  echo "The pinned WineHQ macOS runtime archive is incomplete." >&2
  exit 3
fi

bison_bin="${AETHERXIV_BISON_BIN:-}"
if [[ -z "${bison_bin}" ]]; then
  for candidate in /usr/local/opt/bison/bin/bison /opt/homebrew/opt/bison/bin/bison; do
    if [[ -x "${candidate}" ]]; then
      bison_bin="${candidate}"
      break
    fi
  done
fi
if [[ -z "${bison_bin}" || ! -x "${bison_bin}" ]]; then
  echo "Bison 3 is required. Set AETHERXIV_BISON_BIN to its executable." >&2
  exit 4
fi

mingw_bin="${AETHERXIV_MINGW_BIN:-}"
if [[ -z "${mingw_bin}" ]]; then
  for candidate in /usr/local/opt/mingw-w64/bin "$(dirname "$(command -v i686-w64-mingw32-gcc 2>/dev/null || printf '/missing')")"; do
    if [[ -x "${candidate}/i686-w64-mingw32-gcc" \
        && -x "${candidate}/x86_64-w64-mingw32-gcc" \
        && $(file "${candidate}/i686-w64-mingw32-gcc") == *x86_64* \
        && $(file "${candidate}/x86_64-w64-mingw32-gcc") == *x86_64* ]]; then
      mingw_bin="${candidate}"
      break
    fi
  done
fi
if [[ -z "${mingw_bin}" ]]; then
  echo "Intel x86_64 builds of both i686 and x86_64 MinGW-w64 compilers are required." >&2
  exit 5
fi

build_path="$(dirname "${bison_bin}"):${mingw_bin}:${PATH}"
(
  cd "${build_root}"
  arch -x86_64 env \
    PATH="${build_path}" \
    CC=/usr/bin/clang \
    CXX=/usr/bin/clang++ \
    CFLAGS="-O2" \
    CXXFLAGS="-O2" \
    PKG_CONFIG_PATH="${AETHERXIV_X86_PKG_CONFIG_PATH:-/usr/local/lib/pkgconfig:/usr/local/share/pkgconfig}" \
    "${source_root}/configure" \
      --enable-archs=i386,x86_64 \
      --disable-tests \
      --without-x
  arch -x86_64 env PATH="${build_path}" \
    make -j"$(sysctl -n hw.logicalcpu)" dlls/wow64cpu/x86_64-windows/wow64cpu.dll
)

patched_wow64cpu="${build_root}/dlls/wow64cpu/x86_64-windows/wow64cpu.dll"
"${mingw_bin}/x86_64-w64-mingw32-strip" --strip-debug "${patched_wow64cpu}"
cp "${patched_wow64cpu}" "${runtime_root}/lib/wine/x86_64-windows/wow64cpu.dll"

source_metadata_root="${work_root}/source-metadata"
mkdir -p "${source_metadata_root}"
cp "${source_root}/COPYING.LIB" "${source_root}/AUTHORS" "${source_metadata_root}/"

# Packaging needs only the verified WineHQ runtime, the patched module, and
# source attribution. Releasing the object/source trees keeps ample headroom
# for checksum generation and a fresh-prefix smoke test.
rm -rf "${build_root}" "${source_parent}"
if [[ "${source_archive}" == "${work_root}/"* ]]; then
  rm -f "${source_archive}"
fi
if [[ "${runtime_archive}" == "${work_root}/"* ]]; then
  rm -f "${runtime_archive}"
fi

AETHERXIV_WINE_SOURCE_ROOT="${source_metadata_root}" \
  AETHERXIV_PACKAGE_WINE_IN_PLACE=1 \
  "${ROOT_DIR}/tools/runtime/package-macos.sh" \
  "${runtime_root}" \
  "${package_output}"

echo "AetherXIV runtime build complete: ${package_output}"
