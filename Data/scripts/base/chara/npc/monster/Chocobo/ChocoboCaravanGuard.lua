require ("global")

-- Friendly monster-class actors still require the complete battle-common
-- initialization block. The client's ChocoboCaravanGuard overrides battalion
-- to 1; do not substitute a populace (false,false,0,0) initialization tuple
-- when enabling its combat depiction property.
function init(npc)
    return true, true, 10, 0, 1, true, false, false, false, false, false, false, false, 0;
end
