#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
cd "${ROOT_DIR}"

TARGET_ROOT="${1:-}"
TARGET_CONFIGURATION=""
TARGET_PLATFORM=""
TARGET_IS_STAGING=0
if [[ -n "${TARGET_ROOT}" ]]; then
  TARGET_ROOT="$(cd "${TARGET_ROOT}" && pwd)"
  case "${TARGET_ROOT}" in
    "${ROOT_DIR}/bin/build/Debug/"*|"${ROOT_DIR}/bin/build/Release/"*) ;;
    *) echo "Verification target is outside bin/build/{Debug,Release}: ${TARGET_ROOT}" >&2; exit 2 ;;
  esac
  target_name="$(basename "${TARGET_ROOT}")"
  case "${target_name}" in
    .MacOS.staging) TARGET_PLATFORM="MacOS"; TARGET_IS_STAGING=1 ;;
    .Windows.staging) TARGET_PLATFORM="Windows"; TARGET_IS_STAGING=1 ;;
    .Linux.staging) TARGET_PLATFORM="Linux"; TARGET_IS_STAGING=1 ;;
    .SteamOS.staging) TARGET_PLATFORM="SteamOS"; TARGET_IS_STAGING=1 ;;
    MacOS|Windows|Linux|SteamOS) TARGET_PLATFORM="${target_name}" ;;
    *) echo "Verification target is not a supported platform package: ${TARGET_ROOT}" >&2; exit 2 ;;
  esac
  TARGET_CONFIGURATION="$(basename "$(dirname "${TARGET_ROOT}")")"
fi

generated_dirs=()
while IFS= read -r path; do generated_dirs+=("${path}"); done < <(
  find src tests tools "AetherXIV Launcher" -type d \( -name bin -o -name obj \) -prune -print | sort
)
if ((${#generated_dirs[@]})); then
  echo "Project-local build directories were found; build artifacts belong under ${ROOT_DIR}/bin/build:" >&2
  printf '  %s\n' "${generated_dirs[@]}" >&2
  exit 1
fi

[[ -d "${ROOT_DIR}/bin/build" ]] || { echo "Build output is missing." >&2; exit 2; }
unexpected=()
while IFS= read -r path; do
  unexpected+=("${path}")
done < <(
  find "${ROOT_DIR}/bin" -mindepth 1 -maxdepth 1 \
    ! -name build ! -name .DS_Store -print | sort
)
while IFS= read -r path; do
  entry_name="$(basename "${path}")"
  case "${entry_name}" in
    Debug|Release) ;;
    .DS_Store) ;;
    .work)
      if [[ -z "${TARGET_ROOT}" ]]; then
        unexpected+=("${path}")
      fi
      ;;
    *) unexpected+=("${path}") ;;
  esac
done < <(find "${ROOT_DIR}/bin/build" -mindepth 1 -maxdepth 1 -print | sort)
if ((${#unexpected[@]})); then
  echo "Only bin/build/{Debug,Release} may remain after a build:" >&2
  printf '  %s\n' "${unexpected[@]}" >&2
  exit 3
fi

verify_file() { [[ -f "$1" ]] || { echo "Build is missing required file: $1" >&2; exit 6; }; }

configurations=(Debug Release)
[[ -z "${TARGET_CONFIGURATION}" ]] || configurations=("${TARGET_CONFIGURATION}")
for configuration in "${configurations[@]}"; do
  configuration_root="${ROOT_DIR}/bin/build/${configuration}"
  [[ -d "${configuration_root}" ]] || continue
  invalid_platforms=()
  while IFS= read -r path; do
    entry_name="$(basename "${path}")"
    case "${entry_name}" in
      # Non-macOS Core-only packages are intentional siblings of the corresponding
      # full platform package. Each platform builder verifies its Core output
      # before promotion; do not make a Full verification fail merely because
      # that valid sibling already exists.
      MacOS|Windows|Linux|SteamOS|Windows-Core|Linux-Core|SteamOS-Core) ;;
      .DS_Store) ;;
      ".${TARGET_PLATFORM}.staging")
        if [[ "${TARGET_IS_STAGING}" != 1 ]]; then
          invalid_platforms+=("${path}")
        fi
        ;;
      *) invalid_platforms+=("${path}") ;;
    esac
  done < <(find "${configuration_root}" -mindepth 1 -maxdepth 1 -print | sort)
  if ((${#invalid_platforms[@]})); then
    echo "Unexpected ${configuration} build entries:" >&2
    printf '  %s\n' "${invalid_platforms[@]}" >&2
    exit 4
  fi

  verification_scan_root="${configuration_root}"
  if [[ -n "${TARGET_ROOT}" ]]; then
    verification_scan_root="${TARGET_ROOT}"
  fi

  forbidden=()
  while IFS= read -r path; do
    if [[ -z "${TARGET_ROOT}" && "${path}" == "${configuration_root}/.DS_Store" ]]; then
      continue
    fi
    forbidden+=("${path}")
  done < <(
    find "${verification_scan_root}" -type f \( \
      -name '.DS_Store' \
      -o -iname '*Tests*.dll' -o -iname '*Tests*.exe' \
      -o -iname 'AetherXIV.Map' -o -iname 'AetherXIV.Map.dll' -o -iname 'AetherXIV.Map.exe' \
      -o -iname 'AetherXIV.World' -o -iname 'AetherXIV.World.dll' -o -iname 'AetherXIV.World.exe' \
      -o -iname 'AetherXIV.Lobby' -o -iname 'AetherXIV.Lobby.dll' -o -iname 'AetherXIV.Lobby.exe' \
      -o -iname 'AetherXIV.Scripting.dll' -o -iname 'AetherXIV.Compatibility.dll' \
      -o -iname 'AetherXIV.Map.Host*' -o -iname 'AetherXIV.World.Host*' \
      -o -iname 'AetherXIV.Lobby.Host*' \) -print | sort
  )
  if [[ "${configuration}" == Release ]]; then
    while IFS= read -r path; do forbidden+=("${path}"); done < <(
      find "${verification_scan_root}" -type f -iname '*.pdb' -print | sort
    )
  fi
  if ((${#forbidden[@]})); then
    echo "${configuration} contains test, symbol, or superseded server files:" >&2
    printf '  %s\n' "${forbidden[@]}" >&2
    exit 5
  fi

  for platform in MacOS Windows Linux SteamOS; do
    [[ -z "${TARGET_PLATFORM}" || "${platform}" == "${TARGET_PLATFORM}" ]] || continue
    if [[ "${TARGET_IS_STAGING}" == 1 && "${platform}" == "${TARGET_PLATFORM}" ]]; then
      platform_root="${TARGET_ROOT}"
    else
      platform_root="${configuration_root}/${platform}"
    fi
    [[ -d "${platform_root}" ]] || continue
    verify_file "${platform_root}/build-manifest.txt"
    verify_file "${platform_root}/LICENSE"
    verify_file "${platform_root}/THIRD_PARTY_NOTICES.md"
    verify_file "${platform_root}/MODIFICATIONS.md"
    verify_file "${platform_root}/TRADEMARKS.md"
    grep -Fxq 'product_version=2.1' "${platform_root}/build-manifest.txt" || {
      echo "Build manifest has the wrong product version: ${platform_root}" >&2
      exit 9
    }
    expected_build_number="$(tr -d '[:space:]' < "${ROOT_DIR}/build-number.txt")"
    grep -Fxq "build_number=${expected_build_number}" "${platform_root}/build-manifest.txt" || {
      echo "Build manifest has the wrong build number: ${platform_root}" >&2
      exit 9
    }
    verify_file "${platform_root}/Database/ffxiv_server.sql"
    verify_file "${platform_root}/Database/ffxiv_server.sql.sha256"
    verify_file "${platform_root}/Database/baseline-history.sha256"
    verify_file "${platform_root}/Database/baseline-manifest.json"
    verify_file "${platform_root}/Database/setup.sh"
    verify_file "${platform_root}/Database/setup.ps1"
    verify_file "${platform_root}/Database/migrations/20260627_battlenpc_spawn_audit_pins.sql"
    verify_file "${platform_root}/Database/migrations/20260716_000003_launcher_local_identity.sql"
    verify_file "${platform_root}/Database/migrations/20260716_000004_database_compatibility.sql"
    verify_file "${platform_root}/Database/migrations/20260716_000005_guildleve_content_contract.sql"
    verify_file "${platform_root}/Database/migrations/20260717_000006_central_shroud_enemy_restore.sql"
    verify_file "${platform_root}/Database/migrations/20260717_000007_character_attribute_allocations.sql"
    verify_file "${platform_root}/Database/migrations/20260718_000013_central_shroud_pinspawn_restore.sql"
    verify_file "${platform_root}/Database/migrations/20260720_000016_gridania_tutorial_actor_roles.sql"
    verify_file "${platform_root}/Database/migrations/20260720_000017_gridania_tutorial_spawn_contract.sql"
    verify_file "${platform_root}/Database/migrations/20260720_000018_gridania_tutorial_nameplates.sql"
    verify_file "${platform_root}/Database/migrations/20260722_000019_gridania_man0g1_guilds.sql"
    verify_file "${platform_root}/Database/migrations/20260722_000020_gridania_man0g1_growery.sql"
    verify_file "${platform_root}/Database/migrations/20260722_000021_gridania_man0g1_escort_and_completion.sql"
    verify_file "${platform_root}/Database/migrations/20260722_000022_gridania_man0g1_escort_balance.sql"
    verify_file "${platform_root}/Database/migrations/20260722_000023_gridania_man0g1_escort_boundary.sql"
    verify_file "${platform_root}/Database/migrations/20260722_000024_gridania_man0g1_escort_boundary_polarity.sql"
    verify_file "${platform_root}/Database/migrations/20260722_000025_gridania_man0g1_escort_actor_presentation.sql"
    verify_file "${platform_root}/Database/migrations/20260723_000026_class_job_progression.sql"
    verify_file "${platform_root}/Database/migrations/20260724_000027_correct_1x_player_baselines.sql"
    verify_file "${platform_root}/Database/migrations/20260724_000028_social_state_persistence.sql"
    verify_file "${platform_root}/Database/migrations/20260727_000029_separate_umbra_control_plane.sql"
    verify_file "${platform_root}/Database/migrations/20260727_000030_native_actor_slots.sql"
    verify_file "${platform_root}/Database/migrations/20260728_000031_private_area_spawn_contract.sql"
    verify_file "${platform_root}/Database/migrations/20260802_000032_quest_runtime_contract.sql"
    grep -q 'CREATE TABLE IF NOT EXISTS server_battlenpc_spawn_audit_pins' \
      "${platform_root}/Database/ffxiv_server.sql" || {
        echo "Database baseline omits pinspawn persistence: ${platform_root}" >&2
        exit 7
      }
    grep -q 'aetherxiv-direct-core-v2' "${platform_root}/Database/ffxiv_server.sql" || {
      echo "Database baseline omits the AetherXIV 2.1 compatibility contract: ${platform_root}" >&2
      exit 7
    }

    packaged_logs=()
    while IFS= read -r path; do
      packaged_logs+=("${path}")
    done < <(find "${platform_root}" -type d -path '*/servers/*/logs' -print | sort)
    if ((${#packaged_logs[@]})); then
      echo "${platform} package contains generated service log directories:" >&2
      printf '  %s\n' "${packaged_logs[@]}" >&2
      exit 11
    fi

    if [[ "${platform}" == MacOS ]]; then
      map_root="${platform_root}/AetherXIV Core.app/Contents/Resources/servers/map"
      umbra_root="${platform_root}/AetherXIV Launcher.app/Contents/MacOS/Umbra/Framework"
      compatibility_runtime_root="${platform_root}/AetherXIV Launcher.app/Contents/Resources/CompatibilityRuntime"
      verify_file "${platform_root}/AetherXIV Core.app/Contents/MacOS/AetherXIV.Core.App"
      verify_file "${platform_root}/AetherXIV Launcher.app/Contents/MacOS/AetherXIV.Launcher.App"
      verify_file "${platform_root}/AetherXIV Core.app/Contents/Resources/AppIcon.icns"
      verify_file "${platform_root}/AetherXIV Launcher.app/Contents/Resources/AppIcon.icns"
      grep -q '<key>CFBundleIconFile</key>' "${platform_root}/AetherXIV Core.app/Contents/Info.plist" || {
        echo "Core macOS bundle does not declare its icon: ${platform_root}" >&2
        exit 8
      }
      grep -q '<key>CFBundleIconFile</key>' "${platform_root}/AetherXIV Launcher.app/Contents/Info.plist" || {
        echo "Launcher macOS bundle does not declare its icon: ${platform_root}" >&2
        exit 8
      }
      verify_file "${platform_root}/AetherXIV Core.app/Contents/Resources/servers/world/AetherXIV.Core.World"
      verify_file "${platform_root}/AetherXIV Core.app/Contents/Resources/servers/lobby/AetherXIV.Core.Lobby"
      verify_file "${platform_root}/AetherXIV Core.app/Contents/Resources/servers/launcher-services/AetherXIV.Launcher.Host"
    else
      map_root="${platform_root}/servers/map"
      umbra_root="${platform_root}/launcher/app/Umbra/Framework"
      compatibility_runtime_root="${platform_root}/launcher/app/CompatibilityRuntime"
      executable_suffix=""; [[ "${platform}" == Windows ]] && executable_suffix=".exe"
      verify_file "${platform_root}/core/app/AetherXIV.Core.App${executable_suffix}"
      verify_file "${platform_root}/launcher/app/AetherXIV.Launcher.App${executable_suffix}"
      verify_file "${platform_root}/servers/world/AetherXIV.Core.World${executable_suffix}"
      verify_file "${platform_root}/servers/lobby/AetherXIV.Core.Lobby${executable_suffix}"
      verify_file "${platform_root}/servers/launcher-services/AetherXIV.Launcher.Host${executable_suffix}"
    fi

    if [[ "${platform}" != Windows ]]; then
      verify_file "${compatibility_runtime_root}/aetherxiv-runtime.json"
      verify_file "${compatibility_runtime_root}/aetherxiv-runtime.sha256"
      verify_file "${compatibility_runtime_root}/SOURCE-OFFER.md"
      verify_file "${compatibility_runtime_root}/licenses/Wine-LGPL-2.1.txt"
      verify_file "${compatibility_runtime_root}/bin/wine"
      verify_file "${compatibility_runtime_root}/bin/wineserver"
      verify_file "${compatibility_runtime_root}/lib/wine/x86_64-windows/wow64cpu.dll"
      python3 -c 'import json,sys; d=json.load(open(sys.argv[1], encoding="utf-8")); assert d["schema"] == "aetherxiv.compatibility-runtime.v1"; assert d["runtimeKind"] == "wine"; assert d["prefixArch"] == "wow64"' \
        "${compatibility_runtime_root}/aetherxiv-runtime.json"
      expected_runtime_rid="linux-x64-wow64"
      [[ "${platform}" == MacOS ]] && expected_runtime_rid="osx-x64-wow64"
      python3 -c 'import json,sys; assert json.load(open(sys.argv[1], encoding="utf-8"))["platformRid"] == sys.argv[2]' \
        "${compatibility_runtime_root}/aetherxiv-runtime.json" "${expected_runtime_rid}" || {
          echo "Compatibility runtime targets the wrong host platform: ${platform_root}" >&2
          exit 10
        }
      grep -Fxq "compatibility_runtime_path=$(python3 -c 'import os,sys; print(os.path.relpath(sys.argv[1], sys.argv[2]))' "${compatibility_runtime_root}" "${platform_root}")" \
        "${platform_root}/build-manifest.txt" || {
          echo "Build manifest omits the authoritative compatibility runtime: ${platform_root}" >&2
          exit 10
        }
    fi

    verify_file "${umbra_root}/Aether.Umbra.Bootstrap.x86.dll"
    verify_file "${umbra_root}/Managed/Aether.Umbra.Framework.exe"
    verify_file "${umbra_root}/Managed/Aether.Umbra.Framework.dll"
    verify_file "${umbra_root}/Runtime/hostfxr.dll"
    verify_file "${umbra_root}/Runtime/umbra-runtime.json"
    runtime_version="$(python3 -c 'import json,sys; print(json.load(open(sys.argv[1], encoding="utf-8"))["version"])' "${umbra_root}/Runtime/umbra-runtime.json")"
    verify_file "${umbra_root}/Runtime/host/fxr/${runtime_version}/hostfxr.dll"
    verify_file "${umbra_root}/Runtime/shared/Microsoft.NETCore.App/${runtime_version}/coreclr.dll"
    verify_file "${umbra_root}/Runtime/shared/Microsoft.NETCore.App/${runtime_version}/hostpolicy.dll"
    python3 -c 'import json,sys; d=json.load(open(sys.argv[1], encoding="utf-8")); o=d["runtimeOptions"]; assert "includedFrameworks" not in o; assert o["framework"]["name"] == "Microsoft.NETCore.App"; assert o["framework"]["version"].startswith("10.")' \
      "${umbra_root}/Managed/Aether.Umbra.Framework.runtimeconfig.json"
    verify_file "${umbra_root}/umbra-framework.json"
    verify_file "${umbra_root}/version.txt"
    python3 "${ROOT_DIR}/tools/Universal/verify-umbra-bundle.py" "${umbra_root}"

    bundled_plugins_root="$(dirname "${umbra_root}")/BundledPlugins"
    verify_file "${bundled_plugins_root}/repository.json"
    if ! python3 - "${bundled_plugins_root}/repository.json" "${bundled_plugins_root}" <<'PYEOF'
import json
import os
import sys

repository_path, plugins_root = sys.argv[1], sys.argv[2]
with open(repository_path, encoding="utf-8") as repository_file:
    repository = json.load(repository_file)
built_in = [entry for entry in repository.get("plugins", []) if entry.get("built_in")]
if not built_in:
    raise SystemExit("no built_in plugin entries")
for entry in built_in:
    package = os.path.join(plugins_root, entry["download_url"])
    if not os.path.isfile(package):
        raise SystemExit(f"missing bundled package: {entry['download_url']}")
    if os.path.getsize(package) != entry["size_bytes"]:
        raise SystemExit(f"size mismatch: {entry['download_url']}")
PYEOF
    then
      echo "Bundled Umbra plugin catalog is inconsistent: ${bundled_plugins_root}" >&2
      exit 10
    fi

    map_suffix=""; [[ "${platform}" == Windows ]] && map_suffix=".exe"
    verify_file "${map_root}/AetherXIV.Core.Map${map_suffix}"
    verify_file "${map_root}/scripts/player.lua"
    verify_file "${map_root}/scripts/commands/ArrowReloadCommand.lua"
    verify_file "${map_root}/scripts/directors/AfterQuestWarpDirector.lua"
    verify_file "${map_root}/scripts/directors/WeatherDirector.lua"
    verify_file "${map_root}/staticactors.bin"
    verify_file "${map_root}/scripts.manifest.json"
    verify_file "${map_root}/navmesh/wil0Field01.snb"
    verify_file "${map_root}/navmesh/SHARPNAV_LICENSE"
  done
done

echo "repository-owned Debug/Release build layout verified: ${ROOT_DIR}/bin/build"
