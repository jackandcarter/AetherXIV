#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
DOTNET_BIN="${DOTNET_BIN:-/usr/local/share/dotnet/dotnet}"
SDK_VERSION="${1:-2.1.0}"
SAMPLE_VERSION="${2:-2.0.0}"
OUTPUT_DIR="${3:-${ROOT_DIR}/artifacts/umbra}"
UMBRA_ROOT="${ROOT_DIR}/AetherXIV Launcher/Umbra"
WORK_ROOT="$(mktemp -d "${TMPDIR:-/tmp}/aetherxiv-umbra-resources.XXXXXX")"

cleanup() {
  rm -rf "${WORK_ROOT}"
}
trap cleanup EXIT

for version in "${SDK_VERSION}" "${SAMPLE_VERSION}"; do
  if [[ ! "${version}" =~ ^[0-9]+\.[0-9]+\.[0-9]+([.-][0-9A-Za-z.-]+)?$ ]]; then
    echo "Invalid release version: ${version}" >&2
    exit 2
  fi
done

SDK_ROOT="${WORK_ROOT}/Umbra-SDK-${SDK_VERSION}"
SAMPLE_ROOT="${WORK_ROOT}/Umbra-SamplePlugin-${SAMPLE_VERSION}"
mkdir -p \
  "${SDK_ROOT}/lib/net10.0" \
  "${SDK_ROOT}/nuget" \
  "${SDK_ROOT}/docs" \
  "${SDK_ROOT}/templates/SamplePlugin" \
  "${SAMPLE_ROOT}"

"${DOTNET_BIN}" build \
  "${UMBRA_ROOT}/Aether.Umbra.PluginApi/Aether.Umbra.PluginApi.csproj" \
  --configuration Release \
  --output "${SDK_ROOT}/lib/net10.0" \
  /p:NuGetAudit=false
"${DOTNET_BIN}" pack \
  "${UMBRA_ROOT}/Aether.Umbra.PluginApi/Aether.Umbra.PluginApi.csproj" \
  --configuration Release \
  --output "${SDK_ROOT}/nuget" \
  /p:PackageVersion="${SDK_VERSION}" \
  /p:NuGetAudit=false
"${DOTNET_BIN}" pack \
  "${UMBRA_ROOT}/Aether.Umbra.Sdk/Aether.Umbra.Sdk.csproj" \
  --configuration Release \
  --output "${SDK_ROOT}/nuget" \
  /p:PackageVersion="${SDK_VERSION}" \
  /p:NuGetAudit=false
"${DOTNET_BIN}" build \
  "${UMBRA_ROOT}/Aether.Umbra.SamplePlugin/Aether.Umbra.SamplePlugin.csproj" \
  --configuration Release \
  --output "${WORK_ROOT}/sample-build" \
  /p:NuGetAudit=false

cp "${ROOT_DIR}/docs/UMBRA_SDK.md" "${SDK_ROOT}/docs/UMBRA_SDK.md"
cp "${ROOT_DIR}/LICENSE" "${SDK_ROOT}/LICENSE"
cp "${UMBRA_ROOT}/packaging/Aether.Umbra.SamplePlugin.csproj" \
  "${SDK_ROOT}/templates/SamplePlugin/Aether.Umbra.SamplePlugin.csproj"
cp "${UMBRA_ROOT}/packaging/NuGet.Config" \
  "${SDK_ROOT}/templates/SamplePlugin/NuGet.Config"
cp "${UMBRA_ROOT}/Aether.Umbra.SamplePlugin/SamplePlugin.cs" \
  "${SDK_ROOT}/templates/SamplePlugin/SamplePlugin.cs"
cp "${UMBRA_ROOT}/Aether.Umbra.SamplePlugin/umbra-plugin.json" \
  "${SDK_ROOT}/templates/SamplePlugin/umbra-plugin.json"
cp "${UMBRA_ROOT}/Aether.Umbra.SamplePlugin/README.md" \
  "${SDK_ROOT}/templates/SamplePlugin/README.md"

cp "${WORK_ROOT}/sample-build/Aether.Umbra.SamplePlugin.dll" "${SAMPLE_ROOT}/"
cp "${UMBRA_ROOT}/Aether.Umbra.SamplePlugin/umbra-plugin.json" "${SAMPLE_ROOT}/"
cp "${UMBRA_ROOT}/Aether.Umbra.SamplePlugin/README.md" "${SAMPLE_ROOT}/"
cp "${ROOT_DIR}/LICENSE" "${SAMPLE_ROOT}/LICENSE"

find "${SDK_ROOT}" -type f -name '*.pdb' -delete
find "${SAMPLE_ROOT}" -type f -name '*.pdb' -delete
mkdir -p "${OUTPUT_DIR}"

(
  cd "${SDK_ROOT}"
  find . -type f -print | LC_ALL=C sort |
    zip -X -q "${OUTPUT_DIR}/Umbra-SDK-${SDK_VERSION}.zip" -@
)
(
  cd "${SAMPLE_ROOT}"
  find . -type f -print | LC_ALL=C sort |
    zip -X -q "${OUTPUT_DIR}/Umbra-SamplePlugin-${SAMPLE_VERSION}.zip" -@
)

(
  cd "${OUTPUT_DIR}"
  if command -v sha256sum >/dev/null 2>&1; then
    sha256sum \
      "Umbra-SDK-${SDK_VERSION}.zip" \
      "Umbra-SamplePlugin-${SAMPLE_VERSION}.zip" > SHA256SUMS
  else
    shasum -a 256 \
      "Umbra-SDK-${SDK_VERSION}.zip" \
      "Umbra-SamplePlugin-${SAMPLE_VERSION}.zip" > SHA256SUMS
  fi
)

echo "Created Umbra developer resources in ${OUTPUT_DIR}"
