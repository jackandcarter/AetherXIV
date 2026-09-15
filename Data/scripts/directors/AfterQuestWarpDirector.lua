require("global")

function init()
	return "/Director/AfterQuestWarpDirector";
end

function onEventStarted(player, director, eventType, eventName)
	-- The destination notice is only a carrier. The owning quest controls the
	-- client function and the matching EndEvent, exactly as the legacy quest
	-- scripts expect.
	if (player:HasQuest(110002) == true) then
		local quest = player:GetQuest(110002);
		if (quest ~= nil) then
			quest:OnNotice(player);
			return;
		end
	elseif (player:HasQuest(110006) == true) then
		local quest = player:GetQuest(110006);
		if (quest ~= nil) then
			quest:OnNotice(player);
			return;
		end
	end

	-- A stale kick has no quest transaction to resume.
	player:EndEvent();
end

function main()
end
