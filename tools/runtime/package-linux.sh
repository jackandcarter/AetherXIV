#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
SOURCE_URL="https://media.codeweavers.com/pub/crossover/source/crossover-sources-26.0.0.tar.gz"
SOURCE_SHA256="544d6ef462e5089017340ccf66df0a8cdc117aa9895d91d7c5d10edaa5fcbc56"
RUNTIME_VERSION="26.0.0-wine-11.0-aether.3"
DXVK_VERSION="3.1.1"
DXVK_URL="https://github.com/doitsujin/dxvk/releases/download/v${DXVK_VERSION}/dxvk-${DXVK_VERSION}.tar.gz"
DXVK_SHA256="40565b4a724aadc4433fa4e010b4b23916d9b1f1baeee64e17186db94f54e608"

if (($# != 2)); then
  echo "Usage: $0 <installed-wine-root> <package-output-root>" >&2
  exit 2
fi

install_root="$(cd "$1" && pwd)"
output_root="$2"
case "${output_root}" in
  ""|/|"${HOME}"|"${ROOT_DIR}")
    echo "Refusing unsafe runtime output path: ${output_root}" >&2
    exit 3
    ;;
esac
output_parent="$(cd "$(dirname "${output_root}")" && pwd)"
output_root="${output_parent}/$(basename "${output_root}")"

if [[ ! -x "${install_root}/bin/wine" || ! -x "${install_root}/bin/wineserver" ]]; then
  echo "The input is not an installed Wine runtime: ${install_root}" >&2
  exit 4
fi
if [[ ! -d "${install_root}/lib/wine/i386-windows" \
      || ! -d "${install_root}/lib/wine/x86_64-windows" \
      || ! -d "${install_root}/lib/wine/x86_64-unix" ]]; then
  echo "The installed Wine tree is missing the i386/x86_64 WoW64 payload." >&2
  exit 5
fi

staging_root="$(mktemp -d "${output_parent}/.aetherxiv-runtime.XXXXXX")"
cleanup() {
  rm -rf "${staging_root}"
}
trap cleanup EXIT
runtime_root="${staging_root}/runtime"
dxvk_archive="${AETHERXIV_DXVK_ARCHIVE:-${staging_root}/dxvk-${DXVK_VERSION}.tar.gz}"
if [[ ! -f "${dxvk_archive}" ]]; then
  curl --fail --location --retry 3 --output "${dxvk_archive}.download" "${DXVK_URL}"
  mv "${dxvk_archive}.download" "${dxvk_archive}"
fi
echo "${DXVK_SHA256}  ${dxvk_archive}" | sha256sum --check
bash "${ROOT_DIR}/tools/runtime/verify-dxvk-archive.sh" "${dxvk_archive}"
mkdir -p "${runtime_root}"
cp -a "${install_root}/." "${runtime_root}/"

rm -rf "${runtime_root}/include" "${runtime_root}/share/man"
find "${runtime_root}/bin" -mindepth 1 -maxdepth 1 \
  ! -name wine ! -name wineserver -delete

wine_source_root="${AETHERXIV_WINE_SOURCE_ROOT:-}"
if [[ -z "${wine_source_root}" || ! -f "${wine_source_root}/COPYING.LIB" ]]; then
  echo "AETHERXIV_WINE_SOURCE_ROOT must point to the exact Wine source tree used for this runtime." >&2
  exit 6
fi
mkdir -p "${runtime_root}/licenses"
cp "${wine_source_root}/COPYING.LIB" "${runtime_root}/licenses/Wine-LGPL-2.1.txt"
cp "${wine_source_root}/AUTHORS" "${runtime_root}/licenses/Wine-AUTHORS.txt"
cp "${ROOT_DIR}/tools/runtime/patches/crossover-26.0.0-standalone-runtime.patch" \
  "${runtime_root}/licenses/AetherXIV-Wine-build.patch"

# DXVK is a Windows-side D3D implementation. For this WoW64 runtime only the
# x86 D3D9 DLL is needed; the host NVIDIA/AMD/Intel Vulkan driver remains a
# distribution-provided dependency and is never copied into this package.
dxvk_source="${staging_root}/dxvk-source"
mkdir -p "${dxvk_source}"
tar -xzf "${dxvk_archive}" -C "${dxvk_source}" --strip-components=1
if [[ ! -f "${dxvk_source}/x32/d3d9.dll" ]]; then
  echo "The pinned DXVK archive is missing x32/d3d9.dll." >&2
  exit 7
fi
mkdir -p "${runtime_root}/dxvk/x32" "${runtime_root}/dxvk/probe"
cp "${dxvk_source}/x32/d3d9.dll" "${runtime_root}/dxvk/x32/d3d9.dll"
if ! command -v i686-w64-mingw32-gcc >/dev/null 2>&1; then
  echo "i686-w64-mingw32-gcc is required to build the DXVK D3D9 probe." >&2
  exit 7
fi
i686-w64-mingw32-gcc -O2 \
  "${ROOT_DIR}/tools/runtime/dxvk-probe.c" \
  -o "${runtime_root}/dxvk/probe/AetherXIV.DxvkProbe.exe" \
  -ld3d9 -luser32
# Upstream binary archives do not include LICENSE. Fetch the matching tagged
# source license and verify it independently of the binary payload.
dxvk_license="${staging_root}/DXVK-LICENSE"
curl --fail --location --retry 3 --output "${dxvk_license}" \
  "https://raw.githubusercontent.com/doitsujin/dxvk/v${DXVK_VERSION}/LICENSE"
echo "a5cb1a6ded7d2d7e92d550ba28edd21be2d1d4044662b399887351023e30ce64  ${dxvk_license}" | sha256sum --check
cp "${dxvk_license}" "${runtime_root}/licenses/DXVK-${DXVK_VERSION}.txt"

cat > "${runtime_root}/SOURCE-OFFER.md" <<EOF
# AetherXIV compatibility runtime source

This package contains Wine built from the open-source components published for
CodeWeavers CrossOver 26.0.0. It does not contain or redistribute the CrossOver
application.

- Source: ${SOURCE_URL}
- Source SHA-256: ${SOURCE_SHA256}
- DXVK: ${DXVK_URL}
- DXVK SHA-256: ${DXVK_SHA256}
- AetherXIV patch: licenses/AetherXIV-Wine-build.patch
- Build recipe: tools/runtime/build-linux.sh in the AetherXIV source tree
- Wine license: licenses/Wine-LGPL-2.1.txt
EOF

cat > "${runtime_root}/aetherxiv-runtime.json" <<EOF
{
  "schema": "aetherxiv.compatibility-runtime.v1",
  "name": "AetherXIV Compatibility Runtime",
  "version": "${RUNTIME_VERSION}",
  "platformRid": "linux-x64-wow64",
  "runtimeKind": "wine",
  "executableRelativePath": "bin/wine",
  "wineserverRelativePath": "bin/wineserver",
  "hostLibrariesRelativePath": null,
  "prefixArch": "wow64",
  "sourceUrl": "${SOURCE_URL}",
  "sourceSha256": "${SOURCE_SHA256}",
  "vulkanBridge": "winevulkan",
  "dxvkVersion": "${DXVK_VERSION}",
  "dxvkArchiveSha256": "${DXVK_SHA256}",
  "dxvkD3d9RelativePath": "dxvk/x32/d3d9.dll",
  "dxvkProbeRelativePath": "dxvk/probe/AetherXIV.DxvkProbe.exe",
  "environment": {
    "WINEDEBUG": "-all"
  }
}
EOF

for required_path in \
  "${runtime_root}/lib/wine/i386-windows/winevulkan.dll" \
  "${runtime_root}/lib/wine/x86_64-windows/winevulkan.dll" \
  "${runtime_root}/lib/wine/x86_64-unix/winevulkan.so" \
  "${runtime_root}/lib/wine/i386-windows/d3d9.dll" \
  "${runtime_root}/dxvk/x32/d3d9.dll" \
  "${runtime_root}/dxvk/probe/AetherXIV.DxvkProbe.exe"; do
  if [[ ! -f "${required_path}" ]]; then
    echo "The packaged Linux runtime is missing ${required_path#"${runtime_root}/"}." >&2
    exit 8
  fi
done

# The shared audit resolves Wine's internal Unix libraries from this bundle
# and reports the complete failing linkage output.
bash "${ROOT_DIR}/tools/runtime/audit-linux-bundle.sh" "${runtime_root}"
"${runtime_root}/bin/wine" --version | grep -F 'wine-11.0' >/dev/null
test -f "${runtime_root}/lib/wine/x86_64-windows/wow64cpu.dll"

(
  cd "${runtime_root}"
  find . -type f ! -name aetherxiv-runtime.sha256 -print0 \
    | sort -z \
    | xargs -0 sha256sum > aetherxiv-runtime.sha256
)

rm -rf "${output_root}"
mv "${runtime_root}" "${output_root}"
echo "Packaged AetherXIV compatibility runtime: ${output_root}"
