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

		-- This no-widget handoff follows the working Ul'dah/Limsa contract:
		-- publish the NPC-linkshell state, close the private event, and reload
		-- the public zone. Do not create a deferred notice director here; its
		-- type-5 kick is not a linkpearl click and leaves the client without
		-- the normal icon/menu transaction.
		man0g1Quest:UpdateENPCs();
		GetWorldManager():DoZoneChange(player, 155, nil, 0, 15,
			pos[0], pos[1], pos[2], pos[3]);
		return;
	end

	player:EndEvent();
end
