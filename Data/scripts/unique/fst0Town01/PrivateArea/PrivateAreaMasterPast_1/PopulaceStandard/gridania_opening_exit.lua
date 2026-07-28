require ("global")
require ("quests/man/man0g0")

function onSpawn(player, npc)	
	npc:SetQuestGraphic(player, 0x3);	
end

function onEventStarted(player, npc)
	local man0g1Quest = GetStaticActor("Man0g1");
	callClientFunction(player, "delegateEvent", player, man0g1Quest, "processEvent100");
	player:ReplaceQuest(110005, 110006);
	player:SendGameMessage(man0g1Quest, 353, 0x20);
	player:SendGameMessage(man0g1Quest, 354, 0x20);

	-- Close the source event before the inline same-map reload. A trailing
	-- EndEvent races the replacement actor table and can address the deleted
	-- PrivateAreaMasterPast/1 owner after the destination bundle has begun.
	player:EndEvent();

	-- Rows 353/354 are the instance explanation associated with the opening
	-- exit. Land at the Roost with Man0g1 still at sequence 0 so the optional
	-- adventurer conversations and Miounne's processEvent110 remain in order.
	GetWorldManager():DoZoneChange(player, 155, "PrivateAreaMasterPast", 2, 15, 67.034, 4, -1205.6497, -1.074);
end
