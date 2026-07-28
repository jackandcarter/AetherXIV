require ("global")

function onSpawn(player, npc)
	local man0g1Quest = player:GetQuest("Man0g1");
	if (man0g1Quest ~= nil and man0g1Quest:GetSequence() == 0) then
		npc:SetQuestGraphic(player, 0x2);
	end
end

function onEventStarted(player, npc, triggerName)
	local man0g1Quest = player:GetQuest("Man0g1");
	local pos = player:GetPos();
	
	if (man0g1Quest ~= nil and man0g1Quest:GetSequence() == 0) then
		-- processEvent110 is Miounne's man0g110 Bentbranch briefing. The
		-- shipped client also uses it to clear/reorder desktop mode 16 and to
		-- prepare the fade-in after the following same-area public reload.
		callClientFunction(player, "delegateEvent", player, man0g1Quest, "processEvent110");
		man0g1Quest:NewNpcLsMsg(1);
		man0g1Quest:StartSequence(5);
		player:EndEvent();

		local director = GetWorldManager():GetZone(155):CreateDirector("AfterQuestWarpDirector", false);
		director:StartDirector(true);
		player:AddDirector(director);
		player:SetLoginDirector(director);
		player:DeferContentKickEvent(director, "noticeEvent", true);
		man0g1Quest:UpdateENPCs();
		GetWorldManager():DoZoneChange(player, 155, nil, 0, 15, pos[0], pos[1], pos[2], pos[3]);
		return;
	end
	
	player:EndEvent();
	
end
