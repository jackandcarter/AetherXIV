#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
DOTNET_BIN="${DOTNET_BIN:-dotnet}"
DEV_WORK_ROOT="${AETHERXIV_DEV_WORK_ROOT:-${ROOT_DIR}/bin/build/.work/Verification}"
export AetherXivWorkRoot="${DEV_WORK_ROOT}"

cleanup() {
  rm -rf "${DEV_WORK_ROOT}"
  rmdir "${ROOT_DIR}/bin/build/.work" 2>/dev/null || true
}
trap cleanup EXIT

cleanup
bash -n "${ROOT_DIR}/db/direct-core/setup.sh"
python3 "${ROOT_DIR}/tools/Universal/generate-native-actor-migration.py" \
  --catalog "${ROOT_DIR}/Data/seeds/actor-catalog/native-actor-slot-overrides.json" \
  --output "${ROOT_DIR}/db/direct-core/migrations/20260727_000030_native_actor_slots.sql" \
  --target direct-core \
  --verify
python3 "${ROOT_DIR}/tools/Universal/generate-native-actor-migration.py" \
  --catalog "${ROOT_DIR}/Data/seeds/actor-catalog/native-actor-slot-overrides.json" \
  --output "${ROOT_DIR}/Data/sql/migrations/20260727_native_actor_slots.sql" \
  --target normalized \
  --verify
python3 "${ROOT_DIR}/tools/Universal/create-direct-core-database-package.py" \
  --repo-root "${ROOT_DIR}" \
  --output-dir "${DEV_WORK_ROOT}/Database"
python3 "${ROOT_DIR}/tools/Universal/lua-tree-manifest.py" \
  --scripts-root "${ROOT_DIR}/Data/scripts" \
  --manifest "${ROOT_DIR}/Data/seeds/lua-tree/manifest.json"
if command -v pwsh >/dev/null 2>&1; then
  AETHERXIV_SETUP_PS1="${ROOT_DIR}/db/direct-core/setup.ps1" pwsh -NoProfile -Command \
    '$errors=$null; [void][System.Management.Automation.Language.Parser]::ParseFile($env:AETHERXIV_SETUP_PS1, [ref]$null, [ref]$errors); if ($errors.Count) { $errors | ForEach-Object { Write-Error $_ }; exit 1 }'
fi
"${DOTNET_BIN}" build "${ROOT_DIR}/AetherXIV.sln" \
  --configuration Release -m:1 /nodeReuse:false /p:NuGetAudit=false
"${DOTNET_BIN}" build "${ROOT_DIR}/AetherXIV Launcher/AetherXIV.Launcher.sln" \
  --configuration Release -m:1 /nodeReuse:false /p:NuGetAudit=false

echo "Release source verification passed; no test or private evidence artifacts were added to bin/build."
