require("modifiers")
require("battleutils")

--This is the traited version of Decoy. It can also evade physical attacks.
function onPreAction(effect, caster, target, skill, action, actionContainer)
    --Evade single ranged or magic attack
    --Traited allows for physical attacks
    local magic = action.actionType == ActionType.Magic;
    if target.allegiance != caster.allegiance and (magic or action.actionType == ActionType.Physical) then
        --Set action's hit rate to 0
        action.hitRate = 0.0;
        action.forceFullResist = magic;
        --Remove status and add message
        target.statusEffects.RemoveStatusEffect(effect, actionContainer, 30331, false);
    end

end;
