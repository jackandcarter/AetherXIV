require("global")

function init(npc)
	return false, false, 0, 0;
end

function onEventStarted(player, npc, eventName)
	local npcId = npc:GetActorClassId();
	if (npcId == 1002090) then
		callClientFunction(player, "delegateEvent", player,
			GetStaticActor("DftFst"), "defaultTalkWithStewart_001");
	elseif (npcId == 1002091) then
		callClientFunction(player, "delegateEvent", player,
			GetStaticActor("DftFst"), "defaultTalkWithTrisselle_001");
	elseif (npcId == 1060022) then
		callClientFunction(player, "delegateEvent", player,
			GetStaticActor("DftFst"), "defaultTalkLouisoix_001");
	end
	player:EndEvent();
end
