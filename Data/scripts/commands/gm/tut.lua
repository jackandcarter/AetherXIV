require("global");
require("tutorial");

-- Live tutorial debugger for the opening quests. Designed to answer two
-- questions while a player is stuck at the linkpearl step:
--   1. Is the notice session still alive, and what did the server
--      actually publish for the tutorial's inputs (initialTown)?
--   2. Does the client open widget 15 when asked directly (SendDataPacket)
--      versus when asked through the retail quest dispatch (delegateEvent
--      processEventTu_001)?
-- Subcommands:
--   state  - report initialTown, active opening quest + sequence, and the
--            current event session (owner/name/type) if one is parked.
--   open   - re-dispatch processEventTu_001 on the opening quest through the
--            current event session (retail path; blocked if no session).
--   retry  - re-present the linkpearl tutorial with a type-5 noticeEvent
--            kick on the AfterQuestWarpDirector, exactly like the quest's
--            Miounne onTalk handoff (recreating the director after a relog
--            when the quest-created instance has ended). The director routes
--            it to the quest's onNotice, which arms tutorial mode
--            (startTutorialMode), dispatches processEventTu_001 parked until
--            the Confirm's completion EventUpdate, and ends only after that
--            update. Use this when the player is stuck at the linkpearl
--            step with the notice already closed.
--   direct - bypass the quest dispatch: ask the client to open tutorial 15.
--   mode   - enter tutorial mode directly (SendDataPacket 9).
--   end    - close the tutorial and end tutorial mode (escape hatch).

properties = {
    permissions = 0,
    parameters = "ss",
    description = "Tutorial debug. Args: state | open | retry | direct | mode | end [textId]",
}

function onTrigger(player, argc, subcommand, arg2)
    local cmd = subcommand or "state";

    -- Locate the active opening quest (Gridania 110006, Limsa 110002, Ul'dah 110010).
    local quest = nil;
    local questName = "";
    if (player:HasQuest(110006)) then
        quest = player:GetQuest(110006);
        questName = "Man0g1";
    elseif (player:HasQuest(110002)) then
        quest = player:GetQuest(110002);
        questName = "Man0l1";
    elseif (player:HasQuest(110010)) then
        quest = player:GetQuest(110010);
        questName = "Man0u1";
    end

    if (cmd == "state") then
        local msg = string.format("tut: initialTown=%d quest=%s",
            player:GetInitialTown(), questName);
        if (quest ~= nil) then
            msg = msg .. string.format(" seq=%d", quest:GetSequence());
        end
        msg = msg .. string.format(" eventOwner=0x%X eventName=%s eventType=%d",
            player.currentEventOwner, player.currentEventName, player.currentEventType);
        player:SendMessage(0x20, "", msg);
    elseif (cmd == "open") then
        -- Retail path: re-dispatch processEventTu_001 inside whatever event
        -- session is currently parked, parked on the client's completion
        -- EventUpdate (callClientFunction). If no session is active the
        -- dispatch is blocked and logged as event.runFunction.blocked.
        if (quest ~= nil) then
            callClientFunction(player, "delegateEvent", player, quest, "processEventTu_001");
            player:SendMessage(0x20, "", "tut: dispatched processEventTu_001 (see event.runFunction/blocked in log)");
        else
            player:SendMessage(0x20, "", "tut: no opening quest active");
        end
    elseif (cmd == "retry") then
        -- Re-present the linkpearl tutorial the same way the shipped quest
        -- hands off at the Miounne talk: kick a type-5 noticeEvent on the
        -- AfterQuestWarpDirector so its onEventStarted routes it to the
        -- owning quest's onNotice, which arms tutorial mode
        -- (startTutorialMode), dispatches processEventTu_001 parked on the
        -- Confirm's completion EventUpdate, and ends only after that update.
        -- The quest creates that director during onTalk; after a relog the
        -- director has ended with the session, so recreate it exactly the
        -- way onTalk does when it is missing.
        local director = player:GetDirector("AfterQuestWarpDirector");
        if (director == nil) then
            local zone = GetWorldManager():GetZone(player:GetZoneID());
            if (zone ~= nil) then
                director = zone:CreateDirector("AfterQuestWarpDirector", false);
            end
            if (director ~= nil) then
                director:StartDirector(true);
                player:AddDirector(director);
            end
        end
        if (director ~= nil) then
            -- The client only learns about this director through the actor
            -- records the zone-in bundle queues for owned directors (Player
            -- SendZoneInPackets: GetSpawnPackets + GetInitPackets +
            -- GetSetEventStatusPackets). In the stuck state there is no
            -- zone change, so present the director the same way before the
            -- notice kick; otherwise the client silently ignores an event
            -- for an actor it does not know (live 2026-08-11 1318Z: kick
            -- sent, zero event.start from the client).
            player:SendDirectorPackets(director);
            player:KickEvent(director, "noticeEvent", true);
            player:SendMessage(0x20, "", "tut: presented director, kicked type-5 noticeEvent");
        else
            player:SendMessage(0x20, "", "tut: could not obtain AfterQuestWarpDirector");
        end
    elseif (cmd == "direct") then
        -- Bypass the quest dispatch entirely: arm tutorial mode first (the
        -- client requires it before a widget dock), then ask the client to
        -- open the linkpearl tutorial (widget 15) directly.
        startTutorialMode(player);
        openTutorialWidget(player, CONTROLLER_KEYBOARD, TUTORIAL_NPCLS);
        player:SendMessage(0x20, "", "tut: sent startTutorialMode + openTutorialWidget(1, 15)");
    elseif (cmd == "mode") then
        startTutorialMode(player);
        player:SendMessage(0x20, "", "tut: sent startTutorialMode");
    elseif (cmd == "end") then
        showTutorialSuccessWidget(player, tonumber(arg2) or 9080);
        closeTutorialWidget(player);
        endTutorialMode(player);
        player:SendMessage(0x20, "", "tut: closed widget and ended tutorial mode");
    else
        player:SendMessage(0x20, "", "tut: unknown subcommand '" .. cmd .. "'");
    end
end
