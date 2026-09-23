# Nomomo: Sleep, Cousin of Death

## Status

Evidence investigation only. No quest offer, progression, reward, database or
crafting behavior has been enabled by this investigation. Nomomo's restored
placement and default dialogue remain separate from restoring this quest.

## Verified client evidence

- Quest 110420, client class `Alc200`, title **Sleep, Cousin of Death**.
- English `xtx/journalxtxWil` schema 189073482, rows 68–72 establish:
  Nogeloix sends the player to Nomomo at Camp Black Brush; Nomomo supplies a
  reagent; the player crafts medicine, returns to Nogeloix, visits S'lyhhia and
  Damielliot's sickroom, then returns to Nogeloix for a reward.
- `xtx/itemName` schema 189071399 identifies Mummified Mole **11000076**, Eye
  Drops **3020401**, and Potent Medication **11000077**. The journal links these
  ingredients and result. It does not establish ingredient quantities or the
  synthesis formula.
- Local client script:
  `client/script/tp5rq/r75w9s1v/9y7/9y7hjj.le.lpb`, decoded with the existing
  LPB decoder. SHA256 of the source:
  `c646e0af9fbf6fef9e5361c5dae1122f2dba7b6d23178a5868d537addae8f4b4`.
- `processEventNogeloixStart` offers a choice and plays `alc20010` on acceptance.
  `processEvent010` and `processEvent015` are dialogue delegates.
  `processEvent020` plays `alc20020` and uses a post-warp fade-in;
  `processEvent030` plays `alc20030`. Both choose a branch based on an additional
  argument. Their numeric suffixes alone do not prove server sequence values,
  which NPC should invoke them, or the required instance/warp.
- `quest_new_reward` has a row for 110420; its positional columns have **not**
  been assigned reward semantics. Do not copy approximate guide rewards into SQL.

Recheck item identities, journal linkage and source hashes with:

```sh
/usr/bin/python3 tools/Universal/research-alchemist-quest.py
```

## Existing stack and blockers

- Quest metadata exists, but `Data/scripts/quests/alc/alc200.lua` does not.
- `Data/scripts/unique/wil0Town01a/PopulaceStandard/nogeloix.lua` invokes default
  dialogue only. `quests/dft/dftwil.lua` also maps Nogeloix and Nomomo's default
  delegates. Reuse the existing quest/ENPC routing when adding the quest.
- `Data/scripts/commands/CraftCommand.lua` is a demonstration implementation:
  hardcoded recipe/output 10009617, fixed progress increases, random placeholder
  durability/quality changes, and no real ingredient-consumption/output-grant
  transaction in `startCrafting`. Adding the medicine to a displayed recipe list
  would not make the required craft work.
- No Alc200 match was found in the previously extracted gathering/crafting event
  JSON. This is not a claim that every raw capture was exhaustively excluded.
- Acceptance requirements, generic delegate-to-NPC mappings, sickroom instance
  routing, completion rewards and recipe transaction semantics need validation.
  Database prerequisite 0 is not proof of no story prerequisite.

## Restoration order and acceptance checks

1. Resolve remaining delegate mappings and acceptance conditions from client
   dialogue/cutscene resources and any quest-specific capture evidence.
2. Extend the existing crafting path to support a real, validated quest recipe
   transaction. Do not substitute a talk-to-complete or free-item bypass.
3. Add the quest script and starter route using existing quest state/ENPC APIs;
   add inventory and sickroom transitions only with validated contracts.
4. Verify rejection/cancel paths, repeated talks, insufficient materials/full
   inventory, failed synthesis, relog at every stage, and exactly-once rewards.
   Live-test Nomomo's quest/default dialogue routing and the sickroom return.
5. If runtime changes are made, build Release to the canonical MacOS output;
   preserve the existing baseline hash and use additive migrations if needed.
