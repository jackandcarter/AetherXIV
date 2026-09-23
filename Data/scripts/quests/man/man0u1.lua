require("global")
require("quest")

MAN0U1_SEQ_INTRO = 0;
MAN0U1_SEQ_CAMP = 5;
MAN0U1_SEQ_RETURN = 10;
MAN0U1_SEQ_GUILD_SCHOOLING = 12;
MAN0U1_SEQ_GUILD_TASKS = 15;

MAN0U1_FLAG_CAMP_ATTUNED = 0;

MAN0U1_MOMODI = 1000841;
MAN0U1_MOMODI_DISPLAY_ID = 1500014;
MAN0U1_MARKER_MOMODI = 11001001;
MAN0U1_MARKER_CAMP_BLACK_BRUSH = 11001002;
MAN0U1_ITEM_COLISEUM_PASS = 11000126;

-- These are the two Momodi rows historically emitted by the Ul'dah
-- opening-exit script. They belong to the Adventurers' Guild linkpearl
-- response, so publish them when the player selects that NPC-linkshell
-- message rather than before the public-zone reload.
MAN0U1_INITIAL_NPCLS_ROWS = { 329, 330 };

-- Shipped Man0u1.processEvent013 calls tellByNpcLinkshellChat with these
-- Momodi rows. That client routine yields after each line; a server
-- callClientFunction resumes on its first EventUpdate and tears the pearl
-- down before the conversation can complete. Publish the recovered rows on
-- the authoritative NPC-linkshell channel instead.
MAN0U1_POST_ATTUNEMENT_NPCLS_ROWS = { 242, 243, 244, 245, 246 };

function isObjectivesComplete(player, quest)
	return false;
end

function onStateChange(player, quest, sequence)
	if (sequence == MAN0U1_SEQ_INTRO) then
		quest:SetENpc(MAN0U1_MOMODI, QFLAG_TALK);
	elseif (sequence == MAN0U1_SEQ_RETURN) then
		quest:SetENpc(MAN0U1_MOMODI, QFLAG_TALK);
	end
end

function onTalk(player, quest, npc)
	local sequence = quest:GetSequence();
	if (npc:GetActorClassId() ~= MAN0U1_MOMODI) then
		player:EndEvent();
		return;
	end

	if (sequence == MAN0U1_SEQ_INTRO) then
		local pos = player:GetPos();
		callClientFunction(player, "delegateEvent", player, quest, "processEvent010");
		quest:NewNpcLsMsg(1);
		quest:StartSequence(MAN0U1_SEQ_CAMP);
		player:EndEvent();
		GetWorldManager():DoZoneChange(player, 175, nil, 0, 15, pos[0], pos[1], pos[2], pos[3]);
		return;
	elseif (sequence == MAN0U1_SEQ_CAMP) then
		callClientFunction(player, "delegateEvent", player, quest, "processEvent010_2");
	elseif (sequence == MAN0U1_SEQ_RETURN) then
		callClientFunction(player, "delegateEvent", player, quest, "processEvent015");
		quest:StartSequence(MAN0U1_SEQ_GUILD_SCHOOLING);
	elseif (sequence == MAN0U1_SEQ_GUILD_SCHOOLING) then
		callClientFunction(player, "delegateEvent", player, quest, "processEvent017");
		player:SendGameMessage(GetWorldMaster(), 25117, MESSAGE_TYPE_GENERAL_INFO, MAN0U1_ITEM_COLISEUM_PASS);
		quest:StartSequence(MAN0U1_SEQ_GUILD_TASKS);
	end

	player:EndEvent();
end

function onNpcLS(player, quest, from, msgStep)
	if (from ~= 1 or quest:GetSequence() ~= MAN0U1_SEQ_CAMP) then
		player:EndEvent();
		return;
	end

	if (quest:GetData():GetFlag(MAN0U1_FLAG_CAMP_ATTUNED)) then
		for _, textId in ipairs(MAN0U1_POST_ATTUNEMENT_NPCLS_ROWS) do
			player:SendGameMessageLocalizedDisplayName(
				quest, textId, MESSAGE_TYPE_NPC_LINKSHELL, MAN0U1_MOMODI_DISPLAY_ID);
		end
		quest:EndOfNpcLsMsgs();
		quest:StartSequenceForNpcLs(MAN0U1_SEQ_RETURN);
	else
		-- The pearl is granted during Momodi's briefing and can be tried before
		-- leaving. The recovered Momodi rows are part of this click response;
		-- keep the client event for its progression behavior as well.
		for _, textId in ipairs(MAN0U1_INITIAL_NPCLS_ROWS) do
			player:SendGameMessageLocalizedDisplayName(
				quest, textId, MESSAGE_TYPE_NPC_LINKSHELL, MAN0U1_MOMODI_DISPLAY_ID);
		end
		callClientFunction(player, "delegateEvent", player, quest, "processEvent010_2");
		quest:EndOfNpcLsMsgs();
	end

	player:EndEvent();
end

function getJournalMapMarkerList(player, quest)
	local sequence = quest:GetSequence();
	local markers = {};

	if (sequence == MAN0U1_SEQ_INTRO or sequence == MAN0U1_SEQ_RETURN) then
		table.insert(markers, MAN0U1_MARKER_MOMODI);
	elseif (sequence == MAN0U1_SEQ_CAMP) then
		table.insert(markers, MAN0U1_MARKER_CAMP_BLACK_BRUSH);
	end

	return unpack(markers);
end
