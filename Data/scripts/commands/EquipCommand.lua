-- Retail EquipCommand carries a one-based equipment point and an inventory
-- reference. Player owns validation, the database commit and publication.
function onEventStarted(player, actor, triggerName, equippedItem, param1, param2, param3, param4, param5, param6, param7, equipPoint, itemDBIds)
    if (player:IsValidEquipmentPoint(equipPoint) == false) then
        player:EndEvent();
        return;
    end
    local item = nil;
    if (equippedItem ~= nil) then
        item = player:GetValidatedEquipmentItem(equippedItem, equipPoint, itemDBIds);
        if (item == nil) then
            player:EndEvent();
            return;
        end
    end
    local oldItem = player:GetEquipment():GetItemAtSlot(equipPoint - 1);
    local oldClass = player:GetClass();
    if (player:TryChangeEquipment(item, equipPoint)) then
        local worldMaster = GetWorldMaster();
        if (player:GetClass() ~= oldClass) then
            player:SendGameMessage(player, worldMaster, 30103, 0x20, 0, 0, player, player:GetClass());
        end
        if (item ~= nil) then
            player:SendGameMessage(player, worldMaster, 30601, 0x20, equipPoint, item.itemId, item.quality, 0, 0, 1);
        elseif (oldItem ~= nil) then
            player:SendGameMessage(player, worldMaster, 30602, 0x20, equipPoint, oldItem.itemId, oldItem.quality, 0, 0, 1);
        end
        player:CompleteEquipmentCommand();
    else
        if (item == nil and oldItem ~= nil and (equipPoint == 1 or equipPoint == 10 or equipPoint == 12)) then
            player:SendGameMessage(player, GetWorldMaster(), 30730, 0x20, equipPoint, oldItem.itemId, oldItem.quality, 0, 0, 1);
        end
        player:EndEvent();
    end
end
