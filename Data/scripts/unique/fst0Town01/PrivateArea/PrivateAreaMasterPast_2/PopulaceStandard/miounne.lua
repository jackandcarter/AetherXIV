require ("global")

function onSpawn(player, npc)
	npc:SetQuestGraphic(player, 0x2);
end

function onEventStarted(player, npc, triggerName)
	local man0g1Quest = player:GetQuest("Man0g1");
	local pos = player:GetPos();

	if (man0g1Quest ~= nil and man0g1Quest:GetSequence() == 0) then
		-- The first Canopy briefing is owned by the private-area Miounne
		-- handler, matching Legacy Meteor and Garlemald. The quest is advanced
		-- and the Linkpearl is armed before the same-zone reload; the director
		-- notice then closes its event; the Guild message is read through the menu.
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
		GetWorldManager():DoZoneChange(player, 155, nil, 0, 15,
			pos[0], pos[1], pos[2], pos[3]);
		return;
	end

	player:EndEvent();
end
