require("global")
properties = {
    permissions = 1,
    parameters = "ssss",
    description = "!weather <id|clear|fair|cloudy|fog|wind|rain|dust|sandstorm> [transition=15] [zonewide=0]; !weather auto resets zone weather and end-days FX."
}
local names = {clear=8001, fair=8002, cloudy=8003, fog=8004, wind=8005, rain=8007, dust=8011, sandstorm=8012}
function onTrigger(player, argc, weather, transition, zonewide)
    if not player or not player.isGM then return end
    if weather == "auto" then
        player:GetZone():ResetWeather(player);
        player:SendMessage(MESSAGE_TYPE_SYSTEM, "[weather]", "Normal regional weather resumed; event FX cleared.");
        return;
    end
    local id = names[weather] or tonumber(weather);
    local time = tonumber(transition or "15");
    local scope = tonumber(zonewide or "0");
    if not id or id % 1 ~= 0 or id < 8001 or id > 8017 or id == 8014 or
       not time or time % 1 ~= 0 or time < 0 or time > 65535 or
       (scope ~= 0 and scope ~= 1) then
        player:SendMessage(MESSAGE_TYPE_SYSTEM_ERROR, "[weather]", properties.description .. " Event weather requires !zonefx.");
        return;
    end
    player:GetZone():ChangeWeather(id, time, player, scope == 1);
    player:SendMessage(MESSAGE_TYPE_SYSTEM, "[weather]", scope == 1 and "Zone override set; use !weather auto to resume." or "Personal preview set; clears on zone entry or the next weather change.");
end
