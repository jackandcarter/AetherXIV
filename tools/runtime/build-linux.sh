#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
SOURCE_URL="https://media.codeweavers.com/pub/crossover/source/crossover-sources-26.0.0.tar.gz"
SOURCE_SHA256="544d6ef462e5089017340ccf66df0a8cdc117aa9895d91d7c5d10edaa5fcbc56"
PATCH_PATH="${ROOT_DIR}/tools/runtime/patches/crossover-26.0.0-standalone-runtime.patch"

if (($# != 1)); then
  echo "Usage: $0 <package-output-root>" >&2
  echo "Optional: AETHERXIV_WINE_WORK_ROOT, AETHERXIV_WINE_SOURCE_ARCHIVE" >&2
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
archive_path="${AETHERXIV_WINE_SOURCE_ARCHIVE:-${work_root}/crossover-sources-26.0.0.tar.gz}"
source_parent="${work_root}/source"
source_root="${source_parent}/sources/wine"
build_root="${work_root}/build"
install_root="${work_root}/install"

mkdir -p "${work_root}"
if [[ ! -f "${archive_path}" ]]; then
  curl --fail --location --retry 3 --output "${archive_path}.download" "${SOURCE_URL}"
  mv "${archive_path}.download" "${archive_path}"
fi
echo "${SOURCE_SHA256}  ${archive_path}" | sha256sum --check

for command in bison flex make gcc i686-w64-mingw32-gcc x86_64-w64-mingw32-gcc pkg-config; do
  if ! command -v "${command}" >/dev/null 2>&1; then
    echo "Required Wine build tool is missing: ${command}" >&2
    exit 3
  fi
done

# Wine's Vulkan bridge is required for the native-DXVK renderer. Fail
# during the authoritative runtime build instead of shipping a bundle that can
# only discover the problem after a game launch.
if ! pkg-config --exists vulkan || [[ ! -f "$(pkg-config --variable=includedir vulkan 2>/dev/null)/vulkan/vulkan.h" ]]; then
  echo "Vulkan development files are required to build the Linux Wine runtime (pkg-config vulkan + vulkan/vulkan.h)." >&2
  exit 4
fi

rm -rf "${source_parent}" "${build_root}" "${install_root}"
mkdir -p "${source_parent}" "${build_root}" "${install_root}"
tar -xzf "${archive_path}" -C "${source_parent}"
patch -d "${source_root}" -p1 --forward < "${PATCH_PATH}"

(
  cd "${build_root}"
  "${source_root}/configure" \
    --prefix="${install_root}" \
    --enable-archs=i386,x86_64 \
    --disable-tests \
    --with-vulkan \
    --with-pulse
  make -j"$(nproc)"
  make install
)

AETHERXIV_WINE_SOURCE_ROOT="${source_root}" \
  "${ROOT_DIR}/tools/runtime/package-linux.sh" \
  "${install_root}" \
  "${package_output}"

echo "AetherXIV runtime build complete: ${package_output}"
