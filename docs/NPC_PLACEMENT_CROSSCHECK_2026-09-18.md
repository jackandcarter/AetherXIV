# 1.x NPC placement cross-check — 2026-09-18

Research only. No gameplay, spawn, database, or package changes in this pass.

## Version boundary

Use installed legacy client resources, original retail captures, official 1.x
patch notes, and explicitly version-1.0 or contemporaneous archive material.
ARR locations, even for identically named characters and quests, are excluded.
An archive's age alone is not proof that every image embedded in it is 1.x.

## Client markers against historical map cells

Reproducer: `tools/Universal/crosscheck-job-placement-evidence.py`.
Output: `evidence/job-npc-placement-2026-09-18/marker-grid-crosscheck.json`.
The output retains raw rows and hashes of client schema/data/offset/enable
resources. Cached map navigation resources are checked against installed bytes.

Join quest-marker columns 8/9 to navigation columns 0/1, selecting map variant
0. These numbers are client map identifiers, NOT server territory IDs.
Use the previously inspected native grid convention: origin is negative
navigation columns 3/4, spacing 100, integer conversion truncates toward zero.
No origin is fitted to make a historical coordinate agree.

| NPC | Client-derived grid cell | Official 1.21 issuer cell | Result |
|---|---|---|---|
| Jehantel | 38,48 | 38,48 | agrees |
| Lalai | 7,6 | 7,6 | agrees |
| Erik | 7,3 | 7,3 | agrees |
| Curious Gorge | 15,33 | 15,33 | agrees |
| Raya-O-Senna (job markers) | 15,22 | 15,22 | agrees |
| Dozol Meloc | 11,28 | 11,28 | agrees |
| Alberic | 35,18 | 35,18 | agrees |
| Widargelt (11221201) | **39,31** | **38,31** | unresolved discrepancy |

Widargelt's continuous grid is approximately 39.0067,31.7929, just over the
X=39 boundary. Do not silently shift the marker, change rounding, or label the
patch notes wrong. A quest marker can differ from the actor's actual position.
The German and French official notes also say 38,31, so this is not simply an
English transcription discrepancy.

Other destinations remain distinct: Widargelt 11221502 gives 11,16 in map
group 501; Raya marker 11080205 gives 39,52 in map group 305. Neither is evidence
to move the ordinary job issuer to that destination. Phase ownership remains
unresolved. Pukno Poki's marker gives 42,48; no independent grid confirmation
was established in this pass.

## Additional Black Mage lead

Expanded the name search to Kazagg Chah: display-name ID 2420027, actor class
1060036. Quest markers 11223201 and 11223502 both store
X=-1506.5400390625, Z=-233.97000122070312, map identifiers 104/403.
The navigation transform gives 11,28, the same cell as Dozol Meloc.
This is a named destination, not a recovered actor spawn packet.

The explicitly version-1.0 Black Mage journal places Kazagg in an unmapped cave
west of Camp Horizon. That supports the general search area, not exact XYZ,
rotation, ambient visibility, or server territory binding.

## Collision geometry hypotheses

Ran the existing yaw-only PHB probe over layouts 615A0004–615A0008 at seven
marker positions (including Widargelt's alternate destination). Results and
layout/resource hashes are retained in `job-height-hypotheses-*.json`.
Reference Y values came from existing `dftwil.lua` comments and are NOT
independently verified retail measurements. The probe's error statistics must
not be read as independent accuracy validation.

| NPC | Layout hypothesis | Existing comment Y | Nearest surface Y |
|---|---|---:|---:|
| Widargelt | 615A0005 | 251.439 | 251.366746 |
| Curious Gorge | 615A0006 | 53.2 | 53.112545 |
| Kazagg Chah | 615A0006 | 10.241 | 10.240973 |
| Dozol Meloc | 615A0006 | 10.617 | 10.602771 |

Kazagg and Dozol intersect the same PHB resource, 898001C2. Both also have
higher surfaces: Kazagg near 20.101 and 53.994; Dozol near 22.458 and 54.233.
This is useful evidence for investigating layered cave geometry. It is NOT
permission to choose the highest surface or to infer an NPC from a static mesh.
No Lalai/Erik intersections occurred in these five layouts; this does not
establish absence of their city geometry. Tilted placements were excluded,
zone/layout linkage is not fully verified, and collision flags are not yet a
complete walkability policy. No facing was recovered.

## Historical sources checked

- [Official English patch 1.21 notes](https://forum.square-enix.com/ffxiv/threads/39024-patch1.21-Patch-1.21-Notes): issuer grid table, also cached under `evidence/patch-notes-2026-09-17/`.
- [Official German patch 1.21 notes](https://forum.square-enix.com/ffxiv/printthread.php?page=1&pp=10&t=39022): Widargelt 38,31 confirmation.
- [Official French patch 1.21 notes](https://forum.square-enix.com/ffxiv/threads/39023-patch1.21-Mise-%C3%A0-jour-1.21): same discrepancy retained.
- [Archived Jehantel page](https://archiv.ffxiv.sevengamer.de/wiki/Jehantel): last edited March 11, 2012; South Shroud 38,48.
- [Archived Bard armor guide](https://archiv.ffxiv.sevengamer.de/wiki/Des_Barden_neue_Kleider): chest location leads 16,39 near Turning Leaf and 20,15 beyond cave entrance 20,14; these are quest objects, not NPC placements.
- [Archived Bard quest guide](https://archiv.ffxiv.sevengamer.de/wiki/Der_hochm%C3%BCtige_Narr): North Shroud 26,11 instance-entry lead, not Jehantel's ambient spawn.
- [Black Mage quests, explicitly version 1.0](https://finalfantasy.fandom.com/wiki/Black_Mage_Quests_%28version_1.0%29): historical journal context, secondary transcription.

No new screenshot-derived placement or facing claim is made. Search indexing
provided archive text, but direct archive/API access was intermittent (API 403).
Images still require individual version verification and visual inspection.

## Next evidence needed

1. Match 1.x screenshots/video to local cave/city/forest landmarks, identifying
   floor and actor facing without substituting ARR geography.
2. Independently validate candidate layouts with already captured nearby NPCs,
   then compare all intersecting surfaces and collision flags.
3. Resolve phase/actor-variant ownership before creating ambient rows.
4. Continue raw capture searches for the identified actor classes. The previous
   negative decoded-corpus search did not include newly added Kazagg Chah.

The Decoy failures are not diagnosed by this placement work. Isolation passes
alone do not prove a framework defect; no framework change was made.
