require("global")
require("quests/man/man200")

local snpc;

function init()
	return "/Director/Quest/QuestDirectorEventMan20001";
end

function onCreateContentArea(players, director, contentArea, contentGroup)
	snpc = contentArea:SpawnActor(players[1]:GetSNpcSkin() + 107000,
		"snpc", -203.32, 0, -159.627);

	-- The Meteor source names an undefined singular `player` here. Its clear
	-- intent and every sibling content director use the supplied player list.
	for _, player in pairs(players) do
		contentGroup:AddMember(player);
	end
	contentGroup:AddMember(snpc);
	contentGroup:AddMember(director);
end

function onEventStarted(player, director, triggerName)
end

function main()
end
