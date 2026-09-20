require ("global")

-- Same MonsterBaseClass initialization contract as the existing ambient
-- monster handlers. Family-specific abilities remain server combat data.
function init(npc)
    return true, true, 10, 0, 1, true, false, false, false, false, false, false, false, 0;
end
