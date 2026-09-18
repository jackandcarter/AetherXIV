#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
DOTNET_BIN="${DOTNET_BIN:-/usr/local/share/dotnet/dotnet}"
if [[ ! -x "${DOTNET_BIN}" ]]; then
  DOTNET_BIN="$(command -v dotnet 2>/dev/null || true)"
fi

CONFIGURATION="${AETHERXIV_BUILD_CONFIGURATION:-Release}"
BUILD_SCOPE="full"
INSTALL_DEPENDENCIES=0
BUILD_NUMBER="$(tr -d '[:space:]' < "${ROOT_DIR}/build-number.txt")"
grep -Fq "public const int BuildNumber = ${BUILD_NUMBER};" \
  "${ROOT_DIR}/src/AetherXIV.Core/AetherXivBuildInfo.cs" || {
  echo "Build identity mismatch: build-number.txt and AetherXivBuildInfo.cs must agree." >&2
  exit 3
}
grep -Fq "<AetherXivBuildNumber>${BUILD_NUMBER}</AetherXivBuildNumber>" \
  "${ROOT_DIR}/Directory.Build.props" || {
  echo "Build identity mismatch: build-number.txt and Directory.Build.props must agree." >&2
  exit 3
}
configuration_argument_seen=0
while (($# > 0)); do
  case "$1" in
    Debug|Release)
      if ((configuration_argument_seen)); then
        echo "The build configuration may only be specified once." >&2
        exit 2
      fi
      CONFIGURATION="$1"
      configuration_argument_seen=1
      ;;
    --launcher-only)
      BUILD_SCOPE="launcher"
      ;;
    --scope)
      shift
      case "${1:-}" in core) BUILD_SCOPE="core" ;; full) BUILD_SCOPE="full" ;; *) echo "--scope must be core or full." >&2; exit 2 ;; esac
      ;;
    --install-dependencies)
      INSTALL_DEPENDENCIES=1
      ;;
    *)
      echo "Usage: $0 [Release] [--scope core|full] [--launcher-only]" >&2
      exit 2
      ;;
  esac
  shift
done
if [[ "${CONFIGURATION}" != Release ]]; then
  echo "macOS packages are Release-only; use Release." >&2
  exit 2
fi
if (( INSTALL_DEPENDENCIES )); then
  "${ROOT_DIR}/tools/MacOS/install-build-dependencies.sh"
fi
if [[ "${BUILD_SCOPE}" == core ]]; then
  exec "${ROOT_DIR}/tools/MacOS/build-core-only.sh" "${CONFIGURATION}"
fi
FINAL_OUTPUT_ROOT="${ROOT_DIR}/bin/build/Release/MacOS"
if [[ "${BUILD_SCOPE}" == launcher ]]; then
  OUTPUT_ROOT="${ROOT_DIR}/bin/build/${CONFIGURATION}/.MacOS.launcher.staging"
else
  OUTPUT_ROOT="${ROOT_DIR}/bin/build/${CONFIGURATION}/.MacOS.staging"
fi
STAGING_ROOT="${OUTPUT_ROOT}/.components"
SERVER_RID="${AETHERXIV_SERVER_RID:-osx-arm64}"
LAUNCHER_RID="${AETHERXIV_LAUNCHER_RID:-${SERVER_RID}}"
UMBRA_RID="${AETHERXIV_UMBRA_RID:-win-x86}"
UMBRA_VERSION="${AETHERXIV_UMBRA_VERSION:-2.1.0}"
CODESIGN_IDENTITY="${AETHERXIV_CODESIGN_IDENTITY:--}"
COMPATIBILITY_RUNTIME_ROOT="${AETHERXIV_WINE_RUNTIME_ROOT:-}"
LAUNCHER_ROOT="${ROOT_DIR}/AetherXIV Launcher"
RELEASE_WORK_ROOT="${ROOT_DIR}/bin/build/.work/${CONFIGURATION}/MacOS-${BUILD_SCOPE}"
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

reset_output_root() {
  mkdir -p "${ROOT_DIR}/bin/build"
  # Build into an isolated sibling. The last verified release remains intact
  # until this package has passed every check.
  rm -rf "${OUTPUT_ROOT}"
  mkdir -p "${STAGING_ROOT}"
}

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
  if [[ -z "${DOTNET_BIN}" ]] \
      || ! command -v "${DOTNET_BIN}" >/dev/null 2>&1 \
      || ! "${DOTNET_BIN}" --list-sdks 2>/dev/null | awk '{print $1}' | grep -Fxq '10.0.203'; then
    missing+=(".NET SDK 10.0.203 (dotnet)")
  fi
  command -v python3 >/dev/null 2>&1 || missing+=("Python 3 (python3)")
  command -v i686-w64-mingw32-g++ >/dev/null 2>&1 || missing+=("MinGW-w64 (i686-w64-mingw32-g++)")
  command -v x86_64-w64-mingw32-gcc >/dev/null 2>&1 || missing+=("MinGW-w64 (x86_64-w64-mingw32-gcc)")

  if ((${#missing[@]} > 0)); then
    echo "AetherXIV macOS build prerequisites are missing:" >&2
    printf '  - %s\n' "${missing[@]}" >&2
    echo "See docs/build/MACOS.md before running this build again." >&2
    exit 40
  fi
}

require_compatibility_runtime() {
  if [[ -z "${COMPATIBILITY_RUNTIME_ROOT}" ]]; then
    echo "AETHERXIV_WINE_RUNTIME_ROOT is unset; building the AetherXIV compatibility runtime..."
    COMPATIBILITY_RUNTIME_ROOT="${STAGING_ROOT}/compatibility-runtime"
    "${ROOT_DIR}/tools/runtime/build-macos.sh" "${COMPATIBILITY_RUNTIME_ROOT}"
  fi

  if [[ ! -x "${COMPATIBILITY_RUNTIME_ROOT}/bin/wine" \
      || ! -f "${COMPATIBILITY_RUNTIME_ROOT}/aetherxiv-runtime.json" \
      || ! -f "${COMPATIBILITY_RUNTIME_ROOT}/aetherxiv-runtime.sha256" ]]; then
    echo "Packaged AetherXIV macOS compatibility runtime is missing or invalid: ${COMPATIBILITY_RUNTIME_ROOT}" >&2
    echo "Build it first or point AETHERXIV_WINE_RUNTIME_ROOT at a valid runtime package." >&2
    exit 40
  fi
  if ! python3 -c 'import json,sys; assert json.load(open(sys.argv[1], encoding="utf-8"))["platformRid"] == "osx-x64-wow64"' \
      "${COMPATIBILITY_RUNTIME_ROOT}/aetherxiv-runtime.json" 2>/dev/null; then
    echo "macOS compatibility runtime has the wrong platformRid: ${COMPATIBILITY_RUNTIME_ROOT}" >&2
    exit 40
  fi
}

replacement_target() {
  if [[ "${BUILD_SCOPE}" == launcher ]]; then
    printf '%s\n' "${FINAL_OUTPUT_ROOT}/AetherXIV Launcher.app"
  else
    printf '%s\n' "${FINAL_OUTPUT_ROOT}"
  fi
}

ensure_replacement_target_idle() {
  local target
  target="$(replacement_target)"
  [[ -e "${target}" ]] || return 0

  local lsof_bin="/usr/sbin/lsof"
  if [[ ! -x "${lsof_bin}" ]]; then
    lsof_bin="$(command -v lsof 2>/dev/null || true)"
  fi
  if [[ -z "${lsof_bin}" ]]; then
    echo "Cannot safely verify that the existing macOS package is idle because lsof is unavailable." >&2
    exit 52
  fi

  local open_processes
  open_processes="$("${lsof_bin}" -F pc +D "${target}" 2>/dev/null || true)"
  [[ -z "${open_processes}" ]] && return

  echo "Cannot replace the macOS package while processes are using it:" >&2
  printf '%s\n' "${open_processes}" | awk '
    /^p/ { pid = substr($0, 2) }
    /^c/ { printf "  PID %s: %s\n", pid, substr($0, 2) }
  ' >&2
  echo "Stop AetherXIV Core, Launcher, the game client, and their server processes, then rebuild." >&2
  exit 52
}

build_umbra_native_injector() {
  require_mingw_x86

  local source_path="${LAUNCHER_ROOT}/AetherXIV.Launcher.NativeInjector/umbra_native_injector.cpp"
  local output_path="${STAGING_ROOT}/native/Umbra.NativeInjector.x86.exe"
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
    if [[ -d "${STAGING_ROOT}/launcher/app/Helpers/${helper_rid}" ]]; then
      cp "${output_path}" "${STAGING_ROOT}/launcher/app/Helpers/${helper_rid}/Umbra.NativeInjector.x86.exe"
    fi
  done
}

build_discord_bridge() {
  require_mingw_x86_64

  local source_path="${LAUNCHER_ROOT}/AetherXIV.Launcher.DiscordBridge/discord_bridge.c"
  local output_path="${STAGING_ROOT}/native/AetherXIV.DiscordBridge.exe"
  mkdir -p "$(dirname "${output_path}")"

  echo "Building AetherXIV Wine Discord bridge (macOS syscall numbers)..."
  x86_64-w64-mingw32-gcc \
    -O2 \
    -Wall \
    -masm=intel \
    -static \
    -DAETHERXIV_BRIDGE_MACOS \
    "${source_path}" \
    -o "${output_path}"

  for helper_rid in ${HELPER_RIDS}; do
    if [[ -d "${STAGING_ROOT}/launcher/app/Helpers/${helper_rid}" ]]; then
      cp "${output_path}" "${STAGING_ROOT}/launcher/app/Helpers/${helper_rid}/AetherXIV.DiscordBridge.exe"
    fi
  done
}

build_umbra_bootstrap() {
  require_mingw_x86

  local bootstrap_dir="${LAUNCHER_ROOT}/Umbra/Aether.Umbra.Bootstrap"
  local imgui_dir="${LAUNCHER_ROOT}/Umbra/vendor/imgui"
  local framework_dir="${STAGING_ROOT}/umbra/Framework"
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

package_bundled_plugins() {
  local output_dir="$1"
  echo "Packaging built-in Umbra plugins..."
  python3 "${ROOT_DIR}/tools/Universal/package-bundled-plugins.py" \
    --output "${output_dir}" \
    --dotnet "${DOTNET_BIN}"
}

stamp_bundled_umbra_framework() {
  local framework_dir="${STAGING_ROOT}/umbra/Framework"
  if [[ "${CONFIGURATION}" == Release ]]; then
    find "${framework_dir}" -type f -name '*.pdb' -delete
  fi
  "${DOTNET_BIN}" run \
    --project "${LAUNCHER_ROOT}/AetherXIV.Umbra.BundleFetcher/AetherXIV.Umbra.BundleFetcher.csproj" \
    --configuration "${CONFIGURATION}" \
    -- \
    --stamp-local "${framework_dir}" "${UMBRA_VERSION}"
}

write_info_plist() {
  local plist_path="$1"
  local bundle_name="$2"
  local bundle_identifier="$3"
  local executable_name="$4"
  local icon_file="$5"

  cat > "${plist_path}" <<EOF
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
  <key>CFBundleDevelopmentRegion</key>
  <string>en</string>
  <key>CFBundleDisplayName</key>
  <string>${bundle_name}</string>
  <key>CFBundleExecutable</key>
  <string>${executable_name}</string>
  <key>CFBundleIconFile</key>
  <string>${icon_file}</string>
  <key>CFBundleIdentifier</key>
  <string>${bundle_identifier}</string>
  <key>CFBundleInfoDictionaryVersion</key>
  <string>6.0</string>
  <key>CFBundleName</key>
  <string>${bundle_name}</string>
  <key>CFBundlePackageType</key>
  <string>APPL</string>
  <key>CFBundleShortVersionString</key>
  <string>2.1.0</string>
  <key>CFBundleVersion</key>
  <string>${BUILD_NUMBER}</string>
  <key>LSMinimumSystemVersion</key>
  <string>14.0</string>
  <key>NSHighResolutionCapable</key>
  <true/>
</dict>
</plist>
EOF
}

create_app_bundle() {
  local bundle_name="$1"
  local bundle_identifier="$2"
  local executable_name="$3"
  local publish_dir="$4"
  local icon_source="$5"
  local bundle_dir="${OUTPUT_ROOT}/${bundle_name}.app"
  local contents_dir="${bundle_dir}/Contents"
  local macos_dir="${contents_dir}/MacOS"
  local resources_dir="${contents_dir}/Resources"

  echo "Creating ${bundle_name}.app..."
  rm -rf "${bundle_dir}"
  mkdir -p "${macos_dir}" "${resources_dir}"
  cp -R "${publish_dir}/." "${macos_dir}/"
  cp "${icon_source}" "${resources_dir}/AppIcon.icns"
  chmod +x "${macos_dir}/${executable_name}" || true
  write_info_plist "${contents_dir}/Info.plist" "${bundle_name}" "${bundle_identifier}" "${executable_name}" "AppIcon.icns"
}

create_core_app_bundle() {
  create_app_bundle "AetherXIV Core" "org.aetherxiv.core" "AetherXIV.Core.App" "${STAGING_ROOT}/core/app" "${ROOT_DIR}/assets/icons/aetherxiv-core.icns"
  local resources_dir="${OUTPUT_ROOT}/AetherXIV Core.app/Contents/Resources"
  cp -R "${STAGING_ROOT}/servers" "${resources_dir}/servers"
  mkdir -p "${resources_dir}/AetherXIV Launcher/Image"
  cp -R "${LAUNCHER_ROOT}/Image/Reels" "${resources_dir}/AetherXIV Launcher/Image/Reels"
}

copy_umbra_payload_to_launcher_bundle() {
  local umbra_root="${OUTPUT_ROOT}/AetherXIV Launcher.app/Contents/MacOS/Umbra/Framework"
  rm -rf "${umbra_root}"
  mkdir -p "$(dirname "${umbra_root}")"
  cp -R "${STAGING_ROOT}/umbra/Framework" "${umbra_root}"
  if [[ -d "${STAGING_ROOT}/umbra/BundledPlugins" ]]; then
    cp -R "${STAGING_ROOT}/umbra/BundledPlugins" "$(dirname "${umbra_root}")/BundledPlugins"
  fi
}

copy_compatibility_runtime_to_launcher_bundle() {
  local runtime_target="${OUTPUT_ROOT}/AetherXIV Launcher.app/Contents/Resources/CompatibilityRuntime"
  echo "Embedding the authoritative AetherXIV compatibility runtime..."
  mkdir -p "$(dirname "${runtime_target}")"
  if cp -cR "${COMPATIBILITY_RUNTIME_ROOT}" "${runtime_target}" 2>/dev/null; then
    :
  else
    cp -R "${COMPATIBILITY_RUNTIME_ROOT}" "${runtime_target}"
  fi
}

sign_macos_native_code() {
  local bundle_root="$1"
  local excluded_root="${2:-}"
  local scratch_root
  scratch_root="$(mktemp -d "${OUTPUT_ROOT}/.codesign.XXXXXX")"
  local native_file
  local relative_file
  local scratch_file
  while IFS= read -r -d '' native_file; do
    if [[ -n "${excluded_root}" && "${native_file}" == "${excluded_root}"/* ]]; then
      continue
    fi
    if file "${native_file}" | grep -Fq 'Mach-O'; then
      relative_file="${native_file#${bundle_root}/}"
      scratch_file="${scratch_root}/${relative_file}"
      mkdir -p "$(dirname "${scratch_file}")"
      cp -p "${native_file}" "${scratch_file}"
      codesign --force --sign "${CODESIGN_IDENTITY}" --timestamp=none "${scratch_file}" >/dev/null
      cp -p "${scratch_file}" "${native_file}"
    fi
  done < <(find "${bundle_root}" -type f -print0)
  rm -rf "${scratch_root}"
}

verify_macos_native_code() {
  local bundle_root="$1"
  local excluded_root="${2:-}"
  local scratch_root
  scratch_root="$(mktemp -d "${OUTPUT_ROOT}/.codesign-verify.XXXXXX")"
  local native_file
  local relative_file
  local scratch_file
  while IFS= read -r -d '' native_file; do
    if [[ -n "${excluded_root}" && "${native_file}" == "${excluded_root}"/* ]]; then
      continue
    fi
    if file "${native_file}" | grep -Fq 'Mach-O'; then
      relative_file="${native_file#${bundle_root}/}"
      scratch_file="${scratch_root}/${relative_file}"
      mkdir -p "$(dirname "${scratch_file}")"
      cp -p "${native_file}" "${scratch_file}"
      codesign --verify --strict "${scratch_file}" >/dev/null
    fi
  done < <(find "${bundle_root}" -type f -print0)
  rm -rf "${scratch_root}"
}

sign_macos_app_bundle() {
  local bundle_root="$1"
  codesign --force --deep --sign "${CODESIGN_IDENTITY}" --timestamp=none "${bundle_root}" >/dev/null
  codesign --verify --deep --strict "${bundle_root}" >/dev/null
}

cleanup_staging() {
  rm -rf "${STAGING_ROOT}"
  if [[ "${CONFIGURATION}" == Release ]]; then
    find "${OUTPUT_ROOT}" -type f -name '*.pdb' -delete
  fi
  find "${OUTPUT_ROOT}" -type f -name '.DS_Store' -delete
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

verify_launcher_only_output() {
  local app_root="${OUTPUT_ROOT}/AetherXIV Launcher.app"
  local executable_root="${app_root}/Contents/MacOS"
  local framework_root="${executable_root}/Umbra/Framework"
  local runtime_root="${app_root}/Contents/Resources/CompatibilityRuntime"
  local required_file
  local required_files=(
    "${executable_root}/AetherXIV.Launcher.App"
    "${framework_root}/Aether.Umbra.Bootstrap.x86.dll"
    "${framework_root}/Managed/Aether.Umbra.Framework.exe"
    "${framework_root}/Managed/Aether.Umbra.Framework.dll"
    "${framework_root}/Runtime/hostfxr.dll"
    "${framework_root}/Runtime/umbra-runtime.json"
    "${runtime_root}/aetherxiv-runtime.json"
    "${runtime_root}/aetherxiv-runtime.sha256"
    "${runtime_root}/bin/wine"
    "${runtime_root}/bin/wineserver"
    "${runtime_root}/lib/wine/i386-windows/ntdll.dll"
    "${runtime_root}/lib/wine/x86_64-windows/wow64cpu.dll"
  )

  for required_file in "${required_files[@]}"; do
    if [[ ! -f "${required_file}" ]]; then
      echo "Launcher-only build is missing required file: ${required_file}" >&2
      exit 50
    fi
  done

  local helper_rid
  for helper_rid in ${HELPER_RIDS}; do
    for required_file in \
      "${executable_root}/Helpers/${helper_rid}/AetherXIV.Launcher.ClientLauncher.exe" \
      "${executable_root}/Helpers/${helper_rid}/Umbra.NativeInjector.x86.exe" \
      "${executable_root}/Helpers/${helper_rid}/AetherXIV.DiscordBridge.exe"; do
      if [[ ! -f "${required_file}" ]]; then
        echo "Launcher-only build is missing required helper file: ${required_file}" >&2
        exit 50
      fi
    done
  done

  python3 "${ROOT_DIR}/tools/Universal/verify-umbra-bundle.py" "${framework_root}"
  if ! (cd "${runtime_root}" && shasum -a 256 -c aetherxiv-runtime.sha256 >/dev/null); then
    echo "The launcher-only compatibility runtime failed its packaged checksum inventory." >&2
    exit 51
  fi
}

promote_launcher_app() {
  local source_app="${OUTPUT_ROOT}/AetherXIV Launcher.app"
  local target_app="${FINAL_OUTPUT_ROOT}/AetherXIV Launcher.app"
  local previous_app="${FINAL_OUTPUT_ROOT}/.AetherXIV Launcher.app.previous"
  mkdir -p "${FINAL_OUTPUT_ROOT}"
  rm -rf "${previous_app}"
  if [[ -d "${target_app}" ]]; then
    mv "${target_app}" "${previous_app}"
  fi

  if mv "${source_app}" "${target_app}"; then
    BUILD_COMPLETED=1
    rm -rf "${previous_app}" "${OUTPUT_ROOT}"
    return
  fi

  if [[ -d "${previous_app}" && ! -e "${target_app}" ]]; then
    mv "${previous_app}" "${target_app}"
  fi
  return 1
}

write_build_manifest() {
  local manifest_path="${OUTPUT_ROOT}/build-manifest.txt"
  local map_core_path="${OUTPUT_ROOT}/AetherXIV Core.app/Contents/Resources/servers/map/AetherXIV.Core.Map.dll"
  {
    printf 'schema=aetherxiv.build.manifest.v1\n'
    printf 'built_at_utc=%s\n' "$(date -u +'%Y-%m-%dT%H:%M:%SZ')"
    printf 'configuration=%s\n' "${CONFIGURATION}"
    printf 'product_version=2.1\n'
    printf 'build_number=%s\n' "${BUILD_NUMBER}"
    printf 'server_rid=%s\n' "${SERVER_RID}"
    printf 'compatibility_runtime_manifest_sha256=%s\n' "$(shasum -a 256 "${OUTPUT_ROOT}/AetherXIV Launcher.app/Contents/Resources/CompatibilityRuntime/aetherxiv-runtime.json" | awk '{print $1}')"
    printf 'compatibility_runtime_path=%s\n' "AetherXIV Launcher.app/Contents/Resources/CompatibilityRuntime"
    printf 'map_core_sha256=%s\n' "$(shasum -a 256 "${map_core_path}" | awk '{print $1}')"
    printf 'map_core_path=%s\n' "AetherXIV Core.app/Contents/Resources/servers/map/AetherXIV.Core.Map.dll"
  } > "${manifest_path}"
}

check_build_prerequisites
ensure_replacement_target_idle
reset_output_root
require_compatibility_runtime

if [[ "${BUILD_SCOPE}" == full ]]; then
  # Validate all non-build release inputs before spending time publishing binaries.
  python3 "${ROOT_DIR}/tools/Universal/create-direct-core-database-package.py" \
    --repo-root "${ROOT_DIR}" \
    --output-dir "${OUTPUT_ROOT}/Database"

  echo "Publishing server hosts..."
  publish_project "${ROOT_DIR}/src/AetherXIV.Core.Map/AetherXIV.Core.Map.csproj" "${STAGING_ROOT}/servers/map" --runtime "${SERVER_RID}"
  publish_project "${ROOT_DIR}/src/AetherXIV.Core.World/AetherXIV.Core.World.csproj" "${STAGING_ROOT}/servers/world" --runtime "${SERVER_RID}"
  publish_project "${ROOT_DIR}/src/AetherXIV.Core.Lobby/AetherXIV.Core.Lobby.csproj" "${STAGING_ROOT}/servers/lobby" --runtime "${SERVER_RID}"
  publish_project "${ROOT_DIR}/src/AetherXIV.Launcher.Host/AetherXIV.Launcher.Host.csproj" "${STAGING_ROOT}/servers/launcher-services" --runtime "${SERVER_RID}"
  python3 "${ROOT_DIR}/tools/Universal/lua-tree-manifest.py" \
    --scripts-root "${STAGING_ROOT}/servers/map/scripts" \
    --manifest "${STAGING_ROOT}/servers/map/scripts.manifest.json" \
    --write
fi

echo "Publishing launcher app and Windows helper payload..."
publish_self_contained_project "${LAUNCHER_ROOT}/AetherXIV.Launcher.App/AetherXIV.Launcher.App.csproj" "${STAGING_ROOT}/launcher/app" --runtime "${LAUNCHER_RID}"
for helper_rid in ${HELPER_RIDS}; do
  publish_self_contained_project \
    "${LAUNCHER_ROOT}/AetherXIV.Launcher.ClientLauncher/AetherXIV.Launcher.ClientLauncher.csproj" \
    "${STAGING_ROOT}/launcher/app/Helpers/${helper_rid}" \
    --runtime "${helper_rid}" \
    /p:PublishSingleFile=true \
    /p:IncludeNativeLibrariesForSelfExtract=true
done
build_umbra_native_injector
build_discord_bridge

if [[ "${BUILD_SCOPE}" == full ]]; then
  echo "Publishing AetherXIV Core app..."
  publish_self_contained_project "${ROOT_DIR}/src/AetherXIV.UI.App/AetherXIV.UI.App.csproj" "${STAGING_ROOT}/core/app" --runtime "${LAUNCHER_RID}"
fi

echo "Publishing bundled Umbra base framework and private .NET runtime..."
"${DOTNET_BIN}" restore \
  "${LAUNCHER_ROOT}/Umbra/Aether.Umbra.Framework/Aether.Umbra.Framework.csproj" \
  --runtime "${UMBRA_RID}" \
  -m:1 \
  /nodeReuse:false \
  /p:SelfContained=true \
  /p:NuGetAudit=false
publish_project \
  "${LAUNCHER_ROOT}/Umbra/Aether.Umbra.Framework/Aether.Umbra.Framework.csproj" \
  "${STAGING_ROOT}/umbra/Framework/Managed" \
  --runtime "${UMBRA_RID}"
python3 "${ROOT_DIR}/tools/Universal/stage-umbra-dotnet-runtime.py" \
  --managed "${STAGING_ROOT}/umbra/Framework/Managed" \
  --runtime "${STAGING_ROOT}/umbra/Framework/Runtime" \
  --dotnet "${DOTNET_BIN}"
build_umbra_bootstrap
stamp_bundled_umbra_framework
package_bundled_plugins "${STAGING_ROOT}/umbra/BundledPlugins"

create_app_bundle "AetherXIV Launcher" "org.aetherxiv.launcher" "AetherXIV.Launcher.App" "${STAGING_ROOT}/launcher/app" "${ROOT_DIR}/assets/icons/aetherxiv-launcher.icns"
copy_umbra_payload_to_launcher_bundle
copy_compatibility_runtime_to_launcher_bundle
sign_macos_native_code \
  "${OUTPUT_ROOT}/AetherXIV Launcher.app" \
  "${OUTPUT_ROOT}/AetherXIV Launcher.app/Contents/Resources/CompatibilityRuntime"
if [[ "${BUILD_SCOPE}" == full ]]; then
  create_core_app_bundle
  sign_macos_native_code "${OUTPUT_ROOT}/AetherXIV Core.app"
  verify_macos_native_code "${OUTPUT_ROOT}/AetherXIV Core.app"
  # Seal the finished Core bundle only after every server payload and resource
  # has been placed inside it. This matches the Core-only package contract.
  sign_macos_app_bundle "${OUTPUT_ROOT}/AetherXIV Core.app"
  cp "${ROOT_DIR}/LICENSE" "${OUTPUT_ROOT}/LICENSE"
  cp "${ROOT_DIR}/THIRD_PARTY_NOTICES.md" "${OUTPUT_ROOT}/THIRD_PARTY_NOTICES.md"
  cp "${ROOT_DIR}/MODIFICATIONS.md" "${OUTPUT_ROOT}/MODIFICATIONS.md"
  cp "${ROOT_DIR}/TRADEMARKS.md" "${OUTPUT_ROOT}/TRADEMARKS.md"
  write_build_manifest
  cleanup_staging
  "${ROOT_DIR}/tools/Universal/verify-bin-only-build.sh" "${OUTPUT_ROOT}"
  verify_macos_native_code \
    "${OUTPUT_ROOT}/AetherXIV Launcher.app" \
    "${OUTPUT_ROOT}/AetherXIV Launcher.app/Contents/Resources/CompatibilityRuntime"
  ensure_replacement_target_idle
  promote_output_root
else
  cleanup_staging
  verify_launcher_only_output
  ensure_replacement_target_idle
  promote_launcher_app
fi
cleanup_release_work

if [[ "${BUILD_SCOPE}" == full ]]; then
  cat <<EOF
AetherXIV macOS build complete.
Output: ${FINAL_OUTPUT_ROOT}
Apps: ${FINAL_OUTPUT_ROOT}/AetherXIV Launcher.app
      ${FINAL_OUTPUT_ROOT}/AetherXIV Core.app

This package includes the authoritative AetherXIV compatibility runtime, locally built integrity-pinned Umbra base framework, and Windows helper.
Online Umbra updates are optional and are not contacted during this build.
EOF
else
  cat <<EOF
AetherXIV macOS launcher-only build complete.
App: ${FINAL_OUTPUT_ROOT}/AetherXIV Launcher.app

No Core app, server host, database package, actor data, or server output was built or replaced.
EOF
fi
