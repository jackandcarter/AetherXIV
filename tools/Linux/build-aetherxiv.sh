#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
DOTNET_BIN="${DOTNET_BIN:-dotnet}"
CONFIGURATION="${AETHERXIV_BUILD_CONFIGURATION:-Release}"
BUILD_SCOPE="full"
INSTALL_DEPENDENCIES=0
PLATFORM_NAME="${AETHERXIV_PLATFORM_NAME:-Linux}"
BUILD_NUMBER="$(tr -d '[:space:]' < "${ROOT_DIR}/build-number.txt")"
while (($# > 0)); do
  case "$1" in
    Debug|Release) CONFIGURATION="$1" ;;
    --scope) shift; BUILD_SCOPE="${1:-}" ;;
    --install-dependencies) INSTALL_DEPENDENCIES=1 ;;
    *) echo "Usage: $0 [Debug|Release] [--scope core|full] [--install-dependencies]" >&2; exit 2 ;;
  esac
  shift
done
case "${CONFIGURATION}" in Debug|Release) ;; *) echo "Configuration must be Debug or Release." >&2; exit 2 ;; esac
case "${BUILD_SCOPE}" in core|full) ;; *) echo "--scope must be core or full." >&2; exit 2 ;; esac
if (( INSTALL_DEPENDENCIES )); then
  "${ROOT_DIR}/tools/${PLATFORM_NAME}/install-build-dependencies.sh"
fi
if [[ "${BUILD_SCOPE}" == core ]]; then
  exec "${ROOT_DIR}/tools/${PLATFORM_NAME}/build-core-only.sh" "${CONFIGURATION}"
fi
FINAL_OUTPUT_ROOT="${ROOT_DIR}/bin/build/${CONFIGURATION}/${PLATFORM_NAME}"
OUTPUT_ROOT="${ROOT_DIR}/bin/build/${CONFIGURATION}/.${PLATFORM_NAME}.staging"
SERVER_RID="${AETHERXIV_SERVER_RID:-linux-x64}"
LAUNCHER_RID="${AETHERXIV_LAUNCHER_RID:-${SERVER_RID}}"
UMBRA_RID="${AETHERXIV_UMBRA_RID:-win-x86}"
UMBRA_VERSION="${AETHERXIV_UMBRA_VERSION:-2.0.0}"
COMPATIBILITY_RUNTIME_ROOT="${AETHERXIV_WINE_RUNTIME_ROOT:-}"
LAUNCHER_ROOT="${ROOT_DIR}/AetherXIV Launcher"
RELEASE_WORK_ROOT="${ROOT_DIR}/bin/build/.work/${CONFIGURATION}/${PLATFORM_NAME}"
export AetherXivWorkRoot="${RELEASE_WORK_ROOT}"
BUILD_COMPLETED=0

cleanup_release_work() {
  rm -rf "${RELEASE_WORK_ROOT}"
  rmdir "${ROOT_DIR}/bin/build/.work/${CONFIGURATION}" 2>/dev/null || true
  rmdir "${ROOT_DIR}/bin/build/.work" 2>/dev/null || true
  if [[ "${BUILD_COMPLETED}" != 1 ]]; then
    rm -rf "${OUTPUT_ROOT}"
  fi
}
trap cleanup_release_work EXIT
if [[ -n "${AETHERXIV_HELPER_RIDS:-}" ]]; then
  HELPER_RIDS="${AETHERXIV_HELPER_RIDS}"
elif [[ -n "${AETHERXIV_HELPER_RID:-}" ]]; then
  HELPER_RIDS="${AETHERXIV_HELPER_RID}"
else
  HELPER_RIDS="win-x64"
fi

publish_project() {
  local project_path="$1"
  local output_path="$2"
  shift 2

  publish_project_common "${project_path}" "${output_path}" false "$@"
}

publish_self_contained_project() {
  local project_path="$1"
  local output_path="$2"
  shift 2

  publish_project_common "${project_path}" "${output_path}" true "$@"
}

publish_project_common() {
  local project_path="$1"
  local output_path="$2"
  local self_contained="$3"
  shift 3
  local symbol_args=()
  if [[ "${CONFIGURATION}" == Release ]]; then
    symbol_args=(/p:DebugType=None /p:DebugSymbols=false)
  fi

  mkdir -p "${output_path}"
  "${DOTNET_BIN}" publish "${project_path}" \
    --configuration "${CONFIGURATION}" \
    --self-contained "${self_contained}" \
    --output "${output_path}" \
    -m:1 \
    /nodeReuse:false \
    /p:NuGetAudit=false \
    /p:PublishSingleFile=false \
    /p:UseAppHost=true \
    "${symbol_args[@]}" \
    "$@"
}

reset_output_root() {
  mkdir -p "${ROOT_DIR}/bin/build"
  # Build into an isolated sibling. The last verified release remains intact
  # until this package has passed every check.
  rm -rf "${OUTPUT_ROOT}"
  mkdir -p "${OUTPUT_ROOT}"
}

require_compatibility_runtime() {
  if [[ -z "${COMPATIBILITY_RUNTIME_ROOT}" ]]; then
    echo "AETHERXIV_WINE_RUNTIME_ROOT is unset; building the AetherXIV compatibility runtime..."
    COMPATIBILITY_RUNTIME_ROOT="${OUTPUT_ROOT}/compatibility-runtime"
    "${ROOT_DIR}/tools/runtime/build-linux.sh" "${COMPATIBILITY_RUNTIME_ROOT}"
  fi

  if [[ ! -x "${COMPATIBILITY_RUNTIME_ROOT}/bin/wine" \
      || ! -f "${COMPATIBILITY_RUNTIME_ROOT}/aetherxiv-runtime.json" \
      || ! -f "${COMPATIBILITY_RUNTIME_ROOT}/aetherxiv-runtime.sha256" ]]; then
    echo "Packaged AetherXIV Linux compatibility runtime is missing or invalid: ${COMPATIBILITY_RUNTIME_ROOT}" >&2
    echo "Build it first or point AETHERXIV_WINE_RUNTIME_ROOT at a valid runtime package." >&2
    exit 40
  fi
  if ! python3 -c 'import json,sys; assert json.load(open(sys.argv[1], encoding="utf-8"))["platformRid"] == "linux-x64-wow64"' \
      "${COMPATIBILITY_RUNTIME_ROOT}/aetherxiv-runtime.json" 2>/dev/null; then
    echo "Linux compatibility runtime has the wrong platformRid: ${COMPATIBILITY_RUNTIME_ROOT}" >&2
    exit 40
  fi
}

require_mingw_x86() {
  if ! command -v i686-w64-mingw32-g++ >/dev/null 2>&1; then
    echo "i686-w64-mingw32-g++ is required to build the Windows x86 Umbra injector." >&2
    exit 41
  fi
}

require_mingw_x86_64() {
  if ! command -v x86_64-w64-mingw32-gcc >/dev/null 2>&1; then
    echo "x86_64-w64-mingw32-gcc is required to build the Windows x64 Discord bridge." >&2
    exit 41
  fi
}

check_build_prerequisites() {
  local missing=()
  if ! command -v "${DOTNET_BIN}" >/dev/null 2>&1 \
      || ! "${DOTNET_BIN}" --list-sdks 2>/dev/null | awk '{print $1}' | grep -Fxq '10.0.203'; then
    missing+=(".NET SDK 10.0.203 (dotnet)")
  fi
  command -v python3 >/dev/null 2>&1 || missing+=("Python 3 (python3)")
  command -v i686-w64-mingw32-g++ >/dev/null 2>&1 || missing+=("MinGW-w64 (i686-w64-mingw32-g++)")
  command -v x86_64-w64-mingw32-gcc >/dev/null 2>&1 || missing+=("MinGW-w64 (x86_64-w64-mingw32-gcc)")

  if ((${#missing[@]} > 0)); then
    local docs_platform
    docs_platform="$(printf '%s' "${PLATFORM_NAME}" | tr '[:lower:]' '[:upper:]')"
    echo "AetherXIV ${PLATFORM_NAME} build prerequisites are missing:" >&2
    printf '  - %s\n' "${missing[@]}" >&2
    echo "See docs/build/${docs_platform}.md before running this build again." >&2
    exit 40
  fi
}

build_umbra_native_injector() {
  require_mingw_x86

  local source_path="${LAUNCHER_ROOT}/AetherXIV.Launcher.NativeInjector/umbra_native_injector.cpp"
  local output_path="${OUTPUT_ROOT}/native/Umbra.NativeInjector.x86.exe"
  mkdir -p "$(dirname "${output_path}")"

  echo "Building Umbra native x86 injector..."
  i686-w64-mingw32-g++ \
    -std=c++20 \
    -O2 \
    -municode \
    -static \
    "${source_path}" \
    -o "${output_path}"

  for helper_rid in ${HELPER_RIDS}; do
    if [[ -d "${OUTPUT_ROOT}/launcher/app/Helpers/${helper_rid}" ]]; then
      cp "${output_path}" "${OUTPUT_ROOT}/launcher/app/Helpers/${helper_rid}/Umbra.NativeInjector.x86.exe"
    fi
  done

  rm -rf "${OUTPUT_ROOT}/native"
}

build_discord_bridge() {
  require_mingw_x86_64

  local source_path="${LAUNCHER_ROOT}/AetherXIV.Launcher.DiscordBridge/discord_bridge.c"
  local output_path="${OUTPUT_ROOT}/native/AetherXIV.DiscordBridge.exe"
  mkdir -p "$(dirname "${output_path}")"

  echo "Building AetherXIV Wine Discord bridge (Linux syscall numbers)..."
  x86_64-w64-mingw32-gcc \
    -O2 \
    -Wall \
    -masm=intel \
    -static \
    "${source_path}" \
    -o "${output_path}"

  for helper_rid in ${HELPER_RIDS}; do
    if [[ -d "${OUTPUT_ROOT}/launcher/app/Helpers/${helper_rid}" ]]; then
      cp "${output_path}" "${OUTPUT_ROOT}/launcher/app/Helpers/${helper_rid}/AetherXIV.DiscordBridge.exe"
    fi
  done

  rm -rf "${OUTPUT_ROOT}/native"
}

build_umbra_bootstrap() {
  require_mingw_x86

  local bootstrap_dir="${LAUNCHER_ROOT}/Umbra/Aether.Umbra.Bootstrap"
  local imgui_dir="${LAUNCHER_ROOT}/Umbra/vendor/imgui"
  local framework_dir="${OUTPUT_ROOT}/launcher/app/Umbra/Framework"
  mkdir -p "${framework_dir}"

  echo "Building bundled Umbra native x86 bootstrap..."
  i686-w64-mingw32-g++ \
    -std=c++20 \
    -O2 \
    -fno-builtin \
    -fno-tree-loop-distribute-patterns \
    -fno-exceptions \
    -fno-rtti \
    -DIMGUI_IMPL_WIN32_DISABLE_GAMEPAD \
    -I"${imgui_dir}" \
    -I"${imgui_dir}/backends" \
    -shared \
    -static \
    -static-libgcc \
    -static-libstdc++ \
    -Wl,--kill-at \
    -o "${framework_dir}/Aether.Umbra.Bootstrap.x86.dll" \
    "${bootstrap_dir}/dllmain.cpp" \
    "${imgui_dir}/imgui.cpp" \
    "${imgui_dir}/imgui_draw.cpp" \
    "${imgui_dir}/imgui_tables.cpp" \
    "${imgui_dir}/imgui_widgets.cpp" \
    "${imgui_dir}/backends/imgui_impl_dx9.cpp" \
    "${imgui_dir}/backends/imgui_impl_win32.cpp" \
    -lgdi32 \
    -ldwmapi \
    -lws2_32

  printf '%s\n' "${UMBRA_VERSION}" > "${framework_dir}/version.txt"
  rm -rf "${framework_dir}/Assets"
  cp -R "${LAUNCHER_ROOT}/Umbra/assets" "${framework_dir}/Assets"
}

stamp_bundled_umbra_framework() {
  local framework_dir="${OUTPUT_ROOT}/launcher/app/Umbra/Framework"
  if [[ "${CONFIGURATION}" == Release ]]; then
    find "${framework_dir}" -type f -name '*.pdb' -delete
  fi
  "${DOTNET_BIN}" run \
    --project "${LAUNCHER_ROOT}/AetherXIV.Umbra.BundleFetcher/AetherXIV.Umbra.BundleFetcher.csproj" \
    --configuration "${CONFIGURATION}" \
    -- \
    --stamp-local "${framework_dir}" "${UMBRA_VERSION}"
}

package_bundled_plugins() {
  local output_dir="${OUTPUT_ROOT}/launcher/app/Umbra/BundledPlugins"
  echo "Packaging built-in Umbra plugins..."
  python3 "${ROOT_DIR}/tools/Universal/package-bundled-plugins.py" \
    --output "${output_dir}" \
    --dotnet "${DOTNET_BIN}"
}

copy_compatibility_runtime_to_launcher() {
  local runtime_target="${OUTPUT_ROOT}/launcher/app/CompatibilityRuntime"
  echo "Embedding the authoritative AetherXIV compatibility runtime..."
  cp -a "${COMPATIBILITY_RUNTIME_ROOT}" "${runtime_target}"
}

write_desktop_launchers() {
  local applications_dir="${OUTPUT_ROOT}/desktop"
  local icons_dir="${applications_dir}/icons"
  mkdir -p "${icons_dir}"
  cp "${ROOT_DIR}/assets/icons/aetherxiv-launcher.png" "${icons_dir}/aetherxiv-launcher.png"
  cp "${ROOT_DIR}/assets/icons/aetherxiv-core-master.png" "${icons_dir}/aetherxiv-core.png"
  cat > "${applications_dir}/AetherXIV Launcher.desktop" <<EOF
[Desktop Entry]
Type=Application
Name=AetherXIV Launcher
Exec=../launcher/app/AetherXIV.Launcher.App
Icon=icons/aetherxiv-launcher.png
Terminal=false
Categories=Game;Utility;
EOF
  cat > "${applications_dir}/AetherXIV Core.desktop" <<EOF
[Desktop Entry]
Type=Application
Name=AetherXIV Core
Exec=../core/app/AetherXIV.Core.App
Icon=icons/aetherxiv-core.png
Terminal=false
Categories=Game;Utility;
EOF
}

promote_output_root() {
  local previous_output_root="${FINAL_OUTPUT_ROOT}.previous"
  rm -rf "${previous_output_root}"
  if [[ -d "${FINAL_OUTPUT_ROOT}" ]]; then
    mv "${FINAL_OUTPUT_ROOT}" "${previous_output_root}"
  fi

  if mv "${OUTPUT_ROOT}" "${FINAL_OUTPUT_ROOT}"; then
    BUILD_COMPLETED=1
    rm -rf "${previous_output_root}"
    return
  fi

  if [[ -d "${previous_output_root}" && ! -e "${FINAL_OUTPUT_ROOT}" ]]; then
    mv "${previous_output_root}" "${FINAL_OUTPUT_ROOT}"
  fi
  return 1
}

write_build_manifest() {
  local manifest_path="${OUTPUT_ROOT}/build-manifest.txt"
  local map_core_path="${OUTPUT_ROOT}/servers/map/AetherXIV.Core.Map.dll"
  {
    printf 'schema=aetherxiv.build.manifest.v1\n'
    printf 'built_at_utc=%s\n' "$(date -u +'%Y-%m-%dT%H:%M:%SZ')"
    printf 'configuration=%s\n' "${CONFIGURATION}"
    printf 'product_version=2.1\n'
    printf 'build_number=%s\n' "${BUILD_NUMBER}"
    printf 'server_rid=%s\n' "${SERVER_RID}"
    printf 'compatibility_runtime_manifest_sha256=%s\n' "$(sha256sum "${OUTPUT_ROOT}/launcher/app/CompatibilityRuntime/aetherxiv-runtime.json" | awk '{print $1}')"
    printf 'compatibility_runtime_path=%s\n' "launcher/app/CompatibilityRuntime"
    printf 'map_core_sha256=%s\n' "$(sha256sum "${map_core_path}" | awk '{print $1}')"
    printf 'map_core_path=%s\n' "servers/map/AetherXIV.Core.Map.dll"
  } > "${manifest_path}"
}

check_build_prerequisites
reset_output_root
require_compatibility_runtime

# Validate all non-build release inputs before spending time publishing binaries.
python3 "${ROOT_DIR}/tools/Universal/create-direct-core-database-package.py" \
  --repo-root "${ROOT_DIR}" \
  --output-dir "${OUTPUT_ROOT}/Database"

echo "Publishing server hosts..."
publish_project "${ROOT_DIR}/src/AetherXIV.Core.Map/AetherXIV.Core.Map.csproj" "${OUTPUT_ROOT}/servers/map" --runtime "${SERVER_RID}"
publish_project "${ROOT_DIR}/src/AetherXIV.Core.World/AetherXIV.Core.World.csproj" "${OUTPUT_ROOT}/servers/world" --runtime "${SERVER_RID}"
publish_project "${ROOT_DIR}/src/AetherXIV.Core.Lobby/AetherXIV.Core.Lobby.csproj" "${OUTPUT_ROOT}/servers/lobby" --runtime "${SERVER_RID}"
publish_project "${ROOT_DIR}/src/AetherXIV.Launcher.Host/AetherXIV.Launcher.Host.csproj" "${OUTPUT_ROOT}/servers/launcher-services" --runtime "${SERVER_RID}"
python3 "${ROOT_DIR}/tools/Universal/lua-tree-manifest.py" \
  --scripts-root "${OUTPUT_ROOT}/servers/map/scripts" \
  --manifest "${OUTPUT_ROOT}/servers/map/scripts.manifest.json" \
  --write

echo "Publishing launcher app and Windows helper payload..."
publish_self_contained_project "${LAUNCHER_ROOT}/AetherXIV.Launcher.App/AetherXIV.Launcher.App.csproj" "${OUTPUT_ROOT}/launcher/app" --runtime "${LAUNCHER_RID}"
for helper_rid in ${HELPER_RIDS}; do
  publish_self_contained_project \
    "${LAUNCHER_ROOT}/AetherXIV.Launcher.ClientLauncher/AetherXIV.Launcher.ClientLauncher.csproj" \
    "${OUTPUT_ROOT}/launcher/app/Helpers/${helper_rid}" \
    --runtime "${helper_rid}" \
    /p:PublishSingleFile=true \
    /p:IncludeNativeLibrariesForSelfExtract=true
done
build_umbra_native_injector
build_discord_bridge

echo "Publishing AetherXIV Core app..."
publish_self_contained_project "${ROOT_DIR}/src/AetherXIV.UI.App/AetherXIV.UI.App.csproj" "${OUTPUT_ROOT}/core/app" --runtime "${LAUNCHER_RID}"

echo "Publishing bundled Umbra base framework and private .NET runtime..."
publish_project \
  "${LAUNCHER_ROOT}/Umbra/Aether.Umbra.Framework/Aether.Umbra.Framework.csproj" \
  "${OUTPUT_ROOT}/launcher/app/Umbra/Framework/Managed" \
  --runtime "${UMBRA_RID}"
python3 "${ROOT_DIR}/tools/Universal/stage-umbra-dotnet-runtime.py" \
  --managed "${OUTPUT_ROOT}/launcher/app/Umbra/Framework/Managed" \
  --runtime "${OUTPUT_ROOT}/launcher/app/Umbra/Framework/Runtime" \
  --dotnet "${DOTNET_BIN}"
build_umbra_bootstrap
stamp_bundled_umbra_framework
package_bundled_plugins
copy_compatibility_runtime_to_launcher
write_desktop_launchers

cp "${ROOT_DIR}/LICENSE" "${OUTPUT_ROOT}/LICENSE"
cp "${ROOT_DIR}/THIRD_PARTY_NOTICES.md" "${OUTPUT_ROOT}/THIRD_PARTY_NOTICES.md"
cp "${ROOT_DIR}/MODIFICATIONS.md" "${OUTPUT_ROOT}/MODIFICATIONS.md"
cp "${ROOT_DIR}/TRADEMARKS.md" "${OUTPUT_ROOT}/TRADEMARKS.md"
write_build_manifest

if [[ "${CONFIGURATION}" == Release ]]; then
  find "${OUTPUT_ROOT}" -type f -name '*.pdb' -delete
fi
find "${OUTPUT_ROOT}" -type f -name '.DS_Store' -delete
"${ROOT_DIR}/tools/Universal/verify-bin-only-build.sh" "${OUTPUT_ROOT}"
promote_output_root
cleanup_release_work

cat <<EOF
AetherXIV ${PLATFORM_NAME} build complete.
Output: ${FINAL_OUTPUT_ROOT}

AetherXIV Launcher includes its authoritative compatibility runtime, locally built integrity-pinned Umbra base framework, and Windows helper.
Online Umbra updates are optional and are not contacted during this build.
EOF
