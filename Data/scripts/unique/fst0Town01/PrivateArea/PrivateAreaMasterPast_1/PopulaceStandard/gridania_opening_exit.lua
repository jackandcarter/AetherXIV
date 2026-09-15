require ("global")

function onSpawn(player, npc)	
	npc:SetQuestGraphic(player, 0x3);	
end

function onEventStarted(player, npc)
	-- Quest replacement owns the initial Man0g1 transition. Its onStart hook
	-- plays processEvent100, closes this event, and performs the single warp
	-- into the Canopy copy.
	player:ReplaceQuest(110005, 110006);
end
