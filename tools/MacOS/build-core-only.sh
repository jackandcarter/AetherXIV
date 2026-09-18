#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
DOTNET_BIN="${DOTNET_BIN:-/usr/local/share/dotnet/dotnet}"
if [[ ! -x "${DOTNET_BIN}" ]]; then
  DOTNET_BIN="$(command -v dotnet 2>/dev/null || true)"
fi

CONFIGURATION="${AETHERXIV_BUILD_CONFIGURATION:-Release}"
if (($# > 0)); then
  CONFIGURATION="$1"
  shift
fi
if (($# > 0)); then
  echo "Usage: $0 [Release]" >&2
  exit 2
fi
case "${CONFIGURATION}" in
  Release) ;;
  *) echo "macOS packages are Release-only; use Release." >&2; exit 2 ;;
esac

SERVER_RID="${AETHERXIV_SERVER_RID:-osx-arm64}"
CODESIGN_IDENTITY="${AETHERXIV_CODESIGN_IDENTITY:--}"
BUILD_NUMBER="$(tr -d '[:space:]' < "${ROOT_DIR}/build-number.txt")"
FINAL_OUTPUT_ROOT="${ROOT_DIR}/bin/build/Release/MacOS"
TARGET_APP="${FINAL_OUTPUT_ROOT}/AetherXIV Core.app"
TARGET_DATABASE="${FINAL_OUTPUT_ROOT}/Database"
WORK_ROOT="${ROOT_DIR}/bin/build/.work/${CONFIGURATION}/MacOS-core-only"
TEMP_ROOT="$(mktemp -d "${TMPDIR:-/tmp}/aetherxiv-core-only.XXXXXX")"
TEMP_APP="${TEMP_ROOT}/AetherXIV Core.app"
TEMP_SERVERS="${TEMP_ROOT}/servers"
TEMP_DATABASE="${TEMP_ROOT}/Database"
RELEASE_WORK_ROOT="${WORK_ROOT}"
BUILD_COMPLETED=0
PREVIOUS_APP="${FINAL_OUTPUT_ROOT}/.AetherXIV Core.app.previous"
PREVIOUS_DATABASE="${FINAL_OUTPUT_ROOT}/.Database.previous"

export AetherXivWorkRoot="${RELEASE_WORK_ROOT}"

cleanup() {
  rm -rf "${TEMP_ROOT}" "${RELEASE_WORK_ROOT}"
  rmdir "${ROOT_DIR}/bin/build/.work/${CONFIGURATION}" 2>/dev/null || true
  rmdir "${ROOT_DIR}/bin/build/.work" 2>/dev/null || true
  if [[ "${BUILD_COMPLETED}" != 1 ]]; then
    rm -rf "${PREVIOUS_APP}" "${PREVIOUS_DATABASE}"
  fi
}
trap cleanup EXIT INT TERM

check_prerequisites() {
  local missing=()
  if [[ -z "${DOTNET_BIN}" ]] \
      || ! command -v "${DOTNET_BIN}" >/dev/null 2>&1 \
      || ! "${DOTNET_BIN}" --list-sdks 2>/dev/null | awk '{print $1}' | grep -Fxq '10.0.203'; then
    missing+=(".NET SDK 10.0.203")
  fi
  command -v python3 >/dev/null 2>&1 || missing+=("Python 3")

  if ((${#missing[@]} > 0)); then
    echo "Core-only macOS build prerequisites are missing:" >&2
    printf '  - %s\n' "${missing[@]}" >&2
    exit 40
  fi

  grep -Fq "public const int BuildNumber = ${BUILD_NUMBER};" \
    "${ROOT_DIR}/src/AetherXIV.Core/AetherXivBuildInfo.cs" || {
    echo "Build identity mismatch: build-number.txt and AetherXivBuildInfo.cs disagree." >&2
    exit 3
  }
  grep -Fq "<AetherXivBuildNumber>${BUILD_NUMBER}</AetherXivBuildNumber>" \
    "${ROOT_DIR}/Directory.Build.props" || {
    echo "Build identity mismatch: build-number.txt and Directory.Build.props disagree." >&2
    exit 3
  }
  [[ -f "${ROOT_DIR}/assets/icons/aetherxiv-core.icns" ]] || {
    echo "Core app icon is missing." >&2
    exit 40
  }
}

ensure_target_idle() {
  [[ -e "${TARGET_APP}" ]] || return 0

  local lsof_bin="/usr/sbin/lsof"
  if [[ ! -x "${lsof_bin}" ]]; then
    lsof_bin="$(command -v lsof 2>/dev/null || true)"
  fi
  if [[ -z "${lsof_bin}" ]]; then
    echo "Cannot verify that the existing Core app is idle because lsof is unavailable." >&2
    exit 52
  fi

  local open_processes
  open_processes="$("${lsof_bin}" -F pc +D "${TARGET_APP}" 2>/dev/null || true)"
  if [[ -n "${open_processes}" ]]; then
    echo "Cannot replace the Core app while it is in use:" >&2
    printf '%s\n' "${open_processes}" | awk '
      /^p/ { pid = substr($0, 2) }
      /^c/ { printf "  PID %s: %s\n", pid, substr($0, 2) }
    ' >&2
    echo "Stop AetherXIV Core and all server processes before rebuilding." >&2
    exit 52
  fi
}

publish_project() {
  local project_path="$1"
  local output_path="$2"
  shift 2
  local symbol_args=()
  if [[ "${CONFIGURATION}" == Release ]]; then
    symbol_args=(/p:DebugType=None /p:DebugSymbols=false)
  fi

  mkdir -p "${output_path}"
  "${DOTNET_BIN}" publish "${project_path}" \
    --configuration "${CONFIGURATION}" \
    --runtime "${SERVER_RID}" \
    --self-contained false \
    --output "${output_path}" \
    -m:1 \
    /nodeReuse:false \
    /p:NuGetAudit=false \
    /p:PublishSingleFile=false \
    /p:UseAppHost=true \
    "${symbol_args[@]}" \
    "$@"
}

publish_core_app() {
  local symbol_args=()
  if [[ "${CONFIGURATION}" == Release ]]; then
    symbol_args=(/p:DebugType=None /p:DebugSymbols=false)
  fi

  mkdir -p "${TEMP_ROOT}/core-app"
  "${DOTNET_BIN}" publish "${ROOT_DIR}/src/AetherXIV.UI.App/AetherXIV.UI.App.csproj" \
    --configuration "${CONFIGURATION}" \
    --runtime "${SERVER_RID}" \
    --self-contained true \
    --output "${TEMP_ROOT}/core-app" \
    -m:1 \
    /nodeReuse:false \
    /p:NuGetAudit=false \
    /p:PublishSingleFile=false \
    /p:UseAppHost=true \
    "${symbol_args[@]}"
}

write_info_plist() {
  cat > "${TEMP_APP}/Contents/Info.plist" <<EOF
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
  <key>CFBundleDevelopmentRegion</key>
  <string>en</string>
  <key>CFBundleDisplayName</key>
  <string>AetherXIV Core</string>
  <key>CFBundleExecutable</key>
  <string>AetherXIV.Core.App</string>
  <key>CFBundleIconFile</key>
  <string>AppIcon.icns</string>
  <key>CFBundleIdentifier</key>
  <string>org.aetherxiv.core</string>
  <key>CFBundleInfoDictionaryVersion</key>
  <string>6.0</string>
  <key>CFBundleName</key>
  <string>AetherXIV Core</string>
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

assemble_core_app() {
  mkdir -p "${TEMP_APP}/Contents/MacOS" "${TEMP_APP}/Contents/Resources/servers"
  cp -R "${TEMP_ROOT}/core-app/." "${TEMP_APP}/Contents/MacOS/"
  cp "${ROOT_DIR}/assets/icons/aetherxiv-core.icns" "${TEMP_APP}/Contents/Resources/AppIcon.icns"
  cp -R "${TEMP_SERVERS}/." "${TEMP_APP}/Contents/Resources/servers/"
  mkdir -p "${TEMP_APP}/Contents/Resources/AetherXIV Launcher/Image"
  cp -R "${ROOT_DIR}/AetherXIV Launcher/Image/Reels" \
    "${TEMP_APP}/Contents/Resources/AetherXIV Launcher/Image/Reels"
  # Finder metadata is neither runtime content nor a release asset.  Remove it
  # before signing so a Core-only bundle has the same clean payload as Full.
  find "${TEMP_APP}" -type f -name '.DS_Store' -delete
  write_info_plist
  chmod +x "${TEMP_APP}/Contents/MacOS/AetherXIV.Core.App" \
    "${TEMP_APP}/Contents/Resources/servers/map/AetherXIV.Core.Map" \
    "${TEMP_APP}/Contents/Resources/servers/world/AetherXIV.Core.World" \
    "${TEMP_APP}/Contents/Resources/servers/lobby/AetherXIV.Core.Lobby" \
    "${TEMP_APP}/Contents/Resources/servers/launcher-services/AetherXIV.Launcher.Host"

  # Published framework binaries may retain Finder/provenance extended
  # attributes after being copied into the bundle.  Those attributes are not
  # part of the release payload and can cause codesign to reject an otherwise
  # valid nested Mach-O during recursive signing.
  if command -v xattr >/dev/null 2>&1; then
    xattr -cr "${TEMP_APP}"
  fi
  if ! codesign --force --deep --sign "${CODESIGN_IDENTITY}" --timestamp=none "${TEMP_APP}"; then
    echo "Core app code signing failed before promotion; the existing package was retained." >&2
    exit 51
  fi
}

verify_database_package() {
  local required_files=(
    "${TEMP_DATABASE}/ffxiv_server.sql"
    "${TEMP_DATABASE}/ffxiv_server.sql.sha256"
    "${TEMP_DATABASE}/baseline-history.sha256"
    "${TEMP_DATABASE}/baseline-manifest.json"
    "${TEMP_DATABASE}/setup.sh"
    "${TEMP_DATABASE}/setup.ps1"
    "${TEMP_DATABASE}/migrations"
  )

  for required_file in "${required_files[@]}"; do
    [[ -e "${required_file}" ]] || {
      echo "Core-only database package is missing: ${required_file}" >&2
      exit 50
    }
  done

  [[ -n "$(find "${TEMP_DATABASE}/migrations" -maxdepth 1 -type f -name '*.sql' -print -quit)" ]] || {
    echo "Core-only database package contains no migrations." >&2
    exit 50
  }
  (cd "${TEMP_DATABASE}" && shasum -a 256 -c ffxiv_server.sql.sha256 >/dev/null) || {
    echo "Core-only database baseline checksum verification failed." >&2
    exit 50
  }
  grep -q 'aetherxiv-direct-core-v2' "${TEMP_DATABASE}/ffxiv_server.sql" || {
    echo "Core-only database baseline is missing the current compatibility contract." >&2
    exit 50
  }
}

verify_core_app() {
  local resources="${TEMP_APP}/Contents/Resources"
  local map_root="${resources}/servers/map"
  local required_files=(
    "${TEMP_APP}/Contents/MacOS/AetherXIV.Core.App"
    "${TEMP_APP}/Contents/MacOS/AetherXIV.Core.App.dll"
    "${TEMP_APP}/Contents/Info.plist"
    "${resources}/AppIcon.icns"
    "${resources}/servers/map/AetherXIV.Core.Map"
    "${resources}/servers/map/AetherXIV.Core.Map.dll"
    "${resources}/servers/map/scripts/player.lua"
    "${resources}/servers/map/scripts/directors/AfterQuestWarpDirector.lua"
    "${resources}/servers/map/scripts/quests/man/man0g1.lua"
    "${resources}/servers/map/staticactors.bin"
    "${resources}/servers/world/AetherXIV.Core.World"
    "${resources}/servers/lobby/AetherXIV.Core.Lobby"
    "${resources}/servers/launcher-services/AetherXIV.Launcher.Host"
    "${map_root}/scripts.manifest.json"
  )

  for required_file in "${required_files[@]}"; do
    [[ -f "${required_file}" ]] || {
      echo "Core-only package is missing: ${required_file}" >&2
      exit 50
    }
  done

  grep -Fq '<key>CFBundleIconFile</key>' "${TEMP_APP}/Contents/Info.plist" || {
    echo "Core app bundle does not declare its icon." >&2
    exit 50
  }
  grep -Fq '<string>2.1.0</string>' "${TEMP_APP}/Contents/Info.plist" || {
    echo "Core app bundle has the wrong product version." >&2
    exit 50
  }
  grep -Fq 'quest:OnNotice(player);' \
    "${resources}/servers/map/scripts/directors/AfterQuestWarpDirector.lua" || {
    echo "Core package does not contain the quest-owned notice router." >&2
    exit 50
  }
  if grep -Fq 'startTutorialMode(player);' \
    "${resources}/servers/map/scripts/directors/AfterQuestWarpDirector.lua"; then
    echo "Core package still contains the obsolete server tutorial re-arm in the thin notice router." >&2
    exit 50
  fi
  if grep -Fq 'player:RunEventFunction("delegateEvent", player, quest, "processEventTu_001")' \
    "${resources}/servers/map/scripts/directors/AfterQuestWarpDirector.lua"; then
    echo "Core package still dispatches the Linkpearl tutorial from the director instead of the owning quest." >&2
    exit 50
  fi
  grep -Fq 'callClientFunction(player, "delegateEvent", player, quest, "processEventTu_001")' \
    "${resources}/servers/map/scripts/quests/man/man0g1.lua" || {
    echo "Core package does not contain the quest's opening-tutorial dispatch." >&2
    exit 50
  }
  if grep -Fq 'player:RunEventFunction("delegateEvent", player, quest, "processEventTu_001")' \
    "${resources}/servers/map/scripts/quests/man/man0g1.lua"; then
    echo "Core package bypasses the parked Linkpearl tutorial dispatch." >&2
    exit 50
  fi
  if grep -Fq 'endTutorialMode(player);' "${resources}/servers/map/scripts/directors/AfterQuestWarpDirector.lua"; then
    echo "Core package still contains the mid-arc endTutorialMode in the director (cancel cycle corrupts the dispatch)." >&2
    exit 50
  fi
  if grep -Fq 'KickEventSpecial' "${resources}/servers/map/scripts/directors/AfterQuestWarpDirector.lua"; then
    echo "Core package still contains the obsolete tutorial re-kick." >&2
    exit 50
  fi
  codesign --verify --deep --strict --verbose=2 "${TEMP_APP}" >/dev/null 2>&1 || {
    echo "Core app code-signature verification failed." >&2
    exit 50
  }

  local manifest_check="${TEMP_ROOT}/scripts.manifest.check.json"
  python3 "${ROOT_DIR}/tools/Universal/lua-tree-manifest.py" \
    --scripts-root "${map_root}/scripts" \
    --manifest "${manifest_check}" \
    --write
  cmp -s "${manifest_check}" "${map_root}/scripts.manifest.json" || {
    echo "Core package Lua manifest is inconsistent with its packaged scripts." >&2
    exit 50
  }
}

write_build_manifest() {
  local existing_manifest="${FINAL_OUTPUT_ROOT}/build-manifest.txt"
  [[ -f "${existing_manifest}" ]] || existing_manifest=/dev/null
  # Retain Launcher/runtime provenance when updating only Core in a full package.
  awk -F= '!($1 ~ /^(schema|built_at_utc|configuration|product_version|build_number|server_rid|map_core_sha256|map_core_path|last_build_scope)$/)' \
    "${existing_manifest}" > "${TEMP_ROOT}/build-manifest.txt"
  {
    printf 'schema=aetherxiv.build.manifest.v1\n'
    printf 'built_at_utc=%s\n' "$(date -u +%Y-%m-%dT%H:%M:%SZ)"
    printf 'configuration=Release\nproduct_version=2.1\n'
    printf 'build_number=%s\nserver_rid=%s\n' "${BUILD_NUMBER}" "${SERVER_RID}"
    printf 'last_build_scope=core\nmap_core_sha256=%s\n' "${MAP_SHA256}"
    printf 'map_core_path=AetherXIV Core.app/Contents/Resources/servers/map/AetherXIV.Core.Map.dll\n'
  } >> "${TEMP_ROOT}/build-manifest.txt"
}

promote_core_release() {
  mkdir -p "${FINAL_OUTPUT_ROOT}"
  rm -rf "${PREVIOUS_APP}" "${PREVIOUS_DATABASE}"
  if [[ -d "${TARGET_APP}" ]]; then
    mv "${TARGET_APP}" "${PREVIOUS_APP}"
  fi
  if [[ -d "${TARGET_DATABASE}" ]]; then
    mv "${TARGET_DATABASE}" "${PREVIOUS_DATABASE}"
  fi

  if ! mv "${TEMP_APP}" "${TARGET_APP}"; then
    [[ -d "${PREVIOUS_APP}" && ! -e "${TARGET_APP}" ]] && mv "${PREVIOUS_APP}" "${TARGET_APP}"
    [[ -d "${PREVIOUS_DATABASE}" && ! -e "${TARGET_DATABASE}" ]] && mv "${PREVIOUS_DATABASE}" "${TARGET_DATABASE}"
    exit 53
  fi
  if ! mv "${TEMP_DATABASE}" "${TARGET_DATABASE}"; then
    rm -rf "${TARGET_APP}"
    [[ -d "${PREVIOUS_APP}" ]] && mv "${PREVIOUS_APP}" "${TARGET_APP}"
    [[ -d "${PREVIOUS_DATABASE}" ]] && mv "${PREVIOUS_DATABASE}" "${TARGET_DATABASE}"
    exit 53
  fi

  if ! mv "${TEMP_ROOT}/build-manifest.txt" "${FINAL_OUTPUT_ROOT}/build-manifest.txt"; then
    rm -rf "${TARGET_APP}" "${TARGET_DATABASE}"
    [[ -d "${PREVIOUS_APP}" ]] && mv "${PREVIOUS_APP}" "${TARGET_APP}"
    [[ -d "${PREVIOUS_DATABASE}" ]] && mv "${PREVIOUS_DATABASE}" "${TARGET_DATABASE}"
    exit 53
  fi
  rm -rf "${PREVIOUS_APP}" "${PREVIOUS_DATABASE}"
  BUILD_COMPLETED=1
}

check_prerequisites
ensure_target_idle
rm -rf "${WORK_ROOT}"
mkdir -p "${TEMP_SERVERS}"
python3 "${ROOT_DIR}/tools/Universal/create-direct-core-database-package.py" \
  --repo-root "${ROOT_DIR}" \
  --output-dir "${TEMP_DATABASE}"

publish_project "${ROOT_DIR}/src/AetherXIV.Core.Map/AetherXIV.Core.Map.csproj" "${TEMP_SERVERS}/map"
publish_project "${ROOT_DIR}/src/AetherXIV.Core.World/AetherXIV.Core.World.csproj" "${TEMP_SERVERS}/world"
publish_project "${ROOT_DIR}/src/AetherXIV.Core.Lobby/AetherXIV.Core.Lobby.csproj" "${TEMP_SERVERS}/lobby"
publish_project "${ROOT_DIR}/src/AetherXIV.Launcher.Host/AetherXIV.Launcher.Host.csproj" "${TEMP_SERVERS}/launcher-services"
python3 "${ROOT_DIR}/tools/Universal/lua-tree-manifest.py" \
  --scripts-root "${TEMP_SERVERS}/map/scripts" \
  --manifest "${TEMP_SERVERS}/map/scripts.manifest.json" \
  --write
publish_core_app
assemble_core_app
verify_database_package
verify_core_app

MAP_SHA256="$(shasum -a 256 "${TEMP_APP}/Contents/Resources/servers/map/AetherXIV.Core.Map.dll" | awk '{print $1}')"
UI_SHA256="$(shasum -a 256 "${TEMP_APP}/Contents/MacOS/AetherXIV.Core.App.dll" | awk '{print $1}')"
SCRIPT_SHA256="$(shasum -a 256 "${TEMP_APP}/Contents/Resources/servers/map/scripts.manifest.json" | awk '{print $1}')"
write_build_manifest
promote_core_release

cat <<EOF
AetherXIV macOS core-only build complete.
Core app: ${TARGET_APP}
Database package: ${TARGET_DATABASE}
Product: 2.1.0
Build: ${BUILD_NUMBER}
Map DLL SHA-256: ${MAP_SHA256}
Core UI DLL SHA-256: ${UI_SHA256}
Lua manifest SHA-256: ${SCRIPT_SHA256}

Launcher.app, its Wine runtime, Umbra payload, and launcher resources were not rebuilt or replaced.
No persistent staging output was created; temporary publish files were removed.
EOF
