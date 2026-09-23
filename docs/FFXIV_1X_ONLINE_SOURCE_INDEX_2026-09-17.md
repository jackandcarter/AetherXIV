# FFXIV 1.x online recovery source index

Investigated 2026-09-17. This is a source discovery index, not an applied gameplay migration or a claim that every historical patch/hotfix has been recovered.

## Best sources for action unlocks

- [Official patch 1.20](https://forum.square-enix.com/ffxiv/threads/32606-patch1.20-Patch-1.20-Notes): class action and trait tables, required levels, costs, recasts, equipment restrictions and the action-system revision. Covers the seven battle classes. Example Pugilist actions: Pummel 1, Featherfoot 2, Pounce 4, Second Wind 6, Concussive Blow 10. These are 1.20 values, not automatically a final 1.23b specification.
- [Official patch 1.21](https://forum.square-enix.com/ffxiv/threads/39024-patch1.21-Patch-1.21-Notes): seven jobs, prerequisite classes, quest chains, NPCs, locations and job action tables. Paladin examples: Cover 30, Divine Veil 35, Holy Succor 40, Spirits Within 45, Hallowed Ground 50. Job actions require acquisition through the job system; a table level alone must not be implemented as an automatic grant.
- [Official patch 1.22](https://forum.square-enix.com/ffxiv/threads/43599): later adjustments, including crafting abilities. Follow subsequent lettered updates before assigning final-era values.

## Contemporary player guides

These are player reports hosted on the official forum, not Square Enix specifications. Preserve post dates, patch references and uncertainty. Modern profile class/level or account join date does not date a guide.

- [Achievement Guide](https://forum.square-enix.com/ffxiv/threads/29271-Achievement-Guide?mode=hybrid): 2011 guide explicitly referring to 1.19a; class quests and actions purchased using guild marks. Useful for distinguishing purchase requirements from level requirements.
- [Need some advice of cross class abilites](https://forum.square-enix.com/ffxiv/threads/40799): 2012 player discussion, including Second Wind at Pugilist 6 and Sentinel at Gladiator 22. Useful corroboration of class progression and cross-class use.
- [Thaumaturge Ifrit guide](https://forum.square-enix.com/ffxiv/threads/36166-Thaumaturge-Ifrit-guide?mode=hybrid): early 2012 combat guide and discussion of 1.20 spell usage, ability sequencing and encounter tactics.
- [Good King Moggle Mog XII Battle Strategy](https://forum.square-enix.com/ffxiv/threads/35955-Good-King-Moggle-Mog-XII-Battle-Strategy?mode=hybrid): January 2012 player observations of named enemies, abilities and encounter phases. Timing claims need comparison with captures before implementation.

## Archive entry points and gaps

- [Version 1.0 Forum Archive](https://forum.square-enix.com/ffxiv/forums/618-Version-1.0-Forum-Archive): historical gameplay and class/job discussions.
- [Official patch archive](https://forum.square-enix.com/ffxiv/forums/140-Patch-Notes): two pages; 23 version-specific patch entries from 1.16a through 1.23b were indexed, plus the older-notes index. Listing verification does not mean every destination has been individually inspected.
- [Past Patch Notes](https://forum.square-enix.com/ffxiv/threads/5114-Past-Patch-Notes): official pointers to 1.16, 1.15b, 1.15a and November/December 2010 updates. Original 1.16 destination failed during web retrieval. Earlier contents and complete hotfix coverage remain unverified; investigate historical captures of the original URLs.
- FFXIV Classic wiki main page returned a retrieval error in this pass; do not treat it as a verified recovered source.
- Exclude current ARR ability pages and 2013+ guides unless explicitly quoting identified 1.x material. Some modern secondary patch URLs redirect to other versions.

## Applying this evidence

1. Build a patch-versioned action catalogue: class/job, displayed name, learned level, acquisition method, source URL and section, costs/recasts, restrictions, and later changes.
2. Start with the released 1.20 tables, add 1.21 jobs and quest gates, then reconcile later updates through 1.23b. Keep pre-1.20 records separate.
3. Match names to client action IDs using client sheets/scripts and trace observations. Names shared by multiple action rows are insufficient to select an ID.
4. Compare the resulting catalogue against database grants, quest rewards and command handlers. Only apply entries with resolved version, ID and acquisition conditions.
5. Verify level boundaries and quest gates, then actual client execution, costs, cooldowns and effects. A correct unlock level does not establish enemy potency or AI timing.

No runtime/database changes made for this source-discovery pass.

## Official archive link inventory

- [[patch1.23b] Patch 1.23b Notes](https://forum.square-enix.com/ffxiv/threads/54142-patch1.23b-Patch-1.23b-Notes)
- [[patch 1.23a] Patch 1.23a Notes](https://forum.square-enix.com/ffxiv/threads/51545-patch-1.23a-Patch-1.23a-Notes)
- [[patch1.23] Patch 1.23 Notes](https://forum.square-enix.com/ffxiv/threads/50278-patch1.23-Patch-1.23-Notes)
- [[patch1.22c] Patch 1.22c Notes](https://forum.square-enix.com/ffxiv/threads/48097-patch1.22c-Patch-1.22c-Notes)
- [[patch1.22b] Patch 1.22b Notes](https://forum.square-enix.com/ffxiv/threads/47128-patch1.22b-Patch-1.22b-Notes)
- [[patch1.22a] Patch 1.22a Notes](https://forum.square-enix.com/ffxiv/threads/45067-patch1.22a-Patch-1.22a-Notes)
- [[patch1.22] Patch 1.22 Notes](https://forum.square-enix.com/ffxiv/threads/43599-patch1.22-Patch-1.22-Notes)
- [[patch1.21a] Patch 1.21a Notes](https://forum.square-enix.com/ffxiv/threads/40824-patch1.21a-Patch-1.21a-Notes)
- [[patch1.21] Patch 1.21 Notes](https://forum.square-enix.com/ffxiv/threads/39024-patch1.21-Patch-1.21-Notes)
- [[patch1.20c] 1.20c Patch Notes](https://forum.square-enix.com/ffxiv/threads/36591-patch1.20c-1.20c-Patch-Notes)
- [[patch1.20b] 1.20b Patch Notes](https://forum.square-enix.com/ffxiv/threads/35688-patch1.20b-1.20b-Patch-Notes)
- [[patch1.20a] 1.20a Patch Notes](https://forum.square-enix.com/ffxiv/threads/34137-patch1.20a-1.20a-Patch-Notes)
- [[patch1.20] Patch 1.20 Notes](https://forum.square-enix.com/ffxiv/threads/32606-patch1.20-Patch-1.20-Notes)
- [[patch1.19a] 1.19a Patch Notes](https://forum.square-enix.com/ffxiv/threads/26963-patch1.19a-1.19a-Patch-Notes)
- [[patch1.19] Patch 1.19 Notes](https://forum.square-enix.com/ffxiv/threads/24910-patch1.19-Patch-1.19-Notes)
- [[patch1.18b] Patch 1.18b Notes](https://forum.square-enix.com/ffxiv/threads/20351-patch1.18b-Patch-1.18b-Notes)
- [[patch1.18a] Patch 1.18a Notes](https://forum.square-enix.com/ffxiv/threads/19719-patch1.18a-Patch-1.18a-Notes)
- [[patch1.18] Patch 1.18 Notes](https://forum.square-enix.com/ffxiv/threads/17007-patch1.18-Patch-1.18-Notes)
- [[patch1.17c] Patch 1.17c Notes](https://forum.square-enix.com/ffxiv/threads/13617-patch1.17c-Patch-1.17c-Notes)
- [[patch1.17b] Patch 1.17b Notes](https://forum.square-enix.com/ffxiv/threads/11466-patch1.17b-Patch-1.17b-Notes)
- [[patch1.17a] Patch 1.17a Notes](https://forum.square-enix.com/ffxiv/threads/8426-patch1.17a-Patch-1.17a-Notes)
- [[patch1.17] Patch 1.17 Notes](https://forum.square-enix.com/ffxiv/threads/7032-patch1.17-Patch-1.17-Notes)
- [Past Patch Notes](https://forum.square-enix.com/ffxiv/threads/5114-Past-Patch-Notes)
- [[patch1.16a] Patch 1.16a Notes](https://forum.square-enix.com/ffxiv/threads/4823-patch1.16a-Patch-1.16a-Notes)
