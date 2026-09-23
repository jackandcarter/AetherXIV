#!/usr/bin/env bash
set -euo pipefail

if (($# != 1)); then
  echo "Usage: $0 <dxvk-archive>" >&2
  exit 2
fi

archive="$1"
if [[ ! -f "${archive}" ]]; then
  echo "DXVK archive does not exist: ${archive}" >&2
  exit 3
fi

if ! tar -tzf "${archive}" | grep -Eq '(^|/)(x32/d3d9\.dll)$'; then
  echo "DXVK archive is missing x32/d3d9.dll." >&2
  exit 4
fi

echo "DXVK archive contains the x86 D3D9 payload: ${archive}"
