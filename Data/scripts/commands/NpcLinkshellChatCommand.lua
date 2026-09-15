require ("global")

function onEventStarted(player, command, eventName, npcLsId)
	-- The clicked linkshell id is a best-effort hint and the client's
	-- commandRequest param tail is unreliable (sometimes an empty tail, so
	-- npcLsId is nil). Coerce nil to 0 and let HandleNpcLs resolve the
	-- zero/one-based hint or fall back to the sole pending pearl.
	if (player:HandleNpcLs(npcLsId or 0) == false) then
		player:EndEvent();
	end
end
