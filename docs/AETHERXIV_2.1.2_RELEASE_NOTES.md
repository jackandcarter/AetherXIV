# AetherXIV 2.1.2 Release Notes

Build **22044**. Quest restoration, battle improvements and quality of life.

Since the initial 2.1 release, we have continued restoring Eorzea's stories,
world interactions and gameplay systems while improving the tools used to run
and extend AetherXIV.

## Quest and story restoration

Restoration and progression fixes span Limsa Lominsa's *Shapeless Melody*,
*Treasures of the Main*, *Legends Adrift* and *Never the Twain Shall Meet*, and
Gridania's *Sundered Skies*, *Souls Gone Wild*, *Whispers in the Wood* and
*Beckon of the Elementals*.

- Restored missing quest actors, Echo populations, guild conversations,
  Linkpearl sequences, private-area registrations and scene triggers.
- Improved Sisipu's escort and lighthouse continuation, including encounter
  handling, escort survival checks and destination validation.
- Corrected ship doors, actor positions, nameplates and re-entry handling.
- Continued Ul'dah restoration through *Flowers for All* and additional stages
  of *Court in the Sands*, with guild objectives, dialogue and journal counters.
- Improved shared progression, including *Fade to White* and *Together We Stand*.
- Improved journal text, objective markers, counters and reminders.
- Repaired interrupted-scene recovery and reward retry handling.
- Improved event ownership and cleanup during zone changes and disconnections.

*Court in the Sands* remains partially restored. Its provisional Coliseum and
miner content is available only through development opt-ins, disabled by default.

## Grand Company progression

- Expanded *Breaking the Seals* and *The Price of Integrity*, including duties,
  postbattle conversations and hand-ins.
- Improved readiness checks, encounter ownership, defeat handling, timeouts,
  disconnection cleanup and retry recovery.
- Restored enlistment and seal tutorial conversations: *Testing the Waters*,
  *Seals for the Whorl*, *Why Did It Have to Be Snakes*, *Adder's Nest Egg*,
  *Kindling a Flame* and *Burning a Hole in One's Pocket*.
- Improved prerequisites, cancellation, rewards and objective destinations.

Some restored encounter placements and tuning remain provisional.

## Battle system and enemy AI

- Replaced the placeholder ship battle outcome with an actual
  Emerick/Merodaulyn encounter driven by combat and defeated enemies.
- Improved enemy pursuit and route updates when targets move or enter range.
- Corrected movement scheduling and configured movement-speed handling.
- Improved movement-blocking effects and preservation of scripted destinations.
- Repaired disengagement, return-to-spawn behavior and recovery.
- Cleared obsolete movement after death, despawning and target changes.
- Corrected party filtering and area-of-effect checks involving position,
  height and spatial boundaries.
- Repaired supported ability and status behavior, including Sacred Prism and
  Divine Veil, alongside song, combo, proc and crowd-control fixes.
- Improved duty cleanup after defeat, timeouts and interrupted encounters.
- Improved script error handling so one failed timed event does not prevent
  unrelated scheduled work from continuing.

## Navigation and navmesh restoration

- Improved path following, corner traversal, arrival checks and incomplete-route
  handling.
- Corrected cases where failed navigation could produce unchecked straight-line
  movement.
- Expanded extraction and inspection of client collision geometry and recovery
  of world placement, rotation and scale.
- Corrected navmesh generation issues involving agent dimensions and terrain
  height sampling.
- Added an interactive navigation viewer for inspecting walkable areas,
  collision surfaces, disconnected floors and route connectivity.
- Built and evaluated candidates for ship interiors, inn rooms and Thanalan.

World-wide navmesh restoration remains in progress. Newly generated terrain
candidates are still being validated and are not enabled across the live world.

## World, travel, weather and music

- Added and repaired reviewed NPC, enemy and service placements around Limsa
  Lominsa, Black Brush, Horizon and other areas.
- Restored missing ordinary dialogue and camp-service bindings.
- Added Limsa aethernet attendants and improved pass checks, destinations and
  travel cancellation.
- Improved regional weather synchronization, territory eligibility and
  transitions. Weather probabilities and some regions remain under restoration.
- Fixed mounted music across zone changes and seamless borders, with zone music
  restored on dismount.
- Improved ambient, battle and cutscene music transitions.

## Shops, currency and Company services

- Restored ordinary and guild shop stock against the 1.23b client catalogs,
  covering **1,793 populated catalog entries**.
- Restored guild-mark materia and crystal purchases across **18 guild officers**.
- Corrected shop categories, tool mappings, capacity checks, stack handling,
  Rare-item restrictions and insufficient-currency handling.
- Prevented stale or repeated sale confirmations from completing old transactions.
- Improved guild-mark exchanges, Company membership/rank checks and black-market
  coin exchanges.
- Restored confirmation and equipment handling for nine verified Company bardings.
- Corrected Sanction-dependent equipment bonuses and related service dialogue.
- Repaired Keeper's Hymn consumption and current-class attribute resets.
- Improved transaction handling so inventory changes are published after
  successful persistence.

## Inventory, trading and retainers

- Improved player trading with atomic settlement of both players' inventories.
- Added stronger checks for changed offers, stale references, currency limits
  and restricted items.
- Improved stack splitting, merging and full-inventory exchanges.
- Prevented interrupted, cancelled or disconnected trades from completing
  obsolete offers.
- Preserved supported item instance data and modifiers during transfers.
- Expanded retainer inventory persistence, entrust/retrieve handling, confirmed
  dismissal and possession cleanup.
- Added transaction-backed listing and withdrawal support for player and
  retainer retail inventories.

Listing and storage support does not yet constitute a complete bazaar or
retainer market system.

## Materia and equipment

- Restored materia rules for grades, potency, stat bonuses and compatibility.
- Added validated first melds using the appropriate equipment, materia,
  catalyst, crafting requirements and melder key item.
- Applied supported materia bonuses through equipment stats while preserving
  supported quality and modifiers.
- Fixed equipment-window synchronization when unequipping and re-equipping.
- Tightened equipment ownership, slot, class and ability-unlock validation.

Materia currently supports validated first melds. Conversion, forbidden melding
and final tutorial completion/rewards remain unavailable.

## Core app maintenance, backups and status

The Services tab includes dedicated **Server Maintenance** and **Server Status**
sections.

- Browse characters with explicit active and soft-deleted state labels.
- Select individual soft-deleted characters for restoration or permanent deletion.
- Resolve restoration name and account-slot conflicts through a dialogue.
- Preview deletion targets and require a checksum-verified database backup
  before permanent deletion.
- Recheck character identity, state and presence before maintenance changes.
- Prevent maintenance on characters that are online or still saving.
- Create, verify and restore database backups through Core, with operation history.
- View online player counts and distinguish healthy, starting, degraded, unknown
  and unreachable realm states.
- Inspect health-warning reasons and configure realm roles, regions and labels.
- Improved startup ordering and reconnect handling reduce misleading warnings.
- Missing telemetry appears as unknown rather than an assumed zero-player count
  or confirmed shutdown.

## Launcher and Umbra 2.1.2

- Improved recoverable patch transactions, version/integrity checkpoints,
  interrupted-update recovery and temporary file-lock handling.
- Expanded runtime dependency and platform packaging validation.
- Expanded managed plugin UI APIs for images, animated GIFs, sprite sheets and
  image controls.
- Improved media cleanup during plugin unloading and graphics-device resets.
- Expanded plugin messaging, client observations and tracing support.
- Added optional Performance and Client Inspector and Session Monitor tools,
  saved diagnostic layouts and trace exports.
- Improved developer tools for inspecting client state and preparing placement
  drafts. Factory remains separately installable and is not bundled.

## Continuing restoration

*Court in the Sands*, Toto-Rak, broader market systems, complete crafting and
gathering, and additional world content remain in development. Restored
encounters and systems may need further tuning against historical evidence.

Thank you to everyone testing quests, reporting issues and sharing observations.
Your feedback helps us bring more of 1.23b back to life.
