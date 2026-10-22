# Debugging and Bug Reporting

Report AetherXIV problems in both places when practical:

- Discord: `#bug-reports`
- GitHub: the repository's **Issues** section

Discord is useful for rapid discussion. GitHub is the durable record for
reproduction steps, attached evidence, ownership, and resolution. Link the two
reports instead of creating two unrelated investigations.

## Before reporting

1. Reproduce the problem once with the same settings.
2. Record the local time, time zone, and which action triggered it.
3. Check the Launcher **Launch Log** or the relevant Core service log.
4. Confirm the client, server preset, database, and runtime validations.
5. Avoid repeatedly applying patches, resetting prefixes, or migrating the
   database; those actions can destroy the original evidence.

## Information every report needs

- AetherXIV version and `build-manifest.txt` details when available
- operating system, version, and CPU architecture
- whether this is an official release or a local Debug/Release build
- local all-in-one or remote-server setup
- exact steps to reproduce
- expected result and actual result
- frequency: once, intermittent, or every attempt
- timestamp and time zone
- selected server preset and relevant endpoints, with private addresses redacted
- client validation state and boot/game versions
- runtime mode and graphics target
- whether Umbra was enabled
- relevant character, zone, NPC, quest, item, or action identifiers

## Launcher evidence

Start with the in-app **Launch Log**. Persistent data is under
`Demi Dev Unit/AetherXIV Launcher` in the platform application-data folder:

- Windows: `%APPDATA%\Demi Dev Unit\AetherXIV Launcher\Logs`
- macOS: `~/Library/Application Support/Demi Dev Unit/AetherXIV Launcher/Logs`
- Linux/SteamOS: `$XDG_DATA_HOME/Demi Dev Unit/AetherXIV Launcher/Logs`, or
  `~/.local/share/Demi Dev Unit/AetherXIV Launcher/Logs`

Attach the smallest files that cover the failure:

- the primary launch log;
- its `.helper.log`, if present;
- runtime validation or configuration log for runtime failures;
- `Umbra/Logs` output for framework or plugin failures.

Do not upload the whole application-data directory by default. It can contain
paths, profiles, caches, prefixes, and unrelated logs.

## Core and service evidence

The Core **Logs** tab shows bounded live output for Map, World, Lobby, and
Launcher Services. Copy the section surrounding the failure.

Regular service logs are written below the service's published folder, commonly:

```text
servers/map/logs/<date>/map.log
servers/world/logs/<date>/world.log
servers/lobby/logs/<date>/lobby.log
```

With **Trace enabled**, Core creates a run-specific directory under the
configured Diagnostics dir. Supported services write JSONL traces such as
`map-<timestamp>.jsonl`, `world-<timestamp>.jsonl`, and
`lobby-<timestamp>.jsonl`. Include only the run that reproduced the problem.

### Restoration test capture

Provisional Court content requires explicit development opt-ins. To retain local
choices across application rebuilds, put a `[Development]` section in
`AetherXIV/Core/development.ini` under the operating system's application-data
directory (macOS: `~/Library/Application Support/AetherXIV/Core/development.ini`).
It accepts `coliseum_test_enabled=true` and `miners_guild_test_enabled=true`;
set either to `false` to disable it. Only these two settings are overridden.
Missing keys use `map_config.ini`, whose shipped development defaults stay off.
Changes apply on the next Map server start. The startup log and
`development.content.config` trace report the effective values. This opt-in does
not make provisional combat or placements verified retail behavior.

In Core's advanced diagnostics, enable **Trace enabled**, development logging,
and **Capture full quest/event packets on next stack start**, then save the
configuration and start the stack. This is opt-in. It records selected event,
cutscene and zone handoff opcodes without truncating their packet envelopes;
ordinary wire previews remain bounded. Each service stops full capture after
five minutes, 10,000 packets or 16 MiB, whichever is reached first. Disable the
checkbox after the test. Invalid opcode filters disable capture safely.

**Create test report** reads the latest run offline and opens a searchable local
timeline. It flags missing event replies, zone acknowledgements, and the verified
Court after-warp handoff. These are investigative leads; waiting for player
input or an incomplete recording can also explain missing replies. Raw packet
hex stays in the capture files rather than the HTML report.

The Core executable also supports `--restoration-report RUN_DIRECTORY`, with
optional `--umbra-log CAPTURE_JSONL` when a matching Umbra capture is available.
The separate developer companion capture tools are not included in the public
application downloads; use Core's **Create test report** for the packaged workflow.
Keep captures local: payloads can contain private gameplay or session data.

Client observations are separate from server expectations. Umbra samples existing
framework caches at most once per second during an explicit capture, on a
background worker. Serialization and file writes use a bounded background queue;
if storage falls behind, capture stops with a reason instead of blocking the
render/event producer. Its capture is limited to ten minutes, 20,000 events and
16 MiB. No new native hooks, whole-process scans or screenshots are enabled.
Frame progress does not prove that loading ended or input was released; unverified
1.23b event, UI, loading and network adapters remain explicitly unavailable.

## Crash reports

Include:

- which process crashed;
- exit code or operating-system crash identifier;
- the final 50–100 relevant log lines;
- whether the GUI remained open;
- whether the issue also occurs with Umbra disabled;
- a screenshot only when it adds information not already present in the log.

## Data that must be redacted

Never post these publicly:

- passwords or MariaDB administrator credentials;
- unredacted `core-settings.json`—it stores the application database password;
- account/session tokens, private email addresses, or private account records;
- database dumps containing player or launcher-account data;
- private server addresses, hostnames, or filesystem paths when they identify a
  private system;
- copyrighted client files, patch archives, or extracted Square Enix assets.

Redact the value, not the surrounding error context. For example, preserve
`DatabaseHost=<redacted>` and the failure message so developers can still
understand what failed.

## Discord template

```text
**AetherXIV version/build:**
**Platform:**
**Local or remote server:**
**Client version state:**
**Runtime / graphics target / Umbra:**
**What I did:**
1.
2.
3.
**Expected:**
**Actual:**
**Time and time zone:**
**Relevant character/zone/content:**
**Attached logs:**
**Matching GitHub issue:**
```

## GitHub issue template

```markdown
### Build and environment

- AetherXIV build:
- OS and architecture:
- Release or source build:
- Local or remote topology:
- Client boot/game versions:
- Runtime and graphics target:
- Umbra enabled:

### Reproduction

1.
2.
3.

### Expected behavior

### Actual behavior

### Evidence

- Timestamp and time zone:
- Relevant zone/character/content:
- Logs attached:
- Discord discussion:

### Redaction check

- [ ] No passwords or tokens
- [ ] No unredacted Core settings
- [ ] No player database dump
- [ ] No copyrighted client files
```

## A useful report is narrow

One report should describe one reproducible problem. If a launch failure, quest
failure, and visual bug have different steps or logs, file separate issues and
cross-link them where relevant.
