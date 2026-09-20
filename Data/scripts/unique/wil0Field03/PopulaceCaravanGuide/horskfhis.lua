require("global")

-- Horizon's caravan heads to the Silver Bazaar (area 3, lower route).
-- Show the existing client referral dialogue; escort participation/rewards
-- require a separate content implementation.
function onEventStarted(player, npc, triggerName)
    callClientFunction(player, "caravanGuardOffer", 3, 0, player.gcCurrent);
    player:EndEvent();
end
