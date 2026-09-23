require ("global")
require ("modifiers")
-- onUpdate drives ally/mob engagement through allyGlobal.EngageTarget;
-- without this require the global is nil and every tick errors
-- (swallowed by the ticker drain). Same shape as SimpleContent30002.
require ("ally")
-- SEQ_* constants + the escort TEXT_*/SAY_* constants (proven loadable
-- outside the quest runtime — QuestDirectorMan0l101 requires it too).
require ("quests/man/man0l1")

-- Man0l1 "Treasures of the Main" SEQ_050 — the Zephyr Gate → Oschon's
-- Torch escort duty. Ported from Garlemald-Server
-- scripts/lua/content/SimpleContentMan0l101.lua (develop), the reviewed
-- and live-validated implementation of Garlemald-Server #46: net-new
-- content (upstream pmeteor shipped the trigger arm commented out), built
-- on packet-capture forensics and 1.23b playthrough recordings. Data side:
-- db/direct-core/migrations/20260913_000036 (classes 1090004/2290007/
-- 2205603/1290003, BNPC pool/group 5/6, spawns 25-33, zone-128 camp PA).
-- Adaptation for this stack only: signals are delivered through
-- area:GetPlayerSignal(owner, "escortComplete") — the verified
-- director-park contract this engine already runs for the Gridania
-- escort (SimpleContentMan0g101.lua / QuestDirectorMan0g101.lua); the
-- upstream global-name signal shape does not exist here.

-- Escort pacing / geometry. The onUpdate driver ticks every 500 ms
-- (same content ticker the Gridania escort runs on), so
-- ticks = seconds * 2.
WALK_STEP = 1.6;             -- units per 500 ms tick (escort walking pace)
RUN_STEP = 3.4;              -- units per 500 ms tick (run pace)
HOLD_RADIUS = 28.0;          -- live mob within this of Sisipu/player → hold the walk
ENGAGE_RADIUS = 18.0;        -- mob pulls onto the escort party inside this
PLAYER_LEASH = 15.0;         -- Sisipu > this from the player → hold and chide
WAYPOINT_RADIUS = 4.0;       -- close enough to the lead point → stop stepping
ARRIVAL_RADIUS = 20.0;       -- player inside this of the lighthouse goal → arrival
LEAD_DISTANCE = 5.0;         -- how far ahead of the player Sisipu paces while walking
MOVE_EPSILON = 0.3;          -- player displacement/tick above this = "walking"
TICKS_PER_SECOND = 2;
ESCORT_LIMIT_MINUTES = 30;   -- retail: "There are 30 minutes remaining."
ESCORT_LIMIT_TICKS = ESCORT_LIMIT_MINUTES * 60 * TICKS_PER_SECOND;
REMIND_20MIN_TICKS = 10 * 60 * TICKS_PER_SECOND;    -- 20 minutes remaining
REMIND_10MIN_TICKS = 20 * 60 * TICKS_PER_SECOND;    -- 10 minutes remaining
REMIND_5MIN_TICKS  = 25 * 60 * TICKS_PER_SECOND;    -- 5 minutes remaining
BARK_INTERVAL_TICKS = 40;                           -- guidance bark every ~20 s
ESCORT_DUTY_MUSIC = 37;      -- "Daring Dalliances" — 1.x quest-duty theme
HURT_HP_RATIO = 0.5;         -- at/below → "Ow ow ow..." (took a beating)
CLOSE_HP_RATIO = 0.85;       -- at/below → "Phew... much closer than I'd have liked"
-- Minimap duty halo: an invisible ContentPrivateAreaRange object (class
-- 1290003, seeded with the retail exit/caution circles by migration
-- 000036). The client attaches a native range map marker to any
-- non-talkable actor whose class script defines getMapMarkerRange(),
-- radii resolved from the wire-sent named push circles — so the halo
-- rides this object, and MoveTo'ing it along with Sisipu moves the quest
-- area ring on the minimap.
ESCORT_RING_CLASS = 1290003;
RING_MOVE_TICKS = 1;         -- re-home the halo EVERY 500 ms frame

-- Every escort ambusher is the same chigoe class 2205603, whose
-- gamedata_actor_class displayNameId is 3205603 ("ankle biter"). The
-- engaged/defeated rows 30120/30121 take their subject from the
-- DispId-sender wire family (Player.SendGameMessageDisplayIDSender) —
-- a STATIC name-sheet id in the packet header; a runtime mob actor id
-- in the LuaParam slot leaves the name BLANK (live-proven upstream,
-- Garlemald-Server #199 round 2).
ANKLE_BITER_DISPLAY_ID = 3205603;

-- Sisipu's path: the PLAYER'S OWN recorded footsteps, decoded from the
-- recorded walk's inbound 0x00CA position packets (offset verified
-- against the warp anchor -63.25/33.16/164.51) and downsampled to
-- ~20-unit breadcrumbs: every point is REAL walked ground with true
-- terrain Y, so tracing them can never leave walkable land or float.
-- ARRIVAL_GOAL is pmeteor's SEQ_055 camp point.
TRAIL = {
	{ x = -63.25, y = 33.16, z = 164.51 },
	{ x = -45.02, y = 36.92, z = 155.90 },
	{ x = -26.65, y = 41.26, z = 147.70 },
	{ x = -6.55, y = 45.29, z = 140.92 },
	{ x = 14.93, y = 44.31, z = 140.65 },
	{ x = 36.27, y = 43.91, z = 139.11 },
	{ x = 55.80, y = 46.38, z = 134.14 },
	{ x = 76.48, y = 46.37, z = 129.19 },
	{ x = 97.21, y = 44.13, z = 131.30 },
	{ x = 110.60, y = 47.14, z = 147.63 },
	{ x = 130.32, y = 46.20, z = 155.32 },
	{ x = 149.47, y = 47.23, z = 164.72 },
	{ x = 159.39, y = 49.46, z = 182.96 },
	{ x = 161.77, y = 49.34, z = 203.54 },
	{ x = 154.03, y = 47.11, z = 222.07 },
	{ x = 145.69, y = 44.96, z = 241.43 },
	{ x = 139.74, y = 45.40, z = 261.82 },
	{ x = 133.33, y = 44.95, z = 281.98 },
	{ x = 123.39, y = 45.42, z = 300.94 },
	{ x = 110.17, y = 45.22, z = 317.74 },
	{ x = 98.67, y = 42.78, z = 335.21 },
	{ x = 87.31, y = 40.79, z = 353.31 },
	{ x = 80.17, y = 44.02, z = 373.29 },
	{ x = 73.08, y = 45.07, z = 393.45 },
	{ x = 70.13, y = 43.66, z = 414.70 },
	{ x = 70.11, y = 41.50, z = 435.66 },
	{ x = 69.88, y = 41.37, z = 456.91 },
	{ x = 70.77, y = 43.35, z = 477.76 },
	{ x = 87.71, y = 41.27, z = 489.08 },
	{ x = 108.77, y = 42.33, z = 492.30 },
	{ x = 124.48, y = 46.35, z = 506.46 },
	{ x = 131.73, y = 45.07, z = 526.60 },
	{ x = 139.03, y = 47.06, z = 546.87 },
	{ x = 139.60, y = 53.36, z = 567.82 },
	{ x = 134.90, y = 53.17, z = 588.17 },
	{ x = 134.03, y = 51.99, z = 608.77 },
	{ x = 131.58, y = 54.46, z = 629.89 },
	{ x = 127.75, y = 54.47, z = 650.68 },
	{ x = 123.68, y = 52.42, z = 671.72 },
	{ x = 121.86, y = 55.21, z = 692.62 },
	{ x = 118.85, y = 57.59, z = 713.07 },
	{ x = 106.80, y = 63.79, z = 729.79 },
	{ x = 86.30, y = 64.42, z = 730.03 },
	{ x = 72.22, y = 60.52, z = 745.81 },
	{ x = 58.58, y = 56.03, z = 761.97 },
	{ x = 53.61, y = 59.13, z = 782.49 },
	{ x = 48.21, y = 63.05, z = 802.34 },
	{ x = 27.39, y = 64.30, z = 805.55 },
	{ x = 7.02, y = 59.63, z = 810.46 },
	{ x = 2.98, y = 54.50, z = 831.35 },
	{ x = 3.93, y = 53.68, z = 852.32 },
	{ x = -1.11, y = 53.17, z = 873.06 },
	{ x = -6.17, y = 54.44, z = 893.87 },
	{ x = -14.89, y = 50.45, z = 912.89 },
	{ x = -22.95, y = 47.63, z = 932.25 },
	{ x = -18.26, y = 45.09, z = 951.77 },
	{ x = -6.85, y = 45.96, z = 969.73 },
	{ x = 6.68, y = 44.60, z = 985.20 },
	{ x = 19.95, y = 45.07, z = 1002.12 },
	{ x = 33.13, y = 44.08, z = 1018.93 },
	{ x = 46.17, y = 43.14, z = 1035.55 },
	{ x = 57.56, y = 44.11, z = 1053.79 },
	{ x = 63.09, y = 46.25, z = 1073.98 },
	{ x = 66.75, y = 46.46, z = 1095.15 },
	{ x = 72.18, y = 44.73, z = 1116.02 },
	{ x = 80.52, y = 44.54, z = 1135.35 },
	{ x = 95.82, y = 44.01, z = 1150.23 },
	{ x = 111.13, y = 44.66, z = 1165.12 },
	{ x = 123.30, y = 44.07, z = 1181.26 },
	{ x = 124.53, y = 46.16, z = 1202.12 },
	{ x = 114.03, y = 46.12, z = 1220.71 },
	{ x = 107.51, y = 50.24, z = 1241.09 },
	{ x = 113.16, y = 57.56, z = 1261.33 },
	{ x = 123.01, y = 62.47, z = 1278.80 },
	{ x = 127.12, y = 61.61, z = 1298.65 },
	{ x = 137.44, y = 60.33, z = 1322.00 },
	{ x = 137.74, y = 60.41, z = 1323.04 },
	{ x = 136.44, y = 60.70, z = 1324.01 },
};
ARRIVAL_GOAL = { x = 137.44, y = 60.33, z = 1322.0 };

-- Sisipu's bark slots — man0l1 QUEST-sheet rows decoded from the client
-- text sheets (Garlemald-Server #46). Delivery = emitBark →
-- owner:SendGameMessage(quest, sayId, 0x20) with the Man0l1 quest actor
-- as text owner.
BARKS = {
	dutyStart  = 105,   -- "Excited yet? I know I am. Now let us be off!..."
	guidance   = SAY_ESCORT_GUIDANCE,   -- 283 "Oschon's Torch is due south..."
	waveClean  = 286,   -- "Good show! Perhaps you are not as useless..."
	waveClose  = 287,   -- "Phew... That was much closer than I'd have liked..."
	waveThanks = 288,   -- "Thank the Navigator! Oh, and thank you, as well."
	sisipuHurt = 289,   -- "Ow ow ow... That's going to leave a nasty bruise..."
	dawdle     = 285,   -- "Now put away your toys and come along, <name>..."
	arrival    = 290,   -- "We've finally arrived...and in one piece!"
};

-- Lifecycle state must live on the content instance. The engine invokes
-- onCreate and onUpdate through separate Lua VM contexts, so a Lua-global
-- table is not a valid persistence boundary. This proxy deliberately keeps
-- scalar fields out of the VM and in PrivateAreaContent.SetScriptState /
-- GetScriptState, while retaining the existing Zone.Update scheduler.
local function newState(area)
	return setmetatable({
		mobsLive = {},       -- rebuilt from the live roster for this callback
		mobsEngaged = {},    -- engagement announcements for this callback
	}, {
		__index = function(_, key)
			local value = area:GetScriptState(key);
			if key == "done" or key == "reminded20" or key == "reminded10" or
				key == "reminded5" or key == "sawEscort" or key == "waveEscortFought" or
				key == "trailInit" then
				return value ~= 0;
			end
			if value == 0 and (key == "startTick" or key == "lastBarkTick" or
				key == "lastRingTick" or key == "lastOwnerX" or key == "lastOwnerZ") then
				return nil;
			end
			return value;
		end,
		__newindex = function(_, key, value)
			if value == nil then
				value = 0;
			elseif value == true then
				value = 1;
			elseif value == false then
				value = 0;
			end
			area:SetScriptState(key, value);
		end,
	});
end

function onCreate(starterPlayer, contentArea, director)
	local state = newState(contentArea);
	state.done = false;        -- terminal latch (arrival signalled OR failed)
	state.wpIndex = 1;        -- next TRAIL breadcrumb (re-seated to nearest on first tick)
	state.startTick = nil;    -- latched on the first onUpdate tick (post-warp)
	state.lastBarkTick = nil;
	state.lastRingTick = nil;
	state.lastNear = 0;       -- live-mob count near the party last tick (wave-outcome beat)
	state.reminded20 = false;
	state.reminded10 = false;
	state.reminded5 = false;
	state.sawEscort = false;  -- Sisipu observed alive at least once (death detection)
	state.waveEscortFought = false;
	state.trailInit = false;
	state.lastOwnerX = nil;
	state.lastOwnerZ = nil;
	state.ringActorId = 0;    -- minimap-halo object id (re-acquired from the roster per tick)

	-- Zone-128 route spawns (migration 000036: Sisipu bnpc 25 beside the
	-- gate-side warp-in point; biters 26-33 one per ambush point).
	sisipu = GetWorldManager().SpawnBattleNpcById(25, contentArea);
	local mobs = {};
	for bnpcId = 26, 33 do
		table.insert(mobs, GetWorldManager().SpawnBattleNpcById(bnpcId, contentArea));
	end

	-- Minimap duty halo: spawned at Sisipu's seed position and MoveTo'd
	-- alongside her every frame in onUpdate. Only the ID is stored —
	-- each tick re-acquires a queue-bound handle from the roster.
	local ring = contentArea:SpawnActor(ESCORT_RING_CLASS, "escortAreaRange", -49.0, 36.43, 162.0, 0);
	state.ringActorId = ring.actorId;

	-- Active MainState so Sisipu stands and the ankle biters render
	-- hostile (tutorial-fight pattern).
	sisipu:ChangeState(2);
	for i = 1, #mobs do
		mobs[i]:ChangeState(2);
	end

	-- NO party-add here. onCreate runs PRE-warp, and a party-add
	-- broadcasts a content/party group trio referencing Sisipu before
	-- the client has spawned her. The HUD HP-bar party-add is a cosmetic
	-- follow-up once the warp itself is solid. (Garlemald-Server #46.)

	-- The PLAYER keeps the tutorial-style 1-HP floor (retail wipes are
	-- a rez-and-retry). Sisipu takes REAL damage — her death is the
	-- retail fail condition, detected in onUpdate.
	starterPlayer:SetMod(modifiersGlobal.MinimumHpLock, 1);

	director:AddMember(starterPlayer);
	director:AddMember(director);
	director:AddMember(sisipu);
	director:AddMember(ring);
	for i = 1, #mobs do
		director:AddMember(mobs[i]);
	end
end

-- Duty music override, post-warp (load-gap-safe hook — fires after the
-- bundle flush; the completion camp warp and the fail eject both restore
-- the destination's DB music on zone-in). (Garlemald-Server #46, round 9.)
function onZoneIn(player, contentArea, director)
	player:ChangeMusic(ESCORT_DUTY_MUSIC);
end

function onDestroy()
end

local function dist2d(ax, az, bx, bz)
	local dx = ax - bx;
	local dz = az - bz;
	return math.sqrt(dx * dx + dz * dz);
end

-- Sisipu say-line delivery: quest-sheet text via the server-side
-- SendGameMessage mechanism, text owner = the Man0l1 quest actor.
local function emitBark(owner, sayId)
	if (sayId == nil) then
		return;
	end
	local quest = owner:GetQuest("Man0l1");
	owner:SendGameMessage(quest, sayId, 0x20);
end

-- FAIL flow (Sisipu died / 30-minute expiry / confirmed leave-duty):
-- roll the quest back to SEQ_048 — its onStateChange re-arms the
-- ZEPHYR_TRIGGER push circle, so the duty is retryable from the gate —
-- unbind message, tear the instance down, eject to the gate.
local function escortFail(owner, area, state)
	state.done = true;
	local quest = owner:GetQuest("Man0l1");
	quest:StartSequence(SEQ_048);
	owner:SendGameMessage(GetWorldMaster(), TEXT_UNBOUND_FROM_DUTY, 0x20);
	-- ContentFinished BEFORE the warp-out; the eject is a public-area
	-- warp back to the gate anchor.
	area:ContentFinished();
	GetWorldManager():WarpToPublicArea(owner, -63.25, 33.15, 164.51, 0.8);
	-- Drain the director coroutine parked on "escortComplete" so a stale
	-- park can't double-fire the arrival flow on a later retry. The
	-- woken coroutine's kickEventContinue emits a kick for the just-wiped
	-- director actor — the client drops it, the coroutine parks on
	-- _WAIT_EVENT and is displaced by the player's next event, and its
	-- StartSequence(SEQ_055) tail sits AFTER the never-answered
	-- callClientFunction, so no quest state moves.
	sendSignal(area:GetPlayerSignal(owner, "escortComplete"));
end

function onUpdate(tick, area)
	if not area then return end
	local players = area:GetPlayers()
	local mobs    = area:GetMonsters()   -- live-only (dead filtered)
	local allies  = area:GetAllies()

	local owner = nil
	for player in players do
		if player then owner = owner or player end
	end
	if not owner then return end
	local state = newState(area)
	if not state then
		GetLuaInstance():TraceContentEscort(
			"missing-state", owner, nil, #players, #allies, #mobs,
			0, 0, 0, 0, false)
		return
	end
	if state.done then return end
	local escort = nil
	for ally in allies do
		escort = escort or ally
	end
	GetLuaInstance():TraceContentEscort(
		"roster", owner, escort, #players, #allies, #mobs,
		0, state.wpIndex or 0, 0, 0, false)

	-- The ticker already parks the content driver on the post-warp ack,
	-- so the escort runs from the first post-ack tick. No cross-VM
	-- go-latch: the director and this content script are separate VMs.
	-- (Garlemald-Server #46, round 7d.)

	-- ---- Timer (retail: 30-minute limit) ----
	state.startTick = state.startTick or tick;
	local elapsed = tick - state.startTick;
	if elapsed >= ESCORT_LIMIT_TICKS then
		escortFail(owner, area, state);
		return;
	end
	if not state.reminded20 and elapsed >= REMIND_20MIN_TICKS then
		state.reminded20 = true;
		owner:SendGameMessage(GetWorldMaster(), TEXT_TIME_REMAINING, 0x20, 20);
	end
	if not state.reminded10 and elapsed >= REMIND_10MIN_TICKS then
		state.reminded10 = true;
		owner:SendGameMessage(GetWorldMaster(), TEXT_TIME_REMAINING, 0x20, 10);
	end
	if not state.reminded5 and elapsed >= REMIND_5MIN_TICKS then
		state.reminded5 = true;
		owner:SendGameMessage(GetWorldMaster(), TEXT_TIME_REMAINING, 0x20, 5);
	end

	-- ---- Sisipu death = fail (rosters are live-only, so a dead
	-- escort simply vanishes from GetAllies) ----
	-- CLR actor collections are iterated directly; they are not Lua
	-- one-based arrays.
	local escort = nil
	for ally in allies do
		escort = escort or ally
	end
	if escort then
		if not state.sawEscort then
			-- First live sighting = the duty is underway. Retail entry
			-- order: journal → 34108 (engine-side) → protect → bound →
			-- timer, then Sisipu's send-off. 51005 resolves
			-- "<displayName>" from the actor-id param — this is why the
			-- banner set lives HERE and not in the director.
			owner:SendGameMessage(GetWorldMaster(), TEXT_PROTECT_SISIPU, 0x20, escort.actorId);
			owner:SendGameMessage(GetWorldMaster(), TEXT_BOUND_BY_DUTY, 0x20);
			owner:SendGameMessage(GetWorldMaster(), TEXT_TIME_REMAINING, 0x20, ESCORT_LIMIT_MINUTES);
			emitBark(owner, BARKS.dutyStart);
		end
		state.sawEscort = true;
	elseif state.sawEscort then
		escortFail(owner, area, state);
		return;
	else
		-- Pre-onCreate tick (roster not populated yet) — wait.
		GetLuaInstance():TraceContentEscort(
			"missing-escort", owner, nil, #players, #allies, #mobs,
			0, state.wpIndex or 0, 0, 0, false)
		return;
	end

	-- ---- Ambush points ----
	-- The eight seeded biters sit ONE PER AMBUSH POINT at even ninths of
	-- the recorded arc, so the engage radius activates exactly the point
	-- whose stretch the party has reached: the live ankle biter inside
	-- ENGAGE_RADIUS of the escort or the player pulls alone; Sisipu
	-- fights back like the tutorial allies do.
	local function positionLive(a)
		return a and not (a.positionX == 0 and a.positionY == 0 and a.positionZ == 0);
	end
	if not positionLive(escort) or not positionLive(owner) then
		GetLuaInstance():TraceContentEscort(
			"unsynchronized-position", owner, escort, #players, #allies, #mobs,
			0, state.wpIndex or 0, 0, 0, false)
		return;
	end

	local nearLive = 0
	local anyEngaged = false
	local nearestMob, nearestD = nil, math.huge
	local ring = nil
	local liveIds = {}
	local mobIndex = 0
	for mob in mobs do
		mobIndex = mobIndex + 1
		if mob and state.ringActorId ~= nil and mob.actorId == state.ringActorId then
			ring = mob;
		elseif mob and positionLive(mob) then
			liveIds[mob.actorId] = true;
			local dMob = math.min(
				dist2d(mob.positionX, mob.positionZ, escort.positionX, escort.positionZ),
				dist2d(mob.positionX, mob.positionZ, owner.positionX, owner.positionZ))
			if dMob <= HOLD_RADIUS then
				nearLive = nearLive + 1
			end
			if mob:IsEngaged() then
				anyEngaged = true
				if not state.mobsEngaged[mob.actorId] then
					state.mobsEngaged[mob.actorId] = true;
					owner:SendGameMessageLocalizedDisplayName(GetWorldMaster(), TEXT_MOB_ENGAGED, 0x20, ANKLE_BITER_DISPLAY_ID);
				end
			end
			if dMob < nearestD then
				nearestMob, nearestD = mob, dMob
			end
			if dMob <= ENGAGE_RADIUS and not mob:IsEngaged() then
				allyGlobal.EngageTarget(mob, (mobIndex % 2 == 0) and escort or owner)
			end
		end
	end
	-- "Ankle biter is defeated." — the roster is live-only, so a
	-- previously-seen biter vanishing from it died this tick.
	for id in pairs(state.mobsLive) do
		if not liveIds[id] then
			owner:SendGameMessageLocalizedDisplayName(GetWorldMaster(), TEXT_MOB_DEFEATED, 0x20, ANKLE_BITER_DISPLAY_ID);
		end
	end
	-- The live roster is authoritative for this tick; lifecycle scalar
	-- state remains on the content area. Do not assign this temporary Lua
	-- table through the scalar state proxy.

	-- Minimap halo follows Sisipu EVERY tick, hoisted before the movement
	-- early-returns so the halo tracks her while walking AND while pinned
	-- fighting an ambush wave. moveState 2 (running) — the client glides
	-- a state-0 MoveTo instead of snapping, and the halo would lag a
	-- running Sisipu off the minimap. (Garlemald-Server #199.)
	if ring ~= nil and (state.lastRingTick == nil or tick - state.lastRingTick >= RING_MOVE_TICKS) then
		state.lastRingTick = tick;
		ring:MoveTo(escort.positionX, escort.positionY, escort.positionZ, 0.0, 2);
	end

	-- Sisipu joins the fight against the NEAREST in-range mob only (not
	-- the first roster entry, which could be an unstreamed far cluster —
	-- ranged combat against an invisible attacker).
	if not escort:IsEngaged() and nearestMob ~= nil and nearestD <= ENGAGE_RADIUS then
		allyGlobal.EngageTarget(escort, nearestMob)
	end

	-- Ambush-outcome beat: the contested count dropping back to zero =
	-- this ambush point was cleared this tick. Variant by how the fight
	-- went for Sisipu.
	if escort:IsEngaged() then
		state.waveEscortFought = true;
	end
	if nearLive == 0 and state.lastNear > 0 then
		local hp, maxHp = escort:GetHP(), escort:GetMaxHP();
		local ratio = (maxHp > 0) and (hp / maxHp) or 1.0;
		if ratio <= HURT_HP_RATIO then
			emitBark(owner, BARKS.sisipuHurt);
		elseif ratio <= CLOSE_HP_RATIO then
			emitBark(owner, BARKS.waveClose);
		elseif state.waveEscortFought then
			emitBark(owner, BARKS.waveThanks);
		else
			emitBark(owner, BARKS.waveClean);
		end
		state.waveEscortFought = false;
	end
	state.lastNear = nearLive;
	local dPlayer = dist2d(escort.positionX, escort.positionZ, owner.positionX, owner.positionZ);
	GetLuaInstance():TraceContentEscort(
		"combat-evaluated", owner, escort, #players, #allies, #mobs,
		dPlayer, state.wpIndex or 0, 0, nearLive, anyEngaged or escort:IsEngaged())

	-- ---- Hold while contested: any live mob engaged with the party, or
	-- still lurking inside the hold radius, pins her ----
	if nearLive > 0 or anyEngaged or escort:IsEngaged() then
		GetLuaInstance():TraceContentEscort(
			"hold-combat", owner, escort, #players, #allies, #mobs,
			dPlayer, state.wpIndex or 0, 0, nearLive, anyEngaged or escort:IsEngaged())
		return
	end

	-- ---- Arrival: the PLAYER reaching the lighthouse approach ends the
	-- duty (Sisipu is leashed below, so she is always in range too). →
	-- arrival bark → the director's kickEventContinue machinery
	-- (processEvent605 echo → SEQ_055 → camp warp) takes over. ----
	if dist2d(owner.positionX, owner.positionZ, ARRIVAL_GOAL.x, ARRIVAL_GOAL.z) <= ARRIVAL_RADIUS then
		state.done = true;
		emitBark(owner, BARKS.arrival);
		sendSignal(area:GetPlayerSignal(owner, "escortComplete"));
		return;
	end

	-- ---- Sisipu TRACES the player's recorded footsteps ----

	-- One-time: start from the trail point nearest her spawn (the first
	-- breadcrumb is the warp-in spot behind her seed position).
	if not state.trailInit then
		state.trailInit = true;
		local bestI, bestD = 1, math.huge;
		for i = 1, #TRAIL do
			local d = dist2d(escort.positionX, escort.positionZ, TRAIL[i].x, TRAIL[i].z);
			if d < bestD then bestI, bestD = i, d; end
		end
		state.wpIndex = bestI;
	end

	if dPlayer > PLAYER_LEASH then
		GetLuaInstance():TraceContentEscort(
			"hold-leash", owner, escort, #players, #allies, #mobs,
			dPlayer, state.wpIndex or 0, 0, nearLive, false)
		-- Player lagging (fight, detour) — she holds and (rate-limited)
		-- chides rather than walking off.
		if state.lastBarkTick == nil or tick - state.lastBarkTick >= BARK_INTERVAL_TICKS then
			state.lastBarkTick = tick;
			emitBark(owner, BARKS.dawdle);
		end
		return;
	end

	if state.wpIndex <= #TRAIL then
		-- ON-TRAIL: step toward the next recorded breadcrumb at ITS
		-- recorded ground Y, at RUN pace. Skip THROUGH every breadcrumb
		-- already within radius in one tick, then move toward the first
		-- genuinely-ahead one.
		local wp, d;
		repeat
			wp = TRAIL[state.wpIndex];
			d = wp and dist2d(escort.positionX, escort.positionZ, wp.x, wp.z) or nil;
			if d ~= nil and d <= WAYPOINT_RADIUS then
				state.wpIndex = state.wpIndex + 1;
			end
		until wp == nil or d == nil or d > WAYPOINT_RADIUS or state.wpIndex > #TRAIL;
		if wp ~= nil and d ~= nil and d > WAYPOINT_RADIUS then
			GetLuaInstance():TraceContentEscort(
				"move-command", owner, escort, #players, #allies, #mobs,
				dPlayer, state.wpIndex or 0, d, nearLive, false)
			local dx = (wp.x - escort.positionX) / d;
			local dz = (wp.z - escort.positionZ) / d;
			local step = math.min(RUN_STEP, d);
			escort:MoveTo(escort.positionX + dx * step, wp.y, escort.positionZ + dz * step,
				math.atan(dx, dz), 2);
		end
	else
		-- TRAIL EXHAUSTED: player-relative follow as a safety fallback
		-- only (the trail ends at the lighthouse, inside the arrival
		-- radius, so this branch is normally unreachable).
		local py = owner.positionY;
		local last = state.lastOwnerX and { x = state.lastOwnerX, z = state.lastOwnerZ } or nil;
		local moved = last and dist2d(owner.positionX, owner.positionZ, last.x, last.z) or 0;
		if dPlayer > LEAD_DISTANCE and moved > MOVE_EPSILON then
			local d = math.max(dPlayer, 0.001);
			local dx = (owner.positionX - escort.positionX) / d;
			local dz = (owner.positionZ - escort.positionZ) / d;
			local step = math.min(RUN_STEP, dPlayer);
			escort:MoveTo(escort.positionX + dx * step, py, escort.positionZ + dz * step,
				math.atan(dx, dz), 2);
		end
	end
	-- The content-area state API stores integral lifecycle values. The
	-- trail normally reaches arrival before the player-relative fallback;
	-- do not persist floating-point coordinates through that API.

	-- Guidance bark ("Oschon's Torch is due south...") every ~20 s while
	-- escorting — man0l1 sheet row 283.
	if state.lastBarkTick == nil or tick - state.lastBarkTick >= BARK_INTERVAL_TICKS then
		state.lastBarkTick = tick;
		emitBark(owner, BARKS.guidance);
	end
end

-- Leave-duty teardown (the confirmed-leave command path): the same
-- eject-and-retry flow as a timeout/death fail.
function onAbort(player, contentArea, director)
	local state = newState(contentArea);
	if state.done then
		return;
	end
	escortFail(player, contentArea, state);
end
