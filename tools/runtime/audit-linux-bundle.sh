#!/usr/bin/env bash
set -euo pipefail

if (($# != 1)); then
  echo "Usage: $0 <runtime-root>" >&2
  exit 2
fi

runtime_root="$(cd "$1" && pwd)"
failures=0
warnings=0
require_file() {
  local path="$1"
  if [[ ! -f "${runtime_root}/${path}" ]]; then
    echo "ERROR: missing ${path}" >&2
    failures=$((failures + 1))
  else
    echo "OK: ${path}"
  fi
}

if [[ "$(uname -m)" != "x86_64" ]]; then
  echo "ERROR: the bundled Linux runtime is only supported on an x86_64 host." >&2
  exit 3
fi

[[ -x "${runtime_root}/bin/wine" ]] || { echo "ERROR: missing executable bin/wine" >&2; failures=$((failures + 1)); }
[[ -x "${runtime_root}/bin/wineserver" ]] || { echo "ERROR: missing executable bin/wineserver" >&2; failures=$((failures + 1)); }

# This is a WoW64 package: the Windows game is x86, while Wine's Unix side and
# its Vulkan loader bridge are x86_64. Do not require lib32-vulkan here.
require_file "lib/wine/i386-windows/winevulkan.dll"
require_file "lib/wine/x86_64-windows/winevulkan.dll"
require_file "lib/wine/x86_64-unix/winevulkan.so"
require_file "lib/wine/x86_64-unix/ntdll.so"
require_file "lib/wine/x86_64-unix/win32u.so"

# Require both the Windows audio implementation and a Linux output backend.
# FFXIV 1.23b creates the XAudio2 2.4 AudioReverb COM class.
require_file "lib/wine/i386-windows/xaudio2_4.dll"
require_file "lib/wine/i386-windows/xaudio2_7.dll"
require_file "lib/wine/i386-windows/xapofx1_5.dll"
require_file "lib/wine/i386-windows/winepulse.drv"
require_file "lib/wine/x86_64-unix/winepulse.so"

if command -v ldd >/dev/null 2>&1; then
  for target in "${runtime_root}/bin/wine" "${runtime_root}/bin/wineserver" "${runtime_root}/lib/wine/x86_64-unix/winevulkan.so" "${runtime_root}/lib/wine/x86_64-unix/winepulse.so"; do
    if [[ ! -f "${target}" ]]; then
      continue
    fi
    # Wine loads these sibling modules itself. Standalone ldd needs the same
    # directory to resolve ntdll.so and win32u.so; scope this to the audit only.
    linkage_status=0
    linkage="$(LD_LIBRARY_PATH="${runtime_root}/lib/wine/x86_64-unix${LD_LIBRARY_PATH:+:${LD_LIBRARY_PATH}}" \
      ldd "${target}" 2>&1)" || linkage_status=$?
    if ((linkage_status != 0)) || [[ "${linkage}" == *"not found"* ]]; then
      echo "ERROR: library audit failed for ${target} (ldd exit ${linkage_status}):" >&2
      echo "${linkage}" >&2
      failures=$((failures + 1))
    else
      echo "OK: host linkage ${target#"${runtime_root}/"}"
    fi
  done
else
  echo "WARNING: ldd is unavailable; host linkage was not audited." >&2
  warnings=$((warnings + 1))
fi

if [[ -f "${runtime_root}/lib/wine/i386-windows/d3d9.dll" ]]; then
  echo "INFO: WineD3D x86 d3d9.dll is present."
else
  echo "ERROR: WineD3D x86 d3d9.dll is missing." >&2
  failures=$((failures + 1))
fi

# DXVK is intentionally not inferred from Wine's d3d9.dll. Its x86 PE DLL is
# supplied as a separate native payload and is audited independently.
if [[ -f "${runtime_root}/dxvk/x32/d3d9.dll" \
      && -f "${runtime_root}/dxvk/probe/AetherXIV.DxvkProbe.exe" ]]; then
  echo "OK: bundled DXVK x32 d3d9.dll and D3D9 probe"
else
  echo "ERROR: bundled DXVK x32 d3d9.dll or D3D9 probe is missing; DXVK cannot be exposed." >&2
  failures=$((failures + 1))
fi

if ((failures > 0)); then
  echo "Linux Wine bundle audit failed: ${failures} error(s), ${warnings} warning(s)." >&2
  exit 1
fi

echo "Linux Wine bundle audit passed: Vulkan bridge and DXVK payload are present; ${warnings} warning(s)."
