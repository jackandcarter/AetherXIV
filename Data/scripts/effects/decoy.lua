require("modifiers")
require("battleutils")

--This is the untraited version of decoy.
function onPreAction(effect, caster, target, skill, action, actionContainer)
    --Evade single ranged or magic attack
    --Traited allows for physical attacks
    local magic = action.actionType == ActionType.Magic;
    local ranged = skill ~= nil and skill.isRanged;
    if target.allegiance != caster.allegiance and (magic or (action.actionType == ActionType.Physical and ranged)) then
        action.hitRate = 0.0;
        action.forceFullResist = magic;
        --Remove status and add message 
        target.statusEffects.RemoveStatusEffect(effect, actionContainer, 30331, false);
    end

end;
