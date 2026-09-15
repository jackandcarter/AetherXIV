#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
WINE_SOURCE_URL="https://dl.winehq.org/wine/source/11.0/wine-11.0.tar.xz"
WINE_SOURCE_SHA256="c07a6857933c1fc60dff5448d79f39c92481c1e9db5aa628db9d0358446e0701"
WINE_RUNTIME_URL="https://github.com/Gcenx/macOS_Wine_builds/releases/download/11.0_1/wine-stable-11.0_1-osx64.tar.xz"
WINE_RUNTIME_SHA256="b50dc50ec7f41d58b115a6b685d4d1315ba3c797bd3aa0f49213f2703cb82388"
PATCH_SOURCE_URL="https://media.codeweavers.com/pub/crossover/source/crossover-sources-26.0.0.tar.gz"
RUNTIME_VERSION="11.0_1-aether.3"

usage() {
  echo "Usage: $0 <installed-wine-root> <package-output-root>" >&2
  echo "Set AETHERXIV_WINE_SOURCE_ROOT to include the exact Wine license and source metadata." >&2
}

if (($# != 2)); then
  usage
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
if ! file "${install_root}/bin/wine" | grep -Fq 'x86_64'; then
  echo "The macOS Wine loader must be x86_64 so Apple silicon can run it through Rosetta." >&2
  exit 5
fi
if [[ ! -d "${install_root}/lib/wine/i386-windows" \
      || ! -d "${install_root}/lib/wine/x86_64-windows" \
      || ! -d "${install_root}/lib/wine/x86_64-unix" ]]; then
  echo "The WineHQ runtime is missing the i386/x86_64 WoW64 payload." >&2
  exit 6
fi

staging_root="$(mktemp -d "${output_parent}/.aetherxiv-runtime.XXXXXX")"
cleanup() {
  rm -rf "${staging_root}"
}
trap cleanup EXIT

if [[ "${AETHERXIV_PACKAGE_WINE_IN_PLACE:-0}" == 1 ]]; then
  install_device="$(stat -f '%d' "${install_root}")"
  output_device="$(stat -f '%d' "${output_parent}")"
  if [[ "${install_device}" != "${output_device}" ]]; then
    echo "In-place runtime packaging requires its work and output roots on the same volume." >&2
    exit 7
  fi
  runtime_root="${install_root}"
else
  runtime_root="${staging_root}/runtime"
  mkdir -p "${runtime_root}"
  if cp -cR "${install_root}/." "${runtime_root}/" 2>/dev/null; then
    :
  else
    cp -R "${install_root}/." "${runtime_root}/"
  fi
fi

rm -rf "${runtime_root}/include" "${runtime_root}/share/man" "${runtime_root}/lib/host"
find "${runtime_root}/bin" -mindepth 1 -maxdepth 1 \
  ! -name wine ! -name wineserver -delete

host_root="${runtime_root}/lib"
license_root="${runtime_root}/licenses"
mkdir -p "${license_root}"
if [[ ! -f "${host_root}/libMoltenVK.dylib" ]]; then
  echo "The pinned WineHQ runtime is missing its MoltenVK dependency." >&2
  exit 8
fi

wine_source_root="${AETHERXIV_WINE_SOURCE_ROOT:-}"
if [[ -z "${wine_source_root}" || ! -f "${wine_source_root}/COPYING.LIB" ]]; then
  echo "AETHERXIV_WINE_SOURCE_ROOT must point to the exact Wine source metadata used for this runtime." >&2
  exit 9
fi
cp "${wine_source_root}/COPYING.LIB" "${license_root}/Wine-LGPL-2.1.txt"
cp "${wine_source_root}/AUTHORS" "${license_root}/Wine-AUTHORS.txt"
cp "${ROOT_DIR}/tools/runtime/patches/wine-11.0-rosetta-wow64.patch" \
  "${license_root}/AetherXIV-Wine-build.patch"
cp "${ROOT_DIR}/THIRD_PARTY_NOTICES.md" "${license_root}/AetherXIV-THIRD-PARTY-NOTICES.md"

cat > "${runtime_root}/SOURCE-OFFER.md" <<EOF
# AetherXIV compatibility runtime source

This package uses the official WineHQ macOS Wine Stable 11.0_1 binary release,
with one rebuilt Wine module containing the minimal Rosetta 2 WoW64 transition
workaround required by Umbra's Windows x86 .NET 10 runtime.

- Wine source: ${WINE_SOURCE_URL}
- Wine source SHA-256: ${WINE_SOURCE_SHA256}
- WineHQ macOS runtime: ${WINE_RUNTIME_URL}
- WineHQ macOS runtime SHA-256: ${WINE_RUNTIME_SHA256}
- Rosetta workaround source: ${PATCH_SOURCE_URL}
- AetherXIV patch: licenses/AetherXIV-Wine-build.patch
- Build recipe: tools/runtime/build-macos.sh in the AetherXIV source tree
- Wine license: licenses/Wine-LGPL-2.1.txt

The exact corresponding source archive, patch, and build instructions are
available from the URLs and AetherXIV source tree above.
EOF

cat > "${runtime_root}/aetherxiv-runtime.json" <<EOF
{
  "schema": "aetherxiv.compatibility-runtime.v1",
  "name": "AetherXIV Compatibility Runtime",
  "version": "${RUNTIME_VERSION}",
  "platformRid": "osx-x64-wow64",
  "runtimeKind": "wine",
  "executableRelativePath": "bin/wine",
  "wineserverRelativePath": "bin/wineserver",
  "hostLibrariesRelativePath": "lib",
  "prefixArch": "wow64",
  "sourceUrl": "${WINE_SOURCE_URL}",
  "sourceSha256": "${WINE_SOURCE_SHA256}",
  "binarySourceUrl": "${WINE_RUNTIME_URL}",
  "binarySourceSha256": "${WINE_RUNTIME_SHA256}",
  "environment": {
    "WINEDEBUG": "-all"
  }
}
EOF

find "${runtime_root}/bin" "${runtime_root}/lib" -type f -print0 \
  | while IFS= read -r -d '' executable; do
      if file "${executable}" | grep -Fq 'Mach-O'; then
        codesign --force --sign - "${executable}"
      fi
    done

for pe_module in \
  "${runtime_root}/lib/wine/i386-windows/wined3d.dll" \
  "${runtime_root}/lib/wine/i386-windows/d3d9.dll" \
  "${runtime_root}/lib/wine/i386-windows/xaudio2_7.dll" \
  "${runtime_root}/lib/wine/x86_64-windows/wow64cpu.dll"; do
  if objdump -h "${pe_module}" | grep -Eq '[.]debug_(info|line|abbrev|str)'; then
    echo "The packaged runtime contains development debug sections: ${pe_module}" >&2
    exit 10
  fi
done

(
  cd "${runtime_root}"
  find . -type f ! -name aetherxiv-runtime.sha256 -print0 \
    | sort -z \
    | xargs -0 shasum -a 256 > aetherxiv-runtime.sha256
)

DYLD_FALLBACK_LIBRARY_PATH="${host_root}" \
  "${runtime_root}/bin/wine" --version | grep -Fq 'wine-11.0'

prefix_smoke_root="${staging_root}/prefix-smoke"
mkdir -p "${prefix_smoke_root}"
set +e
DYLD_FALLBACK_LIBRARY_PATH="${host_root}" \
  WINEDEBUG=-all \
  WINEDLLOVERRIDES='winedbg.exe=d;mshtml=' \
  WINEARCH=wow64 \
  WINEPREFIX="${prefix_smoke_root}" \
  /usr/bin/perl -e 'alarm shift; exec @ARGV' 300 \
    "${runtime_root}/bin/wine" wineboot -u
prefix_smoke_status=$?
set -e
DYLD_FALLBACK_LIBRARY_PATH="${host_root}" \
  WINEDEBUG=-all \
  WINEPREFIX="${prefix_smoke_root}" \
  "${runtime_root}/bin/wineserver" -k >/dev/null 2>&1 || true
if [[ "${prefix_smoke_status}" != 0 ]]; then
  echo "The packaged runtime could not initialize a fresh WoW64 prefix (exit ${prefix_smoke_status})." >&2
  exit 11
fi

previous_output="${output_root}.previous"
rm -rf "${previous_output}"
if [[ -e "${output_root}" ]]; then
  mv "${output_root}" "${previous_output}"
fi
if mv "${runtime_root}" "${output_root}"; then
  rm -rf "${previous_output}"
else
  if [[ -e "${previous_output}" && ! -e "${output_root}" ]]; then
    mv "${previous_output}" "${output_root}"
  fi
  exit 12
fi

echo "Packaged AetherXIV compatibility runtime: ${output_root}"
