require("global")
properties = {
    permissions = 1,
    parameters = "ss",
    description = "!zonefx <eventWeatherId> [dalamudLevel=0]; !zonefx off. End-days weather IDs: 8030 (Dalamud), 8031 (Aurora), 8032 (Dalamud thunder). Zone-only override, cleared on server restart."
}
function onTrigger(player, argc, weather, stage)
    if not player or not player.isGM then return end
    if weather == "off" then
        player:GetZone():ResetWeather(player);
        player:SendMessage(MESSAGE_TYPE_SYSTEM, "[zonefx]", "Event FX cleared; normal weather resumed.");
        return;
    end
    local id = tonumber(weather);
    local level = tonumber(stage or "0");
    local valid = id and (id == 8014 or (id >= 8027 and id <= 8032) or id == 8065 or id == 8066);
    if not valid or id % 1 ~= 0 or not level or level % 1 ~= 0 or level < -1 or level > 127 then
        player:SendMessage(MESSAGE_TYPE_SYSTEM_ERROR, "[zonefx]", properties.description);
        return;
    end
    player:GetZone():SetEventWeather(id, level, player);
    player:SendMessage(MESSAGE_TYPE_SYSTEM, "[zonefx]", "Event override set for this zone. Use !zonefx off to clear.");
end
