#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
SOURCE_URL="https://media.codeweavers.com/pub/crossover/source/crossover-sources-26.0.0.tar.gz"
SOURCE_SHA256="544d6ef462e5089017340ccf66df0a8cdc117aa9895d91d7c5d10edaa5fcbc56"
RUNTIME_VERSION="26.0.0-wine-11.0-aether.2"

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

cat > "${runtime_root}/SOURCE-OFFER.md" <<EOF
# AetherXIV compatibility runtime source

This package contains Wine built from the open-source components published for
CodeWeavers CrossOver 26.0.0. It does not contain or redistribute the CrossOver
application.

- Source: ${SOURCE_URL}
- Source SHA-256: ${SOURCE_SHA256}
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
  "environment": {
    "WINEDEBUG": "-all"
  }
}
EOF

if ldd "${runtime_root}/bin/wine" "${runtime_root}/bin/wineserver" | grep -Fq 'not found'; then
  echo "The packaged Linux runtime has unresolved host libraries." >&2
  ldd "${runtime_root}/bin/wine" "${runtime_root}/bin/wineserver" >&2 || true
  exit 7
fi
"${runtime_root}/bin/wine" --version | grep -Fq 'wine-11.0'
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
