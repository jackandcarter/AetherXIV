require("global")

function init()
	return "/Director/AfterQuestWarpDirector";
end

function onEventStarted(player, director, eventType, eventName)
	if (player:HasQuest(110006) == true) then
		local quest = player:GetQuest(110006);
		if (quest ~= nil and quest:GetSequence() == 5) then
			-- processEventTu_001 owns a response-bearing tutorial transaction.
			-- Keep the director notice alive until the client returns its
			-- EventUpdate; ending it here clears the client-side event owner
			-- while the Confirm window is still docking.
			callClientFunction(player, "delegateEvent", player, quest, "processEventTu_001");
			player:EndEvent();
			return;
		end
	end

	-- A stale kick has no tutorial transaction to launch.
	player:EndEvent();
end

function main()
end
