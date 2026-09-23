# Bard quest restoration — verified partial implementation

## Status

The six-quest chain is **not yet playable end-to-end**. Verified progression metadata is applied, and the first three server quest scripts are implemented and packaged. Georjeaux's existing ordinary-dialogue script has deliberately not been changed to offer the unfinished chain. No NPC, enemy, chest, encounter statistics or world-space coordinates were invented.

## Implemented using existing systems

- Extended `ClassQuestProgressionPolicy`, rather than adding another quest registry, to cover Brd0j1–6. Requirements are active Archer for the first quest, active Bard thereafter, Archer levels 30/35/40/45/45/50, Conjurer 15, and the preceding completed quest. Bard reads Archer's saved class level.
- Forward migration `20260917_000041_bard_quest_progression.sql` corrects the existing six `gamedata_quests` records. Applied and verified against the live database; ledger insertion committed in the same transaction. Before-values and rollback SQL: `.local-evidence/restoration-backups/20260917-bard-progression/`.
- `Data/scripts/quests/brd/brd0j1.lua` uses existing `StartSequence`, `SetENpc`, `onTalk`, `onKillBNpc`, quest flags, inventory APIs and `CompleteQuest`. Dialogue transitions: Jehantel → Pukno Poki → exact Qiqirn shirrer kill → delivery to Jehantel. Completion grants verified Soul of the Bard and Keeper's Hymn item IDs. Failed inventory grants leave delivery retryable; successful grants are recorded in existing quest flags. These two flags are server bookkeeping, not claimed retail flag assignments.
- `brd0j2.lua` and `brd0j3.lua` use the existing exact actor-class kill callback for Bardi and Phaia. They cannot complete from ordinary dialogue or a different enemy. Completion feeds the existing ledger and restored action grant policy.
- Quest save format, completion ledger, ENPC event routing and content architecture are unchanged.

## Evidence recovered

Client `quest` sheet and requirement delegates independently establish the six levels. Original `journalxtxFst` entries 433–454 establish objectives and order. The client exposes the dialogue functions and job-action award IDs. Named actor classes recovered:

| Identity | Actor class IDs |
|---|---|
| Jehantel | 1002024, 1060039 |
| Pukno Poki | 1001936 |
| Qiqirn shirrer | 2206306 |
| Bardi | 2101413 |
| Phaia | 2101509 |

These actor classes exist in the database with empty class paths and have no corresponding live spawn rows. Matching by class ID found no placements in the existing decoded actor observations. Two Jehantel identities remain variant candidates; neither was silently selected as a proven outdoor spawn.

Client quest directors were also recovered: Brd0j101 and Brd0j401 derive from SimpleQuestBattleBaseClass; Brd0j601 derives from QuestDirectorBaseClass. These inheritance declarations do not supply encounter spawns, parameters or success criteria by themselves.

Reproduce the sheet extraction with `/usr/bin/python3 tools/Universal/research-bard-quests.py`. Evidence and schema/data hashes are under `evidence/bard-quests-2026-09-17/`; client disassembly and original script hashes are in its `client/` directory. Positional sheet fields without proven meanings remain unlabeled.

Important rejected inference: quest_marker IDs beginning 111301 also occur in older sidequest scripts. They were retained as rejected candidates and were **not** used as Bard positions or journal markers.

## Online corroboration

- [Official patch 1.21](https://forum.square-enix.com/ffxiv/threads/39024-patch1.21-Patch-1.21-Notes): chain names, issuers, job prerequisites and action acquisition rules.
- [Archived Jehantel page, last edited March 2012](https://archiv.ffxiv.sevengamer.de/wiki/Jehantel): legacy South Shroud grid 38–48; this is not a full XYZ transform.
- [Archived armor-quest guide](https://archiv.ffxiv.sevengamer.de/wiki/Des_Barden_neue_Kleider): Turning Leaf and La Noscea chest route leads. Not sufficient to identify all server chest classes/transforms.
- [Preserved 1.0 Bard journal](https://finalfantasy.fandom.com/wiki/Bard_Quests_%28version_1.0%29): corroborates the locally recovered journal. Current ARR versions of identically named quests were excluded.
- Garlemald-Server develop tree at `70e54f33fc4ea2473d6d82b46b6d0a4567fb2986` contained no Brd0j/Jehantel/Pukno-named script paths to reuse.

## Still required for authentic play

1. Verified world placements and class-path/appearance selection for Jehantel and Pukno Poki, plus the Georjeaux offer wiring once those destinations are usable.
2. Qiqirn instance trigger, region, combat setup, failure/retry/exit handling and party ownership; Bardi/Phaia spawn and combat data. A kill hook does not create a playable encounter.
3. Brd0j4 escort/ambush encounter and completion; Brd0j5 all four chest identities, placement and collection events; Brd0j6 final battle, encounter completion and Choral Shirt delivery. No placeholder victory or generic-kill completion was added for these quests.
4. Verified EXP scaling/reward behavior. Current first-three scripts do not invent EXP amounts from approximate guide values. Job-award presentation calls outside normal talk context, NPC linkshell flow and journal map markers still require event/packet validation.
5. In-client validation of NPC dialogue arguments, journal transitions, restart recovery, combat credit, reward presentation and inventory-full retry. Unit tests do not establish client stability or retail encounter fidelity.

## Verification and package

27 focused tests passed across the new Bard scripts/policy, existing class-quest policy and ability unlock policy. They exercise requirements, ordered steps, premature/wrong kills, wrong job, duplicate completion and failed reward retries. Lua syntax checks passed. Six live metadata rows and migration checksum were verified.

Core-only Release packaging completed into `bin/build/Release/MacOS/AetherXIV Core.app`, version 2.1.0 build 22042. Map assembly SHA-256: `d072fc0a818878ecba9be936616c0cb8f2a5ed60a71d7a4a9bd6406518d2abc1`. Packaging these scripts does not activate the quest chain: no public quest offer was enabled. No in-game playthrough was performed.
