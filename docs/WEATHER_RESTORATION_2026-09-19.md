# Weather restoration — 2026-09-19

The existing Area, WeatherDirector and packet codecs remain the runtime owners.
No regional service, world-era service, second scheduler or new client patch was added.

## Evidence and corrections

The local client's decoded `WeatherDirector` only derives from
`WeatherDirectorBaseClass`. In the base class, `weatherDirectorWork.weatherId`
is `integer16`; the sync tag is `weatherInfo`. `processUpdateWork` calls
`_setWeather(id, 15)` only when the player's previous weather ID is nonzero,
then calls `setWeatherId(id)`. Its zero branch does not call `_setWeather`.
This differs from the linked decompilation summary's initial argument 1 and
its description of a `processUIUpdate` method. Prefer the actual local bytes;
do not assume that the two sources describe identical client scripts.

The 54-capture decoded retail corpus contains 11 direct weather packets,
11 weather director instantiations and 11 weather property initializations.
Every direct weather packet is ID 8030, transition argument **5**. Each precedes
its corresponding director initialization. The property uses hash 0x3EAFB2CC,
the existing `weatherDirectorWork.weatherId` hash, and the `/_init` tag.
There are no observed `weatherInfo` updates in this corpus. These are end-era
entry observations, not a sample of normal climate rolls or probabilities.
Examples (frameIndex is the existing decoder's stream-frame index):

| Capture | Direct weather | Director init |
| --- | --- | --- |
| from_gridania_to_blackshroud, stream 0 | 233 | 246 |
| teleport_to_gridania, stream 0 | 55 | 68 |
| teleport_to_camp_nine_ivies, stream 0 | 69 | 82 |
| move_out_of_room, stream 0 | 29 | 41 |

The extracted packet records retain capture, stream, timestamps, payload and
source-corpus line. Corpus and local script SHA-256 hashes accompany them.

A period player report dated February 3, 2012 describes weather boundaries at
00:00, 08:00 and 16:00 ET:
https://forum.square-enix.com/ffxiv/threads/36803-Day-Night-in-game?mode=linear&p=539281
This supports the 1.x cadence, not the Unix-to-client epoch or selection formula.
The existing Unix/1400 block clock is retained; no startup-relative timer exists.
Live ET epoch alignment remains unverified. Do not describe it as client-verified.

Linked summary reviewed:
https://github.com/Yokimitsuro/ffxivDecomp/blob/bc485d8d4de79d80c23eb8feddbfcebfbb6daab5/docs/re/lua/finding_party_subclasses_and_weather.md
Neither transition parameter is assigned a seconds/ticks unit here.

## Applied changes

- Zone entry retains direct bootstrap before director initialization, now using
  the captured argument 5 instead of the unsupported 1. This also applies to
  seamless entry through the existing entry method.
- Routine weather changes, event overrides and reset use the existing director
  `weatherInfo` update with the client's built-in argument 15. The redundant
  direct application has been removed from those broadcasts.
- An area without a director retains the existing direct-packet fallback.
- Explicit GM transition values other than 15 use the direct packet alone.
  This is an emulator control, not a claim about retail override semantics.
  The GM default is now 15. Personal preview still leaves area weather unchanged.
- Each update samples weather once and passes that selection into the broadcast,
  avoiding a second clock read across a block boundary.
- Period calculation is named and tested at boundaries, including day rollover.
  Repeated weather across blocks is allowed and does not rebroadcast.
- Aurora is included in `!zonefx` help. Special IDs were already excluded from
  normal selection; tests now cover every special ID and preserve normal Gloom.

## Boundaries of this restoration

Normal regional percentages and the deterministic hash remain explicitly
provisional. No new probabilities, Coerthas/Mor Dhona distributions, camp RNG,
Atomos weather, Dodore spawn rules, weather gameplay events, or historical era
schedule were invented. The existing selection key is region, not camp.
Unreviewed regions and interiors retain the prior clear fallback; that fallback
is not a recovered retail rule.

Weather, Dalamud level and music are separate packet surfaces. Existing `!zonefx`
controls weather and Dalamud level explicitly. Normal operation defaults to no
Dalamud override. 8014, 8027–8032, 8065 and 8066 remain explicitly requested event
weather, not normal RNG. This preserves access without inventing era mappings.
8017 Gloom remains a normal ID, but no unsupported Mor Dhona weights were added.

The supplied GPT notes about three historical eras, `weatherNow/weatherDefault`,
`isWorldEndTerm`, seasonal timing and Atomos are research leads, not verified
runtime contracts. The corpus does not establish a weather-roll transport
sequence, exact transition duration, cutscene override restoration, or normal
regional frequencies. No live client visual acceptance was performed here.

## Reproduction and acceptance

Run `python3 tools/Universal/audit-weather-restoration.py --client <client-root>`.
It reuses the existing LPB decoder and decoded retail corpus; both must be present.
The output is `evidence/weather-restoration-2026-09-19`.

Automated validation covers normal/event separation, period boundaries, repeated
weather, native director property bytes, single-packet update selection and direct
fallback. Run the Map tests and Protocol EnvironmentPacketCodecTests.

Live acceptance still needs: login, teleport and seamless entry; a director-only
normal change; consecutive equal blocks; explicit GM preview/custom duration and
reset; comparison against the displayed ET clock at all three boundaries. These
checks are required before claiming visually verified retail parity.

Validation results: focused weather/native-identity suite 70 passed; environment
codec suite 6 passed. Full Map suite: 507 passed, 2 skipped, 4 failures in
DecoyMechanicsTests. All 9 Decoy tests pass when run in isolation; the full-suite
interaction is unresolved and is not reported as a clean full-suite pass.
Only the two edited weather command hashes and aggregate hash were refreshed in
the Lua manifest. Pre-existing `quests/man/man0l1.lua` content/manifest drift
remains outside this weather change.

## Acceptance follow-up — 2026-09-20

Authenticated Umbra status/snapshot confirms a running, rendering client whose
executable SHA-256 matches the verified 2012.09.19.0001 profile. The installed
Map assembly contains the restored weather delivery methods/constants. This
establishes their presence, not a byte-for-byte match to today's source.

The available computer-control app inventory does not expose the Wine game
window. The bridge advertises no semantic weather/clock adapter, screenshot,
remote function invocation or packet mutation. Therefore visual acceptance
cannot be performed autonomously through the available interfaces. No process
memory changes or unverified native function calls were used.

Added real Area lifecycle tests: null/non-GM denial, every special weather ID,
reset, cross-area isolation, independent Dalamud level, normal overrides and
invalid input rejection. These exposed that SetEventWeather accepted levels
below -1 despite the existing Lua command rejecting them. The server now also
rejects those values before changing state. This preserves the existing command
contract; it is not a newly recovered native Dalamud-level range.

All 85 weather/lifecycle/native-identity tests pass. The new guard has been
compiled/tested but is not yet deployed to the running Map process.

Local player_work bytecode confirms getWeatherId/setWeatherId simply read/write
work.weatherNow. weatherDefault is declared but this does not establish a
cutscene override restoration mechanism. Source hashes are recorded in
../evidence/weather-restoration-2026-09-20/sources.json.

### Manual live acceptance record required

Use a GM outdoors and finish each experiment with `!weather auto`. Record zone,
UTC time, displayed ET, command, visible effect, and whether the reset succeeds.
Do not describe an accepted command or chat acknowledgment as proof of visuals.

1. `!weather sandstorm 15 1`, then `!weather clear 15 1`: exercise director updates.
2. `!weather rain 7 0`: exercise the personal direct-packet/custom transition path.
3. `!weather auto`: verify normal weather resumes and prior preview clears.
4. `!zonefx 8031 0`, then `!zonefx off`: check Aurora activation/reset while level
   stays at the normal value. Separately check 8030 and 8032. Their IDs must
   never be reached by ordinary automatic selection.
5. Test each other gated ID (8014, 8027, 8028, 8029, 8065, 8066) with `!zonefx`
   only in an appropriate client area/time. Absence of an effect in an unrelated
   area/time is not proof of a transport failure. No new scope/time rules have
   been inferred for these assets.
6. Login, teleport, seamless crossing and inn entry/exit: verify destination
   environment, absence of previous-area FX, and restored ordinary music.
7. Compare displayed ET with UTC at 00/08/16 boundaries and observe normal rolls.
   Equal consecutive selections should retain the sky without forcing a change.

World-era music/Atomos/event coordination and exact climate probabilities remain
unrecovered; passing the above does not turn them into restored features.

GM test access correction: the local `blink` account (user ID 1) owns character
IDs 45–58. All 14 were added to the existing `gm_character_ids` configuration
in source and the installed Map config after user authorization. This is a
character allowlist, not automatic account-wide access for future characters.
The previous configs are backed up under `.local-evidence/restoration-backups`.

### Clock observation supplied by user

At 2026-09-20 16:37:05 America/Chicago (21:37:05 UTC), the connected
client screenshot displays 05:51 ET. Unix time 1789940225 converted at
144/7 ET seconds per real second yields 05:51:25 ET, matching the displayed
minute. This supports the current Unix/1400 block alignment at this observation;
it does not independently demonstrate all three boundary transitions. The next
08:00 ET boundary is 16:43:20 local. The user also reported that the requested
GM weather/Aurora/reset tests behaved as expected.

User subsequently confirmed the weather test worked exactly at the predicted
16:43:20 local / 08:00 ET boundary. Mark this observed boundary verified by
user report; it does not independently verify 00:00/16:00, all zone transitions,
or deployment of the later Dalamud input guard.
