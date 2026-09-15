#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
DOTNET_BIN="${DOTNET_BIN:-dotnet}"
CONFIGURATION="${1:-Release}"
[[ "${CONFIGURATION}" == Debug || "${CONFIGURATION}" == Release ]] || { echo "Usage: $0 [Debug|Release]" >&2; exit 2; }
PLATFORM_NAME="${AETHERXIV_PLATFORM_NAME:-Linux}"
OUTPUT_ROOT="${ROOT_DIR}/bin/build/${CONFIGURATION}/${PLATFORM_NAME}-Core"
STAGING_ROOT="${OUTPUT_ROOT}.staging"
BUILD_NUMBER="$(tr -d '[:space:]' < "${ROOT_DIR}/build-number.txt")"

command -v "${DOTNET_BIN}" >/dev/null || { echo "dotnet is required; run tools/${PLATFORM_NAME}/install-build-dependencies.sh." >&2; exit 40; }
"${DOTNET_BIN}" --list-sdks | awk '{print $1}' | grep -Fxq '10.0.203' || { echo "Pinned .NET SDK 10.0.203 is required." >&2; exit 40; }
command -v python3 >/dev/null || { echo "python3 is required." >&2; exit 40; }

rm -rf "${STAGING_ROOT}"
mkdir -p "${STAGING_ROOT}"
cleanup() { rm -rf "${STAGING_ROOT}"; }
trap cleanup EXIT
publish() {
  "${DOTNET_BIN}" publish "$1" --configuration "${CONFIGURATION}" --runtime "${AETHERXIV_SERVER_RID:-linux-x64}" --self-contained false --output "$2" -m:1 /nodeReuse:false /p:NuGetAudit=false /p:UseAppHost=true
}
python3 "${ROOT_DIR}/tools/Universal/create-direct-core-database-package.py" --repo-root "${ROOT_DIR}" --output-dir "${STAGING_ROOT}/Database"
publish "${ROOT_DIR}/src/AetherXIV.Core.Map/AetherXIV.Core.Map.csproj" "${STAGING_ROOT}/servers/map"
publish "${ROOT_DIR}/src/AetherXIV.Core.World/AetherXIV.Core.World.csproj" "${STAGING_ROOT}/servers/world"
publish "${ROOT_DIR}/src/AetherXIV.Core.Lobby/AetherXIV.Core.Lobby.csproj" "${STAGING_ROOT}/servers/lobby"
publish "${ROOT_DIR}/src/AetherXIV.Launcher.Host/AetherXIV.Launcher.Host.csproj" "${STAGING_ROOT}/servers/launcher-services"
"${DOTNET_BIN}" publish "${ROOT_DIR}/src/AetherXIV.UI.App/AetherXIV.UI.App.csproj" --configuration "${CONFIGURATION}" --runtime "${AETHERXIV_LAUNCHER_RID:-linux-x64}" --self-contained true --output "${STAGING_ROOT}/core/app" -m:1 /nodeReuse:false /p:NuGetAudit=false /p:UseAppHost=true
python3 "${ROOT_DIR}/tools/Universal/lua-tree-manifest.py" --scripts-root "${STAGING_ROOT}/servers/map/scripts" --manifest "${STAGING_ROOT}/servers/map/scripts.manifest.json" --write
cp "${ROOT_DIR}/LICENSE" "${ROOT_DIR}/THIRD_PARTY_NOTICES.md" "${ROOT_DIR}/MODIFICATIONS.md" "${ROOT_DIR}/TRADEMARKS.md" "${STAGING_ROOT}/"
printf 'schema=aetherxiv.build.manifest.v1\nproduct_version=2.1\nbuild_number=%s\nscope=core\nplatform=%s\n' "${BUILD_NUMBER}" "${PLATFORM_NAME}" > "${STAGING_ROOT}/build-manifest.txt"
rm -rf "${OUTPUT_ROOT}.previous"
[[ -d "${OUTPUT_ROOT}" ]] && mv "${OUTPUT_ROOT}" "${OUTPUT_ROOT}.previous"
mv "${STAGING_ROOT}" "${OUTPUT_ROOT}"
trap - EXIT
rm -rf "${OUTPUT_ROOT}.previous"
echo "AetherXIV ${PLATFORM_NAME} core package: ${OUTPUT_ROOT}"
