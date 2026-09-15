#!/usr/bin/env bash
# Reset every character visible to one account using the Lobby's supported
# deletion state transition. This intentionally does not hard-delete rows:
# character-owned rows remain available for support/recovery and foreign-key
# integrity is preserved.
set -euo pipefail

DB_HOST="${AETHERXIV_DB_HOST:-127.0.0.1}"
DB_PORT="${AETHERXIV_DB_PORT:-3306}"
DB_NAME="${AETHERXIV_DB_NAME:-ffxiv_server}"
DB_USER="${AETHERXIV_DB_USER:-aetherxiv}"
DB_PASS="${AETHERXIV_DB_PASSWORD:-aether_dev}"
ACCOUNT_ID=""
ACCOUNT_NAME=""
APPLY=0
ASSUME_YES=0

usage() {
  cat <<'EOF'
Usage:
  reset-account-characters.sh --account-id <id> [--apply --yes]
  reset-account-characters.sh --account-name <name> [--apply --yes]

Without --apply, prints the exact character rows that would be marked deleted.
--apply requires --yes and refuses to run while an affected character has an
active server_sessions row. The update matches the Lobby's DeleteCharacter
behavior: UPDATE characters SET state=1.

Database connection settings use AETHERXIV_DB_HOST, AETHERXIV_DB_PORT,
AETHERXIV_DB_NAME, AETHERXIV_DB_USER, and AETHERXIV_DB_PASSWORD.
EOF
}

while (($#)); do
  case "$1" in
    --account-id) ACCOUNT_ID="${2:-}"; shift 2 ;;
    --account-name) ACCOUNT_NAME="${2:-}"; shift 2 ;;
    --apply) APPLY=1; shift ;;
    --yes) ASSUME_YES=1; shift ;;
    -h|--help) usage; exit 0 ;;
    *) echo "Unknown argument: $1" >&2; usage >&2; exit 2 ;;
  esac
done

[[ -z "$ACCOUNT_ID" || -z "$ACCOUNT_NAME" ]] || {
  echo "Specify exactly one account selector." >&2; exit 2;
}
[[ -n "$ACCOUNT_ID" || -n "$ACCOUNT_NAME" ]] || {
  echo "An account selector is required." >&2; usage >&2; exit 2;
}
[[ -z "$ACCOUNT_ID" || "$ACCOUNT_ID" =~ ^[0-9]+$ ]] || {
  echo "--account-id must be numeric." >&2; exit 2;
}
[[ "$APPLY" == 0 || "$ASSUME_YES" == 1 ]] || {
  echo "Refusing mutation without --yes." >&2; exit 2;
}

MYSQL=(mysql --connect-timeout=5 -h "$DB_HOST" -P "$DB_PORT" -u "$DB_USER")
[[ -z "$DB_PASS" ]] || MYSQL+=("-p$DB_PASS")
MYSQL+=("$DB_NAME")
query() { "${MYSQL[@]}" -N -B -e "$1"; }
quote_literal() { local value="$1"; value="${value//\\/\\\\}"; value="${value//\'/\'\'}"; printf "'%s'" "$value"; }

if [[ -n "$ACCOUNT_NAME" ]]; then
  account_literal="$(quote_literal "$ACCOUNT_NAME")"
  ACCOUNT_ID="$(query "SELECT id FROM users WHERE name=$account_literal LIMIT 2;")"
  [[ "$(wc -l <<<"$ACCOUNT_ID" | tr -d ' ')" == 1 && "$ACCOUNT_ID" =~ ^[0-9]+$ ]] || {
    echo "Account name did not resolve to exactly one account." >&2; exit 1;
  }
fi

account_name="$(query "SELECT name FROM users WHERE id=$ACCOUNT_ID LIMIT 1;")"
[[ -n "$account_name" ]] || { echo "Account $ACCOUNT_ID was not found." >&2; exit 1; }

rows="$(query "SELECT id, slot, name, state FROM characters WHERE userId=$ACCOUNT_ID AND state<>1 ORDER BY slot, id;")"
if [[ -z "$rows" ]]; then
  echo "Account $ACCOUNT_ID ($account_name) has no non-deleted characters."
  exit 0
fi

echo "Account $ACCOUNT_ID ($account_name):"
printf '%s\n' "$rows" | awk -F '\t' '{printf "  id=%s slot=%s name=%s state=%s\n", $1, $2, $3, $4}'

if (( APPLY == 0 )); then
  echo "Dry run only. Re-run with --apply --yes to mark these characters deleted."
  exit 0
fi

live="$(query "SELECT COUNT(*) FROM server_sessions WHERE characterId IN (SELECT id FROM characters WHERE userId=$ACCOUNT_ID AND state<>1);")"
[[ "$live" == 0 ]] || {
  echo "Refusing reset: $live affected character session(s) are active. Log out or stop the stack first." >&2
  exit 1
}

query "START TRANSACTION; UPDATE characters SET state=1 WHERE userId=$ACCOUNT_ID AND state<>1; SELECT ROW_COUNT(); COMMIT;" >/dev/null
remaining="$(query "SELECT COUNT(*) FROM characters WHERE userId=$ACCOUNT_ID AND state<>1;")"
[[ "$remaining" == 0 ]] || { echo "Reset verification failed: $remaining visible character row(s) remain." >&2; exit 1; }
echo "Marked all visible characters for account $ACCOUNT_ID ($account_name) deleted."
