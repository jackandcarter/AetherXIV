require("global")
require("quest")

-- 111301: A Song of Bards and Bowmen. Existing Quest sequence/ENPC contract.
-- Evidence: client Brd0j1 delegates and journalxtxFst 433-438.
-- World placement and the Brd0j101 instance remain separate restoration work.
local GEORJEAUX = 1000830
local JEHANTEL = 1060039
local JEHANTEL_FIELD = 1002024
local PUKNO_POKI = 1001936
local QIQIRN_SHIRRER = 2206306
local SOUL_OF_THE_BARD = 2000205
local KEEPERS_HYMN = 3020410

local function isJehantel(id)
    return id == JEHANTEL or id == JEHANTEL_FIELD
end

function onStart(player, quest)
    quest:StartSequence(0)
end

function onFinish(player, quest)
end

function onStateChange(player, quest, sequence)
    quest:SetENpc(GEORJEAUX)
    if sequence == 0 or sequence == 15 then
        local flag = sequence == 15 and QFLAG_REWARD or QFLAG_TALK
        quest:SetENpc(JEHANTEL, flag)
        quest:SetENpc(JEHANTEL_FIELD, flag)
    elseif sequence == 5 then
        quest:SetENpc(JEHANTEL)
        quest:SetENpc(JEHANTEL_FIELD)
        quest:SetENpc(PUKNO_POKI, QFLAG_TALK)
    elseif sequence == 10 then
        quest:SetENpc(PUKNO_POKI)
        quest:SetENpc(QIQIRN_SHIRRER)
    end
end

function onTalk(player, quest, npc)
    local id, sequence = npc:GetActorClassId(), quest:GetSequence()
    if id == GEORJEAUX then
        callClientFunction(player, "delegateEvent", player, quest, "processEvent000_GEORJEAUX", npc)
    elseif isJehantel(id) and sequence == 0 then
        callClientFunction(player, "delegateEvent", player, quest, "processEvent000", npc)
        quest:StartSequence(5)
    elseif isJehantel(id) and sequence == 5 then
        callClientFunction(player, "delegateEvent", player, quest, "processEvent005_JEHANTEL", npc)
    elseif id == PUKNO_POKI and sequence == 5 then
        callClientFunction(player, "delegateEvent", player, quest, "processEvent005", npc)
        quest:StartSequence(10)
    elseif id == PUKNO_POKI and sequence == 10 then
        callClientFunction(player, "delegateEvent", player, quest, "processEvent010_PUKNOPOKI", npc)
    elseif isJehantel(id) and sequence == 15 then
        if player:GetCurrentClassOrJob() ~= 7 or player:GetClassLevel(7) < 30 or player:GetClassLevel(23) < 15 then
            player:EndEvent()
            return
        end
        -- Reward IDs/quantities are corroborated by quest_new_reward and client presentation.
        -- Keep retries safe if an inventory grant fails; never complete on a failed grant.
        local data = quest:GetData()
        if not data:GetFlag(8) then
            if not player:HasItem(SOUL_OF_THE_BARD) then
                if player:GetItemPackage(INVENTORY_KEYITEMS):AddItem(SOUL_OF_THE_BARD, 1) ~= 0 then
                    player:EndEvent()
                    return
                end
            end
            data:SetFlag(8)
        end
        if not data:GetFlag(9) then
            if player:GetItemPackage(INVENTORY_NORMAL):AddItem(KEEPERS_HYMN, 1) ~= 0 then
                player:EndEvent()
                return
            end
            data:SetFlag(9)
        end
        callClientFunction(player, "delegateEvent", player, quest, "processEvent015", npc)
        -- No guessed EXP scaling or unverified job-item widget argument is sent.
        player:CompleteQuest(quest)
    end
    quest:UpdateENPCs()
    player:EndEvent()
end

function onKillBNpc(player, quest, actorClassId)
    if quest:GetSequence() == 10 and actorClassId == QIQIRN_SHIRRER then
        quest:StartSequence(15)
    end
end
