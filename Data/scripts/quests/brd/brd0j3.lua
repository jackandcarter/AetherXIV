require("global")
require("quest")

-- Brd0j3: client completion delegates name Phaia and award the job action.
-- journalxtxFst confirms defeat as the objective, with no return-to-Jehantel step.
local TARGET = 2101509

function onStart(player, quest)
    quest:StartSequence(0)
end

function onFinish(player, quest)
end

function onStateChange(player, quest, sequence)
    if sequence == 0 then
        quest:SetENpc(1060039)
        quest:SetENpc(1002024)
        quest:SetENpc(TARGET)
    end
end

function onTalk(player, quest, npc)
    local id = npc:GetActorClassId()
    if quest:GetSequence() == 0 and (id == 1060039 or id == 1002024) then
        callClientFunction(player, "delegateEvent", player, quest, "processEvent000", npc)
    end
    player:EndEvent()
end

function onKillBNpc(player, quest, actorClassId)
    if quest:GetSequence() ~= 0 or actorClassId ~= TARGET then return end
    if player:GetCurrentClassOrJob() ~= 18 then return end
    if not player:HasQuest(quest:GetQuestId()) then return end
    -- CompleteQuest is the existing completion ledger + restored action-grant path.
    -- Do not issue client event delegates from an unowned combat callback.
    player:CompleteQuest(quest)
end
