require ("global")
require ("quest")
require ("tutorial")

--[[

Quest Script

Name: 	Treasures of the Main
Code: 	Man0l1
Id: 	110002
Prereq: Shapeless Melody (Man0l0 - 110001)

]]

-- Quest flags (bit indices into questFlags)

-- Sequence Numbers
SEQ_000	= 0;  	-- (Private Area) Drowning Wench Echo Scene.
SEQ_003	= 3;  	-- Go attune to Camp Bearded Rock.
SEQ_005	= 5;  	-- Attuned, go back to Baderon. Info: <param1> If 1, Baderon gave you a tutorial guildleve else 0.
SEQ_006	= 6;  	-- Talk to Baderon again
SEQ_007	= 7;  	-- Find the CUL and MSK Guilds. Info: Params '0,5,20' will show the msg that you visited both guilds and to notify Baderon on the LS.
SEQ_035	= 35;	-- Go to the FSH Guild.
SEQ_040	= 40;	-- Learn hand signals from the guild
SEQ_048	= 48;	-- Travel to Zephyr Gate
SEQ_050	= 50;	-- Escort mission
SEQ_055	= 55;	-- Search lighthouse for corpse
SEQ_060	= 60;	-- Talk to Sisipu
SEQ_065	= 65;	-- Return to FSH Guild
SEQ_070	= 70;	-- Contact Baderon on LS
SEQ_075	= 75;	-- Go to the ARM and BSM Guilds. Talk to Bodenolf.
SEQ_080	= 80;	-- Speak with H'naanza
SEQ_085	= 85;	-- Walk into push trigger
SEQ_090	= 90;	-- Contact Baderon on LS
SEQ_092	= 92;	-- Return to Baderon.

-- Actor Class Ids
-- Echo in Adv Guild
YSHTOLA 				= 1000001;
CRAPULOUS_ADVENTURER 	= 1000075;
DUPLICITOUS_TRADER	 	= 1000076;
DEBONAIR_PIRATE 		= 1000077;
ONYXHAIRED_ADVENTURER	= 1000098;
SKITTISH_ADVENTURER		= 1000099;
RELAXING_ADVENTURER 	= 1000100;
BADERON 				= 1000137;
MYTESYN 				= 1000167;
COCKAHOOP_COCKSWAIN 	= 1001643;
SENTENIOUS_SELLSWORD 	= 1001649;
SOLICITOUS_SELLSWORD 	= 1001650;

-- Sequence 003
BEARDEDROCK_AETHERYTE	= 1280002;

-- Sequence 007
CHARLYS					= 1000138;
ISANDOREL				= 1000152;
MERLZIRN				= 1000472;
MSK_TRIGGER				= 1090001;

-- Echo in MSK Guild
NERVOUS_BARRACUDA		= 1000096;
INTIMIDATING_BARRACUDA	= 1000097;
OVEREAGER_BARRACUDA		= 1000107;
SOPHISTICATED_BARRACUDA	= 1000108;
SMIRKING_BARRACUDA		= 1000109;
MANNSKOEN				= 1000142;
TOTORUTO				= 1000161;
ADVENTURER1				= 1000869;
ADVENTURER2				= 1000870;
ADVENTURER3				= 1000871;
ECHO_EXIT_TRIGGER		= 1090003;

-- Fsh Guild
NNMULIKA				= 1000153;
SISIPU_EMOTE			= 1000155;
ZEPHYR_TRIGGER			= 1090004;

-- Sequence 055, 060, 065
SISIPU					= 1000156;
WINDWORN_CORPSE			= 1000091;
GLASSYEYED_CORPSE		= 1000092;
FEARSTRICKEN_CORPSE		= 1000378;
FSH_TRIGGER				= 1090006;

-- Echo in the Bsm Guild
TATTOOED_PIRATE			= 1000111;
IOFA					= 1000135;
BODENOLF				= 1000144;
HNAANZA					= 1000145;
MIMIDOA					= 1000176;
JOELLAUT				= 1000163;
WERNER					= 1000247;
HIHINE					= 1000267;
TRINNE					= 1000268;
ECHO_EXIT_TRIGGER2		= 1090007;

-- Quest Markers

-- Quest Data
CNTR_SEQ7_CUL		= 1;
CNTR_SEQ7_MSK		= 2;
CNTR_SEQ40_FSH		= 3;
CNTR_LS_MSG			= 4;

-- Msg packs for the Npc LS
NPCLS_MSGS = {
	{339},
	{80, 81, 82},
	{131, 326, 132},
	{161, 162, 163, 164}
};

-- Escort-duty data flag (SEQ_050 one-shot rescue latch): set by
-- startMan0l1Content BEFORE StartSequence(SEQ_050) so the onStateChange
-- run it fires consumes the flag and stays dormant; any other SEQ_050
-- entry (relog with dead content, a wedged save) sees it CLEAR and rolls
-- back to SEQ_048 for retry. (Garlemald-Server #46.)
FLAG_ESCORT_HANDOFF		= 1;

-- Escort-duty duty banners / announcements (worldMaster sheet ids and
-- Sisipu say rows decoded from the client text sheets by
-- Garlemald-Server #46; consumed by content/SimpleContentMan0l101.lua
-- and QuestDirectorMan0l101.lua).
TEXT_PROTECT_SISIPU		= 51005;	-- "Protect <displayName>'s group~ from harm." (param: Sisipu's actor id)
TEXT_BOUND_BY_DUTY		= 50011;	-- "You are now bound by duty."
TEXT_UNBOUND_FROM_DUTY	= 50012;	-- "You are no longer bound by duty."
TEXT_TIME_REMAINING		= 25018;	-- "There ~is/are~ <N> ~minute/minutes~ remaining." (param: minutes)
TEXT_MOB_ENGAGED		= 30120;	-- "<displayName> is engaged."
TEXT_MOB_DEFEATED		= 30121;	-- "<displayName> is defeated."
SAY_ESCORT_GUIDANCE		= 283;		-- "Oschon's Torch is due south..." (man0l1 QUEST-sheet row 283)

function onStart(player, quest)	
	quest:StartSequence(SEQ_000);
	callClientFunction(player, "delegateEvent", player, quest, "processEvent010");
	player:SendGameMessage(quest, 320, 0x20);
	player:SendGameMessage(quest, 321, 0x20);
	player:EndEvent();
	GetWorldManager():DoZoneChange(player, 133, "PrivateAreaMasterPast", 2, 15,
		-459.619873, 40.0005722, 196.370377, 2.010813);
end

function onFinish(player, quest)
end

-- Echo classes also exist in public Limsa. Arm them only in their instance.
local function inLimsaEcho(player, kind)
 local area = player:GetZone();
 return area ~= nil and area:GetTerritoryId() == 230 and area:IsPrivate()
  and area:GetPrivateAreaName() == "PrivateAreaMasterPast"
  and area:GetPrivateAreaType() == kind;
end

function onStateChange(player, quest, sequence)
	local data = quest:GetData();

	if (sequence == SEQ_000) then
		quest:SetENpc(YSHTOLA);
		quest:SetENpc(CRAPULOUS_ADVENTURER);
		quest:SetENpc(DUPLICITOUS_TRADER);
		quest:SetENpc(DEBONAIR_PIRATE);
		quest:SetENpc(ONYXHAIRED_ADVENTURER);
		quest:SetENpc(SKITTISH_ADVENTURER);
		quest:SetENpc(RELAXING_ADVENTURER);
		quest:SetENpc(BADERON, QFLAG_TALK);
		quest:SetENpc(MYTESYN);
		quest:SetENpc(COCKAHOOP_COCKSWAIN);
		quest:SetENpc(SENTENIOUS_SELLSWORD);
		quest:SetENpc(SOLICITOUS_SELLSWORD);
	elseif (sequence == SEQ_003) then
		quest:SetENpc(BADERON);
	elseif (sequence == SEQ_005) then
		quest:SetENpc(BADERON, QFLAG_TALK);
	elseif (sequence == SEQ_006) then
		quest:SetENpc(BADERON, QFLAG_TALK);
	elseif (sequence == SEQ_007) then
		local subseqCUL = data:GetCounter(CNTR_SEQ7_CUL);
		local subseqMSK = data:GetCounter(CNTR_SEQ7_MSK);
		-- The Musketeers' echo shares its actor classes with static public
		-- Limsa spawns.  Do not arm those echo interactions until the player
		-- is actually inside the recovered type-3 private area; otherwise a
		-- stale/recovered save can run the echo-exit cinematic against the
		-- public trigger and leave the client waiting for a reload that the
		-- server never issued.
		local area = player:GetZone();
		local inMusketeersEcho = area ~= nil
			and area:GetTerritoryId() == 230
			and area:IsPrivate()
			and area:GetPrivateAreaName() == "PrivateAreaMasterPast"
			and area:GetPrivateAreaType() == 3;
		-- Always active in this seqence
		quest:SetENpc(BADERON);
		quest:SetENpc(CHARLYS, subseqCUL == 0 and QFLAG_TALK or QFLAG_NONE);
		-- Down and Up the MSK guild
		quest:SetENpc(ISANDOREL, (subseqMSK == 0 or subseqMSK == 2) and QFLAG_TALK or QFLAG_NONE);
		if (subseqMSK == 1) then
			quest:SetENpc(MSK_TRIGGER, QFLAG_PUSH, false, true);
		elseif (subseqMSK == 2) then
			quest:SetENpc(MERLZIRN);
		end
		-- In Echo: these class ids must resolve only in the type-3 private
		-- scene, never against their public-area counterparts.
		if (inMusketeersEcho) then
			quest:SetENpc(NERVOUS_BARRACUDA);
			quest:SetENpc(INTIMIDATING_BARRACUDA);
			quest:SetENpc(OVEREAGER_BARRACUDA);
			quest:SetENpc(SOPHISTICATED_BARRACUDA);
			quest:SetENpc(SMIRKING_BARRACUDA);
			quest:SetENpc(MANNSKOEN);
			quest:SetENpc(TOTORUTO);
			quest:SetENpc(ADVENTURER1);
			quest:SetENpc(ADVENTURER2);
			quest:SetENpc(ADVENTURER3);
			quest:SetENpc(ECHO_EXIT_TRIGGER, subseqMSK == 3 and QFLAG_PUSH or QFLAG_NONE, false, subseqMSK == 3);
		end
	elseif (sequence == SEQ_035) then
		quest:SetENpc(NNMULIKA, QFLAG_TALK);
	elseif (sequence == SEQ_040) then
		if (inLimsaEcho(player, 5)) then
            quest:SetENpc(SISIPU_EMOTE, QFLAG_TALK, true, false, true);
            quest:SetENpc(NNMULIKA);
        else
            quest:SetENpc(NNMULIKA, QFLAG_TALK);
        end
	elseif (sequence == SEQ_048) then
		quest:SetENpc(BADERON);
		quest:SetENpc(ZEPHYR_TRIGGER, QFLAG_PUSH, false, true);
		quest:SetENpc(NNMULIKA);
	elseif (sequence == SEQ_050) then
		-- Escort-duty rescue (Garlemald-Server #46). onStateChange runs
		-- at SEQ_050 in exactly two situations:
		--   1. The legit escort start: startMan0l1Content sets
		--      FLAG_ESCORT_HANDOFF then calls StartSequence(SEQ_050) —
		--      the commands apply in order, so THIS run sees the flag
		--      SET, consumes it, and stays dormant.
		--   2. Everything else — the processor's relog re-arm for a
		--      player whose escort content died with the session
		--      (content instances don't persist), or a save wedged at
		--      SEQ_050 by an older build (the flag was never set).
		--      The flag is CLEAR → roll back to SEQ_048 so the
		--      ZEPHYR_TRIGGER re-arms and the duty can be retried from
		--      the gate.
		-- One-shot-safe by construction: the rescue moves the sequence
		-- to SEQ_048, so this arm no longer matches.
		local data = quest:GetData();
		if (data:GetFlag(FLAG_ESCORT_HANDOFF)) then
			data:ClearFlag(FLAG_ESCORT_HANDOFF);
		else
			quest:StartSequence(SEQ_048);
		end
	elseif (sequence == SEQ_055) then
		quest:SetENpc(WINDWORN_CORPSE, QFLAG_TALK);
		quest:SetENpc(GLASSYEYED_CORPSE);
		quest:SetENpc(FEARSTRICKEN_CORPSE);
		quest:SetENpc(SISIPU);
	elseif (sequence == SEQ_060) then
		quest:SetENpc(SISIPU, QFLAG_TALK);
		quest:SetENpc(WINDWORN_CORPSE);
		quest:SetENpc(GLASSYEYED_CORPSE);
		quest:SetENpc(FEARSTRICKEN_CORPSE);
	elseif (sequence == SEQ_065) then
		quest:SetENpc(FSH_TRIGGER, QFLAG_PUSH, false, true);
	elseif (sequence == SEQ_075) then	
		quest:SetENpc(BODENOLF, QFLAG_TALK);
	elseif (sequence == SEQ_080) then	
		if (not inLimsaEcho(player, 4)) then
			quest:SetENpc(BODENOLF, QFLAG_TALK);
		else
		quest:SetENpc(HNAANZA, QFLAG_TALK);
		quest:SetENpc(TATTOOED_PIRATE);
		quest:SetENpc(IOFA);
		quest:SetENpc(BODENOLF);
		quest:SetENpc(MIMIDOA);
		quest:SetENpc(JOELLAUT);
		quest:SetENpc(WERNER);
		quest:SetENpc(HIHINE);
		quest:SetENpc(TRINNE);
		end
	elseif (sequence == SEQ_085) then	
		if (not inLimsaEcho(player, 4)) then
			quest:SetENpc(BODENOLF, QFLAG_TALK);
		else
		quest:SetENpc(HNAANZA);
		quest:SetENpc(TATTOOED_PIRATE);
		quest:SetENpc(WERNER);
		quest:SetENpc(HIHINE);
		quest:SetENpc(TRINNE);
		quest:SetENpc(ECHO_EXIT_TRIGGER2, QFLAG_PUSH, false, true);
		end
	elseif (sequence == SEQ_092) then	
		quest:SetENpc(BADERON, QFLAG_REWARD);
	end	
	
end

function onTalk(player, quest, npc)
	local sequence = quest:GetSequence();
	local classId = npc:GetActorClassId();

    if ((sequence == SEQ_080 or sequence == SEQ_085) and classId == BODENOLF
        and not inLimsaEcho(player, 4)) then
        callClientFunction(player, "delegateEvent", player, quest, "processEvent630");
        player:EndEvent();
        GetWorldManager():WarpToPrivateArea(player, "PrivateAreaMasterPast", 4, -504.985, 42.490, 433.712, 2.35);
        return;
    end
	
	if (sequence == SEQ_000) then
		seq000_onTalk(player, quest, npc, classId);
	elseif (sequence == SEQ_003) then
		if (classId == BADERON) then
			callClientFunction(player, "delegateEvent", player, quest, "processEvent020_2");
			player:EndEvent();
			-- Leaving the opening inn is a scripted PA -> public reload. Seamless
			-- boundary detection intentionally does not run inside PrivateArea;
			-- use the same player-position handoff as the legacy Baderon handler.
			GetWorldManager():DoZoneChange(player, 133, nil, 0, 15,
				player.positionX, player.positionY, player.positionZ, player.rotation);
			return;
		end
	elseif (sequence == SEQ_005) then
		if (classId == BADERON) then
			callClientFunction(player, "delegateEvent", player, quest, "processEvent026");
			player:EndEvent();
			quest:StartSequence(SEQ_006);
		end
	elseif (sequence == SEQ_006) then
		if (classId == BADERON) then
			callClientFunction(player, "delegateEvent", player, quest, "processEvent027");			
			player:EndEvent();
			player:SendGameMessage(GetWorldMaster(), 25117, 0x20, 11000125); -- You obtain Baderon's Recommendation
			quest:StartSequence(SEQ_007);
		end
	elseif (sequence == SEQ_007) then
		seq007_onTalk(player, quest, npc, classId);
	elseif (sequence == SEQ_035) then
		if (classId == NNMULIKA) then
			callClientFunction(player, "delegateEvent", player, quest, "processEvent600");
			quest:EndOfNpcLsMsgs();
			quest:StartSequence(SEQ_040);
			player:EndEvent();
			GetWorldManager():WarpToPrivateArea(player, "PrivateAreaMasterPast", 5);
		end
	elseif (sequence == SEQ_040) then
		if (classId == SISIPU_EMOTE) then
			local emoteTestStep = quest:GetData():GetCounter(CNTR_SEQ40_FSH);
			if (emoteTestStep == 0 or emoteTestStep == 1) then
				callClientFunction(player, "delegateEvent", player, quest, "processEvent601_1");
				player:SendGameMessage(GetWorldMaster(), 25083, MESSAGE_TYPE_SYSTEM, 1);
				if (emoteTestStep == 0) then
					quest:GetData():IncCounter(CNTR_SEQ40_FSH);
				end
			elseif (emoteTestStep == 2) then
				callClientFunction(player, "delegateEvent", player, quest, "processEvent601_2");
				player:SendGameMessage(GetWorldMaster(), 25083, MESSAGE_TYPE_SYSTEM, 1);
			elseif (emoteTestStep == 3) then
				callClientFunction(player, "delegateEvent", player, quest, "processEvent601_3");
				player:SendGameMessage(GetWorldMaster(), 25083, MESSAGE_TYPE_SYSTEM, 1);
			elseif (emoteTestStep == 4) then
				callClientFunction(player, "delegateEvent", player, quest, "processEvent601_4");
				player:SendGameMessage(GetWorldMaster(), 25083, MESSAGE_TYPE_SYSTEM, 1);
			elseif (emoteTestStep == 5) then
				callClientFunction(player, "delegateEvent", player, quest, "processEvent601_5");
				player:SendGameMessage(GetWorldMaster(), 25083, MESSAGE_TYPE_SYSTEM, 1);
			elseif (emoteTestStep == 6) then
				callClientFunction(player, "delegateEvent", player, quest, "processEvent601_6");
				player:SendGameMessage(GetWorldMaster(), 25083, MESSAGE_TYPE_SYSTEM, 1);
			end			
		elseif (classId == NNMULIKA) then
			if (not inLimsaEcho(player, 5)) then
                -- Replay the entry cutscene for saves left outside by a failed warp.
                callClientFunction(player, "delegateEvent", player, quest, "processEvent600");
                quest:EndOfNpcLsMsgs();
                player:EndEvent();
                GetWorldManager():WarpToPrivateArea(player, "PrivateAreaMasterPast", 5);
                return;
            end
            callClientFunction(player, "delegateEvent", player, quest, "processEvent600_2");
		end
		player:EndEvent();
	elseif (sequence == SEQ_048) then
		if (classId == BADERON) then
			callClientFunction(player, "delegateEvent", player, quest, "processEvent602_3");			
		elseif (classId == NNMULIKA) then
			callClientFunction(player, "delegateEvent", player, quest, "processEvent602_2");		
		end		
		player:EndEvent();
	elseif (sequence == SEQ_055 or sequence == SEQ_060) then
		if (classId == SISIPU) then
			if (sequence == SEQ_060) then
				callClientFunction(player, "delegateEvent", player, quest, "processEvent615");
				quest:StartSequence(SEQ_065);
				player:EndEvent();
				GetWorldManager():WarpToPublicArea(player, -42.0, 37.678, 155.694, -1.25);
				return;
			else
				callClientFunction(player, "delegateEvent", player, quest, "processEvent605_2");					
			end
		elseif (classId == WINDWORN_CORPSE) then
			if (sequence == SEQ_055) then
				callClientFunction(player, "delegateEvent", player, quest, "processEvent610");
				quest:StartSequence(SEQ_060);
			else
				callClientFunction(player, "delegateEvent", player, quest, "processEvent610_2");
			end
		elseif (classId == FEARSTRICKEN_CORPSE) then
			callClientFunction(player, "delegateEvent", player, quest, "processEvent610_2");
		elseif (classId == GLASSYEYED_CORPSE) then
			callClientFunction(player, "delegateEvent", player, quest, "processEvent610_2");		
		end		
		player:EndEvent();
	elseif (sequence == SEQ_070) then
		if (classId == BADERON) then
			callClientFunction(player, "delegateEvent", player, quest, "processEvent615_2");
		end
		player:EndEvent();
	elseif (sequence == SEQ_075) then
		if (classId == BODENOLF) then
			callClientFunction(player, "delegateEvent", player, quest, "processEvent630");
			player:EndEvent();
			quest:StartSequence(SEQ_080);
			GetWorldManager():WarpToPrivateArea(player, "PrivateAreaMasterPast", 4, -504.985, 42.490, 433.712, 2.35);
		end
	elseif (sequence == SEQ_080) then
		if (classId == HNAANZA) then
			callClientFunction(player, "delegateEvent", player, quest, "processEvent632");
			player:EndEvent();
			quest:StartSequence(SEQ_085);
		else
			seq080_085_onTalk(player, quest, npc, classId);
		end
	elseif (sequence == SEQ_085) then
		if (classId == HNAANZA) then
			callClientFunction(player, "delegateEvent", player, quest, "processEvent632_2");
			player:EndEvent();
		else
			seq080_085_onTalk(player, quest, npc, classId);
		end
	elseif (sequence == SEQ_092) then
		if (classId == BADERON) then
			callClientFunction(player, "delegateEvent", player, quest, "processEventComplete");
			callClientFunction(player, "delegateEvent", player, quest, "sqrwa", 300, 1, 1, 2);
			player:EndEvent();
			player:CompleteQuest(quest);
			return;
		end
	end
	
	quest:UpdateENPCs();
end

function seq000_onTalk(player, quest, npc, classId)
	if     (classId == CRAPULOUS_ADVENTURER) then
		callClientFunction(player, "delegateEvent", player, quest, "processEvent010_2");
	elseif (classId == SKITTISH_ADVENTURER) then
		callClientFunction(player, "delegateEvent", player, quest, "processEvent010_3");
	elseif (classId == DUPLICITOUS_TRADER) then
		callClientFunction(player, "delegateEvent", player, quest, "processEvent010_4");	
	elseif (classId == DEBONAIR_PIRATE) then
		callClientFunction(player, "delegateEvent", player, quest, "processEvent010_5");
	elseif (classId == ONYXHAIRED_ADVENTURER) then
		callClientFunction(player, "delegateEvent", player, quest, "processEvent010_6");
	elseif (classId == RELAXING_ADVENTURER) then
		callClientFunction(player, "delegateEvent", player, quest, "processEvent010_7");
	elseif (classId == YSHTOLA) then		
		callClientFunction(player, "delegateEvent", player, quest, "processEvent010_8");
	elseif (classId == BADERON) then
		callClientFunction(player, "delegateEvent", player, quest, "processEvent020");
		quest:NewNpcLsMsg(1);
		quest:StartSequence(SEQ_003);
		player:EndEvent();		
		
		quest:UpdateENPCs();
		GetWorldManager():DoZoneChange(player, 133, nil, 0, 15, player.positionX, player.positionY, player.positionZ, player.rotation);
		return;
	elseif (classId == MYTESYN) then
		callClientFunction(player, "delegateEvent", player, quest, "processEvent010_7");	
	elseif (classId == COCKAHOOP_COCKSWAIN) then
		callClientFunction(player, "delegateEvent", player, quest, "processEtc001");
	elseif (classId == SOLICITOUS_SELLSWORD) then
		callClientFunction(player, "delegateEvent", player, quest, "processEtc002");
	elseif (classId == SENTENIOUS_SELLSWORD) then
		callClientFunction(player, "delegateEvent", player, quest, "processEtc003");
	end
	
	player:EndEvent();
end

function seq007_onTalk(player, quest, npc, classId)
	local data = quest:GetData();
	local subseqCUL = data:GetCounter(CNTR_SEQ7_CUL);
	local subseqMSK = data:GetCounter(CNTR_SEQ7_MSK);
	
	if (classId == BADERON) then
		if (subseqCUL == 1) then
			callClientFunction(player, "delegateEvent", player, quest, "processEvent027_3");
		elseif (subseqMSK == 4) then
			callClientFunction(player, "delegateEvent", player, quest, "processEvent027_4");
		else
			callClientFunction(player, "delegateEvent", player, quest, "processEvent027_2");
		end
	elseif (classId == CHARLYS) then
		if (subseqCUL == 0) then
			callClientFunction(player, "delegateEvent", player, quest, "processEvent030");
			data:IncCounter(CNTR_SEQ7_CUL);			
			if (data:GetCounter(CNTR_SEQ7_MSK) == 4) then
				seq007_endSequence(player, quest);
			end
			--give 1000g
		else
			callClientFunction(player, "delegateEvent", player, quest, "processEvent030_2");
		end
	elseif (classId == ISANDOREL) then
		if (subseqMSK == 2) then
			callClientFunction(player, "delegateEvent", player, quest, "processEvent050");
			data:IncCounter(CNTR_SEQ7_MSK);
			player:EndEvent();
			GetWorldManager():WarpToPrivateArea(player, "PrivateAreaMasterPast", 3);
			return;
		elseif (subseqMSK == 0) then
			callClientFunction(player, "delegateEvent", player, quest, "processEvent035");
			data:IncCounter(CNTR_SEQ7_MSK);
		elseif (subseqMSK == 1) then
			callClientFunction(player, "delegateEvent", player, quest, "processEvent035_2");
		end
	elseif (classId == MERLZIRN) then
		callClientFunction(player, "delegateEvent", player, quest, "processEvent40_2");
	elseif (classId == INTIMIDATING_BARRACUDA) then
		callClientFunction(player, "delegateEvent", player, quest, "processEvent050_2");
	elseif (classId == TOTORUTO) then
		callClientFunction(player, "delegateEvent", player, quest, "processEvent050_4");		
	elseif (classId == MANNSKOEN) then
		callClientFunction(player, "delegateEvent", player, quest, "processEvent050_6");
	elseif (classId == NERVOUS_BARRACUDA) then
		callClientFunction(player, "delegateEvent", player, quest, "processEvent050_7");
	elseif (classId == OVEREAGER_BARRACUDA) then
		callClientFunction(player, "delegateEvent", player, quest, "processEvent050_8");
	elseif (classId == SOPHISTICATED_BARRACUDA) then
		callClientFunction(player, "delegateEvent", player, quest, "processEvent050_9");
	elseif (classId == SMIRKING_BARRACUDA) then
		callClientFunction(player, "delegateEvent", player, quest, "processEvent050_10");
	elseif (classId == ADVENTURER2) then
		callClientFunction(player, "delegateEvent", player, quest, "processEvent050_13");
	elseif (classId == ADVENTURER3) then
		callClientFunction(player, "delegateEvent", player, quest, "processEvent050_14");
	elseif (classId == ADVENTURER1) then
		callClientFunction(player, "delegateEvent", player, quest, "processEvent050_15");
	end
		
	player:EndEvent();
end

function seq007_endSequence(player, quest)
	-- processEvent033 only emits the two QUEST-sheet dialogue lines; it
	-- never sends an EventUpdate.  Running it through callClientFunction
	-- would therefore park this push coroutine forever and prevent the
	-- echo-exit EndEvent/public reload below from running.
	player:SendGameMessage(quest, 333, 0x20);
	player:SendGameMessage(quest, 334, 0x20);
	quest:NewNpcLsMsg(1);
end

function seq080_085_onTalk(player, quest, npc, classId)
	if (classId == IOFA) then
		callClientFunction(player, "delegateEvent", player, quest, "processEvent630_2");
	elseif (classId == TRINNE) then
		callClientFunction(player, "delegateEvent", player, quest, "processEvent630_3");
	elseif (classId == HIHINE) then
		callClientFunction(player, "delegateEvent", player, quest, "processEvent630_4");
	elseif (classId == MIMIDOA) then
		callClientFunction(player, "delegateEvent", player, quest, "processEvent630_5");
	elseif (classId == WERNER) then
		callClientFunction(player, "delegateEvent", player, quest, "processEvent630_6");
	elseif (classId == TATTOOED_PIRATE) then
		callClientFunction(player, "delegateEvent", player, quest, "processEvent630_7");
	elseif (classId == JOELLAUT) then
		callClientFunction(player, "delegateEvent", player, quest, "processEvent630_8");
	elseif (classId == BODENOLF) then
		callClientFunction(player, "delegateEvent", player, quest, "processEvent630_9");
	end
	player:EndEvent();
end

function onPush(player, quest, npc)
	local data = quest:GetData();
	local sequence = quest:GetSequence();
	local classId = npc:GetActorClassId();
	
	if (sequence == SEQ_007) then
		if (classId == MSK_TRIGGER) then
			callClientFunction(player, "delegateEvent", player, quest, "processEvent040");
			data:IncCounter(CNTR_SEQ7_MSK);
			player:EndEvent();
			quest:UpdateENPCs();
			GetWorldManager():DoZoneChange(player, 230, nil, 0, 15, -620.0, 29.476, -70.050, 0.791);
		elseif (classId == ECHO_EXIT_TRIGGER) then
			callClientFunction(player, "delegateEvent", player, quest, "processEvent060");
			data:IncCounter(CNTR_SEQ7_MSK);
			if (data:GetCounter(CNTR_SEQ7_CUL) == 1) then
				seq007_endSequence(player, quest);
			end
				player:EndEvent();
				quest:UpdateENPCs();
				-- This is a fixed echo -> public Limsa transition.  Specify its
				-- public destination rather than relying on WarpToPublicArea's
				-- private-area guard, so a character saved by an older broken
				-- transition can recover instead of leaving the client at Now
				-- Loading with the server already in public Limsa.
				GetWorldManager():DoZoneChange(player, 230, nil, 0, 15,
					player.positionX, player.positionY, player.positionZ, player.rotation);
			end
	elseif (sequence == SEQ_048) then
		if (classId == ZEPHYR_TRIGGER) then
			local result = callClientFunction(player, "delegateEvent", player, quest, "contentsJoinAskInBasaClass");
			if (result == 1) then
				-- The Zephyr Gate escort duty (Garlemald-Server #46) —
				-- replaces the inherited pmeteor skip ("For now just
				-- skip the sequence") that jumped straight to the
				-- processEvent605 arrival. The escort's completion beat
				-- (605 → SEQ_055 → lighthouse-echo warp) now lives in
				-- QuestDirectorMan0l101's coroutine.
				startMan0l1Content(player, quest);
				return;
			end
			player:EndEvent();
		end
	elseif (sequence == SEQ_065) then
		if (classId == FSH_TRIGGER) then
			callClientFunction(player, "delegateEvent", player, quest, "processEvent620");			
			-- Fishermen's Guild payout for the strongbox delivery —
			-- 3,000 gil per the upstream TODO and the era guides
			-- (FFXIVenturer "Treasures of the Main": "You will receive
			-- 3000 gil"). (Garlemald-Server #46)
			player:AddGil(3000);
			player:EndEvent();
			quest:NewNpcLsMsg(1);
			quest:StartSequence(SEQ_070);
		end		
	elseif (sequence == SEQ_085) then
		if (classId == ECHO_EXIT_TRIGGER2) then
			callClientFunction(player, "delegateEvent", player, quest, "processEvent635");			
			player:EndEvent();			
			quest:NewNpcLsMsg(1);
			quest:StartSequence(SEQ_090);
			quest:UpdateENPCs();
			GetWorldManager():WarpToPublicArea(player);
		end
	end
end

function onEmote(player, quest, npc, eventName)
	local data = quest:GetData();
	local sequence = quest:GetSequence();
	local classId = npc:GetActorClassId();	

	-- Play the emote
	if (eventName == "emoteDefault1") then 		-- Bow
		player:DoEmote(npc.Id, 5, 21041);
	elseif (eventName == "emoteDefault2") then	-- Clap
		player:DoEmote(npc.Id, 7, 21061);
	elseif (eventName == "emoteDefault3") then	-- Congratulate
		player:DoEmote(npc.Id, 29, 21281);
	elseif (eventName == "emoteDefault4") then	-- Poke
		player:DoEmote(npc.Id, 28, 21271);
	elseif (eventName == "emoteDefault5") then	-- Joy
		player:DoEmote(npc.Id, 18, 21171);
	elseif (eventName == "emoteDefault6") then	-- Wave
		player:DoEmote(npc.Id, 16, 21151);
	end
	wait(2.5);
	
	-- Handle the result
	if (sequence == SEQ_040) then
		if (classId == SISIPU_EMOTE) then
			local emoteTestStep = data:GetCounter(CNTR_SEQ40_FSH);
			-- Bow
			if (emoteTestStep == 1) then
				if (eventName == "emoteDefault1") then
					callClientFunction(player, "delegateEvent", player, quest, "processEvent601_7");
					callClientFunction(player, "delegateEvent", player, quest, "processEvent601_2");
					player:SendGameMessage(GetWorldMaster(), 25083, MESSAGE_TYPE_SYSTEM, 1);
					data:IncCounter(CNTR_SEQ40_FSH);
				else
					callClientFunction(player, "delegateEvent", player, quest, "processEvent601_8");
					callClientFunction(player, "delegateEvent", player, quest, "processEvent601_1");
				end
			-- Clap
			elseif (emoteTestStep == 2) then
				if (eventName == "emoteDefault2") then
					callClientFunction(player, "delegateEvent", player, quest, "processEvent601_7");
					callClientFunction(player, "delegateEvent", player, quest, "processEvent601_3");
					player:SendGameMessage(GetWorldMaster(), 25083, MESSAGE_TYPE_SYSTEM, 1);
					data:IncCounter(CNTR_SEQ40_FSH);
				else
					callClientFunction(player, "delegateEvent", player, quest, "processEvent601_8");					
					callClientFunction(player, "delegateEvent", player, quest, "processEvent601_2");
				end
			-- Congratulate
			elseif (emoteTestStep == 3) then
				if (eventName == "emoteDefault3") then
					callClientFunction(player, "delegateEvent", player, quest, "processEvent601_7");
					callClientFunction(player, "delegateEvent", player, quest, "processEvent601_4");
					player:SendGameMessage(GetWorldMaster(), 25083, MESSAGE_TYPE_SYSTEM, 1);
					data:IncCounter(CNTR_SEQ40_FSH);
				else
					callClientFunction(player, "delegateEvent", player, quest, "processEvent601_8");					
					callClientFunction(player, "delegateEvent", player, quest, "processEvent601_3");
					player:SendGameMessage(GetWorldMaster(), 25083, MESSAGE_TYPE_SYSTEM, 1);
				end
			-- Poke
			elseif (emoteTestStep == 4) then
				if (eventName == "emoteDefault4") then
					callClientFunction(player, "delegateEvent", player, quest, "processEvent601_7");
					callClientFunction(player, "delegateEvent", player, quest, "processEvent601_5");
					player:SendGameMessage(GetWorldMaster(), 25083, MESSAGE_TYPE_SYSTEM, 1);
					data:IncCounter(CNTR_SEQ40_FSH);					
				else
					callClientFunction(player, "delegateEvent", player, quest, "processEvent601_8");
					callClientFunction(player, "delegateEvent", player, quest, "processEvent601_4");
				end
			-- Joy
			elseif (emoteTestStep == 5) then
				if (eventName == "emoteDefault5") then
					callClientFunction(player, "delegateEvent", player, quest, "processEvent601_7");
					callClientFunction(player, "delegateEvent", player, quest, "processEvent601_6");
					player:SendGameMessage(GetWorldMaster(), 25083, MESSAGE_TYPE_SYSTEM, 1);
					data:IncCounter(CNTR_SEQ40_FSH);
				else
					callClientFunction(player, "delegateEvent", player, quest, "processEvent601_8");					
					callClientFunction(player, "delegateEvent", player, quest, "processEvent601_5");
				end
			-- Wave
			elseif (emoteTestStep == 6) then
				if (eventName == "emoteDefault6") then
					callClientFunction(player, "delegateEvent", player, quest, "processEvent602");
					player:EndEvent();					
					quest:StartSequence(SEQ_048);
					GetWorldManager():WarpToPublicArea(player);
					return;
				else
					callClientFunction(player, "delegateEvent", player, quest, "processEvent601_8");					
					callClientFunction(player, "delegateEvent", player, quest, "processEvent601_6");
				end
			end
		end
	end
		
	player:EndEvent();
	quest:UpdateENPCs();
end

function onNotice(player, quest, target)
	-- Legacy/Aether parity (origin/main): the linkpearl step is driven purely
	-- by the NPC-linkshell alert armed before the warp (NewNpcLsMsg →
	-- SetNpcLs ALERT → the client pops the blinking linkshell icon). The
	-- pearl click is answered by onNpcLS (message + end-of-msgs). There is
	-- NO tutorial-mode widget here: dispatching the invented tutorial
	-- function (widget 15 with its Confirm button) was an invented (Garlemald)
	-- addition, and the client's confirm path derefs a NULL tutorial
	-- sub-object (X->field_8) and crashes. The notice itself only needs to
	-- close so the client is not event-locked.
	player:EndEvent();
	quest:UpdateENPCs();
end

function onNpcLS(player, quest, from, msgStep)
	local sequence = quest:GetSequence();
	local msgPack;

	if (from == 1) then
		-- Get the right msg pack
		if (sequence == SEQ_003) then
			msgPack = 1;
		elseif (sequence == SEQ_007 or sequence == SEQ_035) then
			msgPack = 2;
		elseif (sequence == SEQ_070 or sequence == SEQ_075) then
			msgPack = 3;
		elseif (sequence == SEQ_090 or sequence == SEQ_092) then
			msgPack = 4;
		end	
				
        -- A saved alert can outlive its dialogue stage (e.g. entering the guild
        -- after reading only the first message). Drain it without advancing.
        if (msgPack == nil or NPCLS_MSGS[msgPack][msgStep] == nil) then
            quest:EndOfNpcLsMsgs();
            player:EndEvent();
            return;
        end
		-- Quick way to handle all msgs nicely.
		player:SendGameMessageLocalizedDisplayName(quest, NPCLS_MSGS[msgPack][msgStep], MESSAGE_TYPE_NPC_LINKSHELL, 1000015);
		if (msgStep >= #NPCLS_MSGS[msgPack]) then
			quest:EndOfNpcLsMsgs();
		else
			quest:ReadNpcLsMsg();
		end
		
		-- Handle anything else. Legacy/Aether parity: the SEQ_003 pearl click
		-- is answered purely by the linkshell message above — no tutorial-
		-- widget tail (that path crashes the client's confirm handler). The
		-- single-message pack drains via EndOfNpcLsMsgs and the next beat is
		-- the aetheryte attunement.
		if (sequence == SEQ_007) then
			quest:StartSequenceForNpcLs(SEQ_035);
		elseif (sequence == SEQ_070) then
			quest:StartSequenceForNpcLs(SEQ_075);
		elseif (sequence == SEQ_090) then
			quest:StartSequenceForNpcLs(SEQ_092);
		end
	end
	
	player:EndEvent();
end

function startMan0l1Content(player, quest)
	-- One-shot rescue latch FIRST, journal update SECOND: the commands
	-- apply in queue order, so the onStateChange(SEQ_050) run that
	-- StartSequence fires sees the flag SET and stays dormant (it
	-- consumes the flag — see the SEQ_050 rescue arm). This is also
	-- retail's first entry beat (journal update precedes the instance
	-- messages). (Garlemald-Server #46 — escort leg.)
	quest:GetData():SetFlag(FLAG_ESCORT_HANDOFF);
	quest:StartSequence(SEQ_050);

	-- ===== CUTSCENE → SAME-MAP DUTY WARP (Garlemald-Server #46) =====
	-- The escort runs as a content instance on ZONE 128 — retail
	-- geography, pmeteor's intended (dead-code) shape: CreateContentArea
	-- on the same map at the gate, then DoZoneChangeContent to
	-- (-63.25, 33.15, 164.51, 0.8) with spawnType 16. The 6th
	-- CreateContentArea arg 128 is the parent zone (the Gridania escort
	-- passes 150 there the same way).
	local contentArea = player.CurrentArea:CreateContentArea(player, "/Area/PrivateArea/Content/PrivateAreaMasterSimpleContent", "Man0l101", "SimpleContentMan0l101", "Quest/QuestDirectorMan0l101", 128);
	if (contentArea == nil) then
		return;
	end
	local director = contentArea:GetContentDirector();
	player:AddDirector(director);
	director:StartDirector(false);
	-- Script order keeps the kick BEFORE the warp (pmeteor parity);
	-- DeferContentKickEvent parks it and the engine's zone-readiness ack
	-- (RX 0x0007 → ReleaseDeferredContentKickEvent) fires it post-warp —
	-- a kick riding the warp bundle AFTER DeleteAllActors is dropped by
	-- the client (the director actor was wiped), the client never
	-- answers, and the director coroutine never arms. This deferred
	-- delivery is this stack's equivalent of Garlemald's Rust-side
	-- deferred TX (wire-proven upstream, session 53943) and is the same
	-- mechanism the Gridania escort (man0g1.lua startMan0g1Content)
	-- uses. (Garlemald-Server #46.)
	player:DeferContentKickEvent(director, "noticeEvent", true);
	player:SetLoginDirector(director);

	-- 1. Cutscene IN PLACE at the gate. processEvent604 = fadeOut +
	--    NQCutScene("man0l604") + startFadeInCutSceneAfterWarp, which
	--    ARMS a Now-Loading veil that waits for the warp. The kicked
	--    noticeEvent arms a Now-Loading HOLD that
	--    QuestDirectorMan0l101's questBaseRewardSeting delegate (slot-66
	--    + fade + wait) dismisses as its first post-kick beat.
	callClientFunction(player, "delegateEvent", player, quest, "processEvent604");

	-- 2. Close the push event BEFORE the warp. An EndEvent (0x0131)
	--    landing mid-reload loses the client's _onPostEvent teardown →
	--    the session-global desktopWidgetMode-16 mask ("tutorial mode"
	--    menu lock) — wire-proven upstream; 0x0131 ahead of the 0x00E2
	--    reload latch is retail's invariant ordering on every captured
	--    transition. (Garlemald-Server #46.)
	player:EndEvent();

	-- 3. Duty warp into the zone-128 gate-side content instance (escort
	--    NPCs seeded on the southbound road — migration 000036).
	--    spawnType 16 (0x10) → force-reload branch (34108 "You have
	--    entered an instance." + DeleteAllActors + 0x00E2(0x10) +
	--    zone-in bundle). The retail entry text ("Protect Sisipu from
	--    harm." / "You are now bound by duty." / "There are 30 minutes
	--    remaining.") is emitted by the CONTENT script's first-sighting
	--    latch after the fade-in — anything queued here after the warp
	--    command would ship into the client's Now-Loading gap (the
	--    man0l0 Hob-crash shape).
	GetWorldManager():DoZoneChangeContent(player, contentArea, -63.25, 33.15, 164.51, 0.8, 16);
end

function getJournalInformation(player, quest)
	return 0, quest:GetData():GetCounter(CNTR_SEQ7_CUL) * 5, quest:GetData():GetCounter(CNTR_SEQ7_MSK) * 5;
end

function getJournalMapMarkerList(player, quest)
	local sequence = quest:GetSequence();
	
end
