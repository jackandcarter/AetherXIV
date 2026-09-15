require ("global")
require ("quests/man/man0l1")

-- Man0l1 SEQ_050 Zephyr Gate escort director. Ported from Garlemald-Server
-- scripts/lua/directors/Quest/QuestDirectorMan0l101.lua (develop), the
-- reviewed implementation of Garlemald-Server #46 — restructured from the
-- dead upstream port (whose onCreateContentArea was never called by any
-- engine): spawning lives in content/SimpleContentMan0l101.lua's onCreate
-- like every tutorial fight, and this director owns only the completion
-- beat, following the QuestDirectorMan0g101 coroutine pattern:
--
--   client noticeEvent kick (startMan0l1Content's KickEvent) →
--   onEventStarted runs the post-warp chain (fade-in dismissal →
--   EndEvent → escort go; the retail TtrBlkNml001 tutorial block is a
--   deferred cosmetic follow-up — without tutorial mode armed its
--   hardcoded-anchor engine calls crash the client) →
--   parks on "escortComplete" →
--   the content script's onUpdate fires the signal once Sisipu has led
--   the player down the zone-128 road to the lighthouse approach
--   (waypoint arrival — NOT a kill count; the ambush waves are
--   incidental) → arrival cutscene (processEvent605, the
--   wiki-documented "arrive at Oschon's Torch, enters an echo" beat)
--   → SEQ_055 journal update + "no longer bound by duty" →
--   ContentFinished teardown → warp into the lighthouse echo private
--   area (pmeteor's SEQ_055 camp shape).
--
--   The content script ALSO fires "escortComplete" on its FAIL flow
--   (Sisipu death / 30-min expiry / leave-duty onAbort) purely to
--   DRAIN this park (a stale park would double-fire the arrival on a
--   retry — the signal wakes every parked coroutine). In that case
--   the player was already rolled back to SEQ_048 and warped out, so
--   the kickEventContinue below emits a kick for a wiped director
--   actor — the client drops it, this coroutine parks on _WAIT_EVENT
--   and is later displaced, and the StartSequence(SEQ_055) tail never
--   runs. (Garlemald-Server #46.)

function init()
	return "/Director/Quest/QuestDirectorMan0l101";
end

function onEventStarted(player, director, triggerName)
	local man0l1Quest = player:GetQuest("Man0l1");
	if man0l1Quest == nil then
		player:EndEvent()
		director:EndDirector()
		return
	end

	-- Post-warp Now-Loading dismissal. The kicked noticeEvent arms a
	-- Now-Loading HOLD that only a client Lua call to MyPlayer slot 66
	-- (_fadeInNowLoadingForNoticeEventJustInArea) dismisses; the
	-- tutorials dismiss it via the same slot-66 call inside startCutScene's
	-- prologue. questBaseRewardSeting is the QuestBase-class delegate
	-- whose client body is exactly that dismissal + startFadeInCutScene-
	-- Default + wait. (Garlemald-Server #46, round 7c.)
	--
	-- NO processTtrBlkNml001 here: upstream wire-proved the client hard-
	-- dying on that delegate without tutorial mode armed (the block's
	-- hardcoded Sisipu-anchor engine calls crash the process). Cosmetic
	-- only; deferred until the leg is playable end-to-end. (#46, round 7b.)
	callClientFunction(player, "delegateEvent", player, man0l1Quest, "questBaseRewardSeting");

	-- Close the kick — post-load, like the tutorials (their noticeEvent
	-- EndEvent goes out well after the reload). An EndEvent landing
	-- mid-reload loses the client's _onPostEvent teardown and masks the
	-- menu. (#46, round 7d.)
	player:EndEvent();

	-- The escort runs from the content script's own onUpdate ticks (no
	-- cross-VM go-latch — the director and the content script are
	-- separate Lua VMs, so a global set here was invisible to onUpdate
	-- and froze Sisipu). (Garlemald-Server #46, round 7d.)

	waitForSignal(player:GetZone():GetPlayerSignal(player, "escortComplete"));

	-- Render-settle beat (Man0g101/Man0l001 pattern): without it the
	-- arrival cutscene lands in the same drain as the last death packets.
	wait(2);

	-- Reopen the event context BEFORE delegating — a bare delegate ships
	-- with owner=0 and the client echo-drops it (Man0g101 pattern).
	kickEventContinue(player, director, "noticeEvent", "noticeEvent");
	-- Arrival echo at Oschon's Torch. processEvent605 ends by arming an
	-- after-warp veil (startFadeInCutSceneAfterWarp) — it MUST be
	-- followed by the camp warp below, whose real private-area load
	-- resolves the veil.
	callClientFunction(player, "delegateEvent", player, man0l1Quest, "processEvent605");
	-- Camp handoff (retail order: journal update → 34108 → "no longer
	-- bound by duty"). StartSequence(SEQ_055) IS the journal update;
	-- 34108 "You have entered an instance." rides the engine's
	-- private-area warp path. The unbind line goes out BEFORE the
	-- EndEvent + warp — a game message queued after the warp ships into
	-- the client's Now-Loading gap (the man0l0 Hob-crash shape). Id 50012
	-- "You are no longer bound by duty." (Garlemald-Server #46.)
	man0l1Quest:StartSequence(SEQ_055);
	player:SendGameMessage(GetWorldMaster(), TEXT_UNBOUND_FROM_DUTY, 0x20);
	-- EndEvent BEFORE the warp. (#46.)
	player:EndEvent();
	player:GetZone():ContentFinished();
	-- pmeteor's SEQ_055 lighthouse camp shape: DoZoneChange(128,
	-- 'PrivateAreaMasterPast', 2, 15, 137.44, 60.33, 1322.0, -1.60) —
	-- the private area seeded by migration 000036.
	GetWorldManager():DoZoneChange(player, 128, "PrivateAreaMasterPast", 2, 15, 137.44, 60.33, 1322.0, -1.60);
end

function main()
end
