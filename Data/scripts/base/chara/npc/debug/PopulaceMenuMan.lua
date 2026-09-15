require("global")

function init(npc)
	return false, false, 0, 0;
end

function onEventStarted(player, npc, eventName)
	callClientFunction(player, "debugMenuEvent", player);
	player:EndEvent();
end
