using AetherXIV.Core.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using MoonSharp.Interpreter;
using AetherXIV.Core.Map.dataobjects;
using AetherXIV.Core.Map.dataobjects.chara;
using AetherXIV.Core.Map.lua;
using AetherXIV.Core.Map.packets.WorldPackets.Send.Group;
using AetherXIV.Core.Map.packets.WorldPackets.Send;
using AetherXIV.Core.Map.utils;
using AetherXIV.Core.Map.actors.group;
using AetherXIV.Core.Map.actors.chara.player;
using AetherXIV.Core.Map.actors.director;
using AetherXIV.Core.Map.actors.chara.npc;
using AetherXIV.Core.Map.actors.chara.ai;
using AetherXIV.Core.Map.actors.chara.ai.controllers;
using AetherXIV.Core.Map.actors.chara.ai.utils;
using AetherXIV.Core.Map.actors.chara.ai.state;
using AetherXIV.Core.Map.actors.chara;
using AetherXIV.Core.Map.actors.area;
using AetherXIV.Core.Map.packets.send;
using AetherXIV.Core.Map.packets.send.actor;
using AetherXIV.Core.Map.packets.send.events;
using AetherXIV.Core.Map.packets.send.actor.inventory;
using AetherXIV.Core.Map.packets.send.player;
using AetherXIV.Core.Map.packets.send.actor.battle;
using AetherXIV.Core.Map.packets.receive;
using AetherXIV.Core.Map.packets.receive.events;
using static AetherXIV.Core.Map.LuaUtils;
using AetherXIV.Core.Map.packets.send.actor.events;

namespace AetherXIV.Core.Map.Actors
{
    class PlayerBaseStatProfile
    {
        public readonly byte classId;
        public readonly byte tribe;
        public readonly short level;
        public readonly short hp;
        public readonly short mp;
        public readonly short strength;
        public readonly short vitality;
        public readonly short dexterity;
        public readonly short intelligence;
        public readonly short mind;
        public readonly short piety;
        public readonly string source;

        public PlayerBaseStatProfile(byte classId, byte tribe, short level, short hp, short mp, short strength, short vitality, short dexterity, short intelligence, short mind, short piety, string source)
        {
            this.classId = classId;
            this.tribe = tribe;
            this.level = level;
            this.hp = hp;
            this.mp = mp;
            this.strength = strength;
            this.vitality = vitality;
            this.dexterity = dexterity;
            this.intelligence = intelligence;
            this.mind = mind;
            this.piety = piety;
            this.source = source;
        }
    }

    class PlayerClassAttributeAllocation
    {
        public readonly byte classId;
        public readonly short pointsRemaining;
        public readonly short strength;
        public readonly short vitality;
        public readonly short dexterity;
        public readonly short intelligence;
        public readonly short mind;
        public readonly short piety;

        public PlayerClassAttributeAllocation(byte classId, short pointsRemaining, short strength, short vitality, short dexterity, short intelligence, short mind, short piety)
        {
            this.classId = classId;
            this.pointsRemaining = pointsRemaining;
            this.strength = strength;
            this.vitality = vitality;
            this.dexterity = dexterity;
            this.intelligence = intelligence;
            this.mind = mind;
            this.piety = piety;
        }

        public short SpentPoints()
        {
            return (short)(strength + vitality + dexterity + intelligence + mind + piety);
        }
    }

    class PlayerAttributePointState
    {
        public readonly short available;
        public readonly short limit;
        public readonly short inSTR;
        public readonly short inVIT;
        public readonly short inDEX;
        public readonly short inINT;
        public readonly short inMIN;
        public readonly short inPIE;

        public PlayerAttributePointState(short available, short limit, PlayerClassAttributeAllocation allocation)
        {
            this.available = available;
            this.limit = limit;
            inSTR = allocation.strength;
            inVIT = allocation.vitality;
            inDEX = allocation.dexterity;
            inINT = allocation.intelligence;
            inMIN = allocation.mind;
            inPIE = allocation.piety;
        }
    }

    class Player : Character
    {
        public const int JOBID_MNK = 15;
        public const int JOBID_PLD = 16;
        public const int JOBID_WAR = 17;
        public const int JOBID_BRD = 18;
        public const int JOBID_DRG = 19;
        public const int JOBID_BLM = 26;
        public const int JOBID_WHM = 27;

        public const int TIMER_TOTORAK = 0;
        public const int TIMER_DZEMAEL = 1;
        public const int TIMER_BOWL_OF_EMBERS_HARD = 2;
        public const int TIMER_BOWL_OF_EMBERS = 3;
        public const int TIMER_THORNMARCH = 4;
        public const int TIMER_AURUMVALE = 5;
        public const int TIMER_CUTTERSCRY = 6;
        public const int TIMER_BATTLE_ALEPORT = 7;
        public const int TIMER_BATTLE_HYRSTMILL = 8;
        public const int TIMER_BATTLE_GOLDENBAZAAR = 9;
        public const int TIMER_HOWLING_EYE_HARD = 10;
        public const int TIMER_HOWLING_EYE = 11;
        public const int TIMER_CASTRUM_TOWER = 12;
        public const int TIMER_BOWL_OF_EMBERS_EXTREME = 13;
        public const int TIMER_RIVENROAD = 14;
        public const int TIMER_RIVENROAD_HARD = 15;
        public const int TIMER_BEHEST = 16;
        public const int TIMER_COMPANYBEHEST = 17;
        public const int TIMER_RETURN = 18;
        public const int TIMER_SKIRMISH = 19;

        public const int NPCLS_GONE = 0;
        public const int NPCLS_INACTIVE = 1;
        public const int NPCLS_ACTIVE = 2;
        public const int NPCLS_ALERT = 3;
        public const uint NPC_LINKSHELL_COUNT = 64;

        public const int SLOT_MAINHAND = 0;
        public const int SLOT_OFFHAND = 1;
        public const int SLOT_THROWINGWEAPON = 4;
        public const int SLOT_PACK = 5;
        public const int SLOT_POUCH = 6;
        public const int SLOT_HEAD = 8;
        public const int SLOT_UNDERSHIRT = 9;
        public const int SLOT_BODY = 10;
        public const int SLOT_UNDERGARMENT = 11;
        public const int SLOT_LEGS = 12;
        public const int SLOT_HANDS = 13;
        public const int SLOT_BOOTS = 14;
        public const int SLOT_WAIST = 15;
        public const int SLOT_NECK = 16;
        public const int SLOT_EARS = 17;
        public const int SLOT_WRISTS = 19;
        public const int SLOT_RIGHTFINGER = 21;
        public const int SLOT_LEFTFINGER = 22;

        public static int[] MAXEXP = {570, 700, 880, 1100, 1500, 1800, 2300, 3200, 4300, 5000,                   //Level <= 10
                                     5900, 6800, 7700, 8700, 9700, 11000, 12000, 13000, 15000, 16000,            //Level <= 20
                                     20000, 22000, 23000, 25000, 27000, 29000, 31000, 33000, 35000, 38000,       //Level <= 30
                                     45000, 47000, 50000, 53000, 56000, 59000, 62000, 65000, 68000, 71000,       //Level <= 40
                                     74000, 78000, 81000, 85000, 89000, 92000, 96000, 100000, 100000, 110000};   //Level <= 50

        // Event Related — plain fields, matching legacy Meteor/Garlemald.
        public uint currentEventOwner = 0;
        public string currentEventName = "";
        public byte currentEventType = 0;
        public Coroutine currentEventRunning;
        public uint currentCutsceneState = 0;
        public string currentCutsceneName = "";
        public uint currentCutsceneDetail = 0;

        //Player Info
        public uint destinationZone;
        public ushort destinationSpawnType;
        public uint[] timers = new uint[20];
        public uint currentTitle;
        public uint playTime;
        public uint lastPlayTimeUpdate;
        public bool isGM = false;
        public bool isZoneChanging = true;
        private bool hasExpectedZoneChangePosition;
        private bool awaitingZoneReadyAcknowledgement;
        private uint expectedZoneChangeZone;
        private float expectedZoneChangeX;
        private float expectedZoneChangeY;
        private float expectedZoneChangeZ;
        private float expectedZoneChangeRotation;
        private uint rejectedZoneChangePositionCount;

        //Trading
        private Player otherTrader = null;
        private ReferencedItemPackage myOfferings;
        private bool isTradeAccepted = false;

        //GC Related
        public byte gcCurrent;
        public byte gcRankLimsa;
        public byte gcRankGridania;
        public byte gcRankUldah;

        //Mount Related
        public bool hasChocobo;
        public bool hasGoobbue;
        public string chocoboName;
        public byte mountState = 0;
        public byte chocoboAppearance;
        public byte rentalChocoboAppearance = ChocoboPolicy.RentalAppearance;
        public uint rentalExpireTime = 0;
        public byte rentalMinLeft = 0;
        public ChocoboRideKind chocoboRideKind = ChocoboRideKind.None;

        public uint achievementPoints;

        //Property Array Request Stuff
        private int lastPosition = 0;
        private int lastStep = 0;

        //Quest Actors (MUST MATCH playerWork.questScenario/questGuildleve)
        public Quest[] questScenario = new Quest[16];
        public uint[] questGuildleve = new uint[8];
        public QuestStateManager questStateManager;

        //Aetheryte
        public uint homepoint = 0;
        public byte homepointInn = 0;
        // Attuned-aetheryte set (Garlemald characters_aetherytes parity):
        // hydrated from the DB at load, mutated by UnlockAetheryteNode,
        // gate for HasAetheryteNodeUnlocked and the TeleportCommand refusal.
        public readonly HashSet<uint> unlockedAetherytes = new HashSet<uint>();

        //Nameplate Stuff
        public uint currentLSPlate = 0;
        public byte repairType = 0;

        //Retainer
        RetainerMeetingRelationGroup retainerMeetingGroup = null;
        public Retainer currentSpawnedRetainer = null;
        public bool sentRetainerSpawn = false;

        private List<Director> ownedDirectors = new List<Director>();
        private Director loginInitDirector = null;
        private Actor deferredContentKickOwner = null;
        private string deferredContentKickEventName = null;
        private object[] deferredContentKickParameters = null;

        // Garlemald pending_kick_event / pending_content_kick_event: a notice
        // kicked in a SetLoginDirector (login-scoped) burst is parked instead
        // of sent immediately. A non-content warp (login zone-in, ordinary
        // zone change) emits it at the END of the zone-in bundle, after all
        // spawns; a DoZoneChangeContent re-parks it to the content slot, and
        // the client's 0x0007(-1) zone-in-complete ack releases it — so the
        // kick survives the content warp's actor wipe and lands on a director
        // the client knows. (Garlemald processor.rs is_login_scoped_burst /
        // pending_content_kick_event; port ledger "content-warp kick".)
        // Path companion (legacy "special NPC") state used by quest events.
        public string SNpcNickname { set; get; } = "???";
        public byte SNpcSkin { set; get; } = 1;
        public byte SNpcPersonality { set; get; } = 1;
        public short SNpcCoordinate { set; get; } = 1;

        List<ushort> hotbarSlotsToUpdate = new List<ushort>();

        public PlayerWork playerWork = new PlayerWork();
        private readonly Dictionary<byte, PlayerClassAttributeAllocation> classAttributeAllocations = new Dictionary<byte, PlayerClassAttributeAllocation>();
        private readonly Dictionary<string, PlayerBaseStatProfile> baseStatProfiles = new Dictionary<string, PlayerBaseStatProfile>();
        private readonly HashSet<string> missingBaseStatProfiles = new HashSet<string>();

        public Session playerSession;

        public Player(Session cp, uint actorID) : base(actorID)
        {
            playerSession = cp;
            actorName = String.Format("_pc{0:00000000}", actorID);
            className = "Player";

            moveSpeeds[0] = SetActorSpeedPacket.DEFAULT_STOP;
            moveSpeeds[1] = SetActorSpeedPacket.DEFAULT_WALK;
            moveSpeeds[2] = SetActorSpeedPacket.DEFAULT_RUN;
            moveSpeeds[3] = SetActorSpeedPacket.DEFAULT_ACTIVE;

            itemPackages[ItemPackage.NORMAL] = new ItemPackage(this, ItemPackage.MAXSIZE_NORMAL, ItemPackage.NORMAL);
            itemPackages[ItemPackage.KEYITEMS] = new ItemPackage(this, ItemPackage.MAXSIZE_KEYITEMS, ItemPackage.KEYITEMS);
            itemPackages[ItemPackage.CURRENCY_CRYSTALS] = new ItemPackage(this, ItemPackage.MAXSIZE_CURRANCY, ItemPackage.CURRENCY_CRYSTALS);
            itemPackages[ItemPackage.MELDREQUEST] = new ItemPackage(this, ItemPackage.MAXSIZE_MELDREQUEST, ItemPackage.MELDREQUEST);
            itemPackages[ItemPackage.BAZAAR] = new ItemPackage(this, ItemPackage.MAXSIZE_BAZAAR, ItemPackage.BAZAAR);
            itemPackages[ItemPackage.LOOT] = new ItemPackage(this, ItemPackage.MAXSIZE_LOOT, ItemPackage.LOOT);
            equipment = new ReferencedItemPackage(this, ItemPackage.MAXSIZE_EQUIPMENT, ItemPackage.EQUIPMENT);

            //Set the Skill level caps of all FFXIV (classes)skills to 50
            for (int i = 0; i < charaWork.battleSave.skillLevelCap.Length; i++)
            {
                if (i != CLASSID_PUG &&
                    i != CLASSID_MRD &&
                    i != CLASSID_GLA &&
                    i != CLASSID_MRD &&
                    i != CLASSID_ARC &&
                    i != CLASSID_LNC &&
                    i != CLASSID_THM &&
                    i != CLASSID_CNJ &&
                    i != CLASSID_CRP &&
                    i != CLASSID_BSM &&
                    i != CLASSID_ARM &&
                    i != CLASSID_GSM &&
                    i != CLASSID_LTW &&
                    i != CLASSID_WVR &&
                    i != CLASSID_ALC &&
                    i != CLASSID_CUL &&
                    i != CLASSID_MIN &&
                    i != CLASSID_BTN &&
                    i != CLASSID_FSH)
                    charaWork.battleSave.skillLevelCap[i] = 0xFF;
                else
                    charaWork.battleSave.skillLevelCap[i] = 50;

            }

            charaWork.property[0] = 1;
            charaWork.property[1] = 1;
            charaWork.property[2] = 1;
            charaWork.property[4] = 1;

            charaWork.command[0] =  0xA0F00000 | 21001;
            charaWork.command[1] =  0xA0F00000 | 21001;

            charaWork.command[2] =  0xA0F00000 | 21002;
            charaWork.command[3] =  0xA0F00000 | 12004;
            charaWork.command[4] =  0xA0F00000 | 21005;
            charaWork.command[5] =  0xA0F00000 | 21006;
            charaWork.command[6] =  0xA0F00000 | 21007;
            charaWork.command[7] =  0xA0F00000 | 12009;
            charaWork.command[8] =  0xA0F00000 | 12010;
            charaWork.command[9] =  0xA0F00000 | 12005;
            charaWork.command[10] = 0xA0F00000 | 12007;
            charaWork.command[11] = 0xA0F00000 | 12011;
            charaWork.command[12] = 0xA0F00000 | 22012;
            charaWork.command[13] = 0xA0F00000 | 22013;
            charaWork.command[14] = 0xA0F00000 | 29497;
            charaWork.command[15] = 0xA0F00000 | 22015;            

            charaWork.commandAcquired[27150 - 26000] = true;

            // Do not pre-mark 110001 (Man0l0, Limsa's opening) complete here. Doing so made
            // IsQuestCompleted(110001) true at construction, blocking player.lua's
            // ensureOpeningQuest from ever accepting it and leaving Limsa characters with no
            // OpeningDirector, opening cutscene, or control-scheme widget. Completion is set
            // by the normal CompleteQuest/ReplaceQuest paths once the opening actually runs.
            playerWork.questGuildleveComplete[120050 - 120001] = true;

            for (int i = 0; i < charaWork.additionalCommandAcquired.Length; i++ )
                charaWork.additionalCommandAcquired[i] = true;
            
            for (int i = 0; i < charaWork.commandCategory.Length; i++)
                charaWork.commandCategory[i] = 1;

            charaWork.battleTemp.generalParameter[3] = 1;

            charaWork.eventSave.bazaarTax = 5;
            charaWork.battleSave.potencial = 6.6f;

            charaWork.battleSave.negotiationFlag[0] = true;

            charaWork.commandCategory[0] = 1;
            charaWork.commandCategory[1] = 1;

            charaWork.parameterSave.commandSlot_compatibility[0] = true;
            charaWork.parameterSave.commandSlot_compatibility[1] = true;

            charaWork.commandBorder = 0x20;

            charaWork.parameterTemp.tp = 0;

            Database.LoadPlayerCharacter(this);
            isGM = ConfigConstants.GM_CHARACTER_IDS.Contains(actorId);
            if (equipment.GetItemAtSlot(SLOT_MAINHAND) != null)
                RefreshEquipmentAppearance(false);
            lastPlayTimeUpdate = Utils.UnixTimeStampUTC();

            this.aiContainer = new AIContainer(this, new PlayerController(this), null, new TargetFind(this));
            allegiance = CharacterTargetingAllegiance.Player;
            RecalculateStats("login");

            questStateManager = new QuestStateManager(this);
            questStateManager.Init(questScenario, playerWork.questScenarioComplete);
            questStateManager.DiagnoseConsistency(
                questScenario,
                playerWork.questScenario,
                "player-construction");
        }

        public List<SubPacket> Create0x132Packets()
        {
            List<SubPacket> packets = new List<SubPacket>();
            packets.Add(_0x132Packet.BuildPacket(actorId, 0xB, "commandForced"));
            packets.Add(_0x132Packet.BuildPacket(actorId, 0xA, "commandDefault"));
            packets.Add(_0x132Packet.BuildPacket(actorId, 0x6, "commandWeak"));
            packets.Add(_0x132Packet.BuildPacket(actorId, 0x4, "commandContent"));
            packets.Add(_0x132Packet.BuildPacket(actorId, 0x6, "commandJudgeMode"));
            packets.Add(_0x132Packet.BuildPacket(actorId, 0x100, "commandRequest"));
            packets.Add(_0x132Packet.BuildPacket(actorId, 0x100, "widgetCreate"));
            packets.Add(_0x132Packet.BuildPacket(actorId, 0x100, "macroRequest"));
            return packets;
        }

        /*        
         * PLAYER ARGS:
         * Unknown - Bool 
         * Unknown - Bool
         * Is Init Director - Bool
         * Unknown - Bool
         * Unknown - Number
         * Unknown - Bool
         * Timer Array - 20 Number
        */

        public override SubPacket CreateScriptBindPacket(Player requestPlayer)
        {
            List<LuaParam> lParams;
            if (IsMyPlayer(requestPlayer.actorId))
            {
                if (loginInitDirector != null)
                    lParams = LuaUtils.CreateLuaParamList("/Chara/Player/Player_work", false, false, true, loginInitDirector, true, 0, false, timers, true);
                else
                    lParams = LuaUtils.CreateLuaParamList("/Chara/Player/Player_work", true, false, false, true, 0, false, timers, true);
            }
            else
                lParams = LuaUtils.CreateLuaParamList("/Chara/Player/Player_work", false, false, false, false, false, true);

            ActorInstantiatePacket.BuildPacket(
                actorId,
                actorName,
                className,
                lParams,
                GetActorInstantiationAreaKey(requestPlayer)).DebugPrintSubPacket();


            return ActorInstantiatePacket.BuildPacket(
                actorId,
                actorName,
                className,
                lParams,
                GetActorInstantiationAreaKey(requestPlayer));
        }

        public override List<SubPacket> GetSpawnPackets(Player requestPlayer, ushort spawnType)
        {
            List<SubPacket> subpackets = new List<SubPacket>();
            subpackets.Add(CreateAddActorPacket(8));
            if (IsMyPlayer(requestPlayer.actorId))
                subpackets.AddRange(Create0x132Packets());
            subpackets.Add(CreateSpeedPacket());
            subpackets.Add(CreateSpawnPositonPacket(this, spawnType));
            subpackets.Add(CreateAppearancePacket());
            subpackets.Add(CreateNamePacket());
            subpackets.Add(_0xFPacket.BuildPacket(actorId));
            subpackets.Add(CreateStatePacket());
            subpackets.Add(CreateSubStatePacket());
            subpackets.Add(CreateInitStatusPacket());
            subpackets.Add(CreateSetActorIconPacket());
            subpackets.Add(CreateIsZoneingPacket());
            subpackets.AddRange(CreatePlayerRelatedPackets(requestPlayer.actorId));
            subpackets.Add(CreateScriptBindPacket(requestPlayer));
            return subpackets;
        }

        public List<SubPacket> CreatePlayerRelatedPackets(uint requestingPlayerActorId)
        {
            List<SubPacket> subpackets = new List<SubPacket>();

            if (gcCurrent != 0)
                subpackets.Add(SetGrandCompanyPacket.BuildPacket(actorId, gcCurrent, gcRankLimsa, gcRankGridania, gcRankUldah));

            if (currentTitle != 0)
                subpackets.Add(SetPlayerTitlePacket.BuildPacket(actorId, currentTitle));

            if (currentJob != 0)
                subpackets.Add(SetCurrentJobPacket.BuildPacket(actorId, currentJob));

            if (IsMyPlayer(requestingPlayerActorId))
            {
                subpackets.Add(SetSpecialEventWorkPacket.BuildPacket(actorId));

                if (hasChocobo && chocoboName != null && !chocoboName.Equals(""))
                {
                    subpackets.Add(SetChocoboNamePacket.BuildPacket(actorId, chocoboName));
                    subpackets.Add(SetHasChocoboPacket.BuildPacket(actorId, hasChocobo));
                }

                if (hasGoobbue)
                    subpackets.Add(SetHasGoobbuePacket.BuildPacket(actorId, hasGoobbue));

                subpackets.Add(SetAchievementPointsPacket.BuildPacket(actorId, achievementPoints));

                subpackets.Add(Database.GetLatestAchievements(this));
                subpackets.Add(Database.GetAchievementsPacket(this));
            }

            if (mountState == 1)
                subpackets.Add(SetCurrentMountChocoboPacket.BuildPacket(actorId, GetRideChocoboAppearance(), rentalExpireTime, rentalMinLeft));
            else if (mountState == 2)
                subpackets.Add(SetCurrentMountGoobbuePacket.BuildPacket(actorId, 1));

            //Inn Packets (Dream, Cutscenes, Armoire)   
            if (zone.isInn)
            {
                SetCutsceneBookPacket cutsceneBookPacket = new SetCutsceneBookPacket();
                for (int i = 0; i < 2048; i++)
                    cutsceneBookPacket.cutsceneFlags[i] = true;
                QueuePacket(cutsceneBookPacket.BuildPacket(actorId, "<Path Companion>", 11, 1, 1));
                QueuePacket(SetPlayerDreamPacket.BuildPacket(actorId, 0x16, GetInnCode()));
            }

            return subpackets;
        }

        public override List<SubPacket> GetInitPackets()
        {
            ActorPropertyPacketUtil propPacketUtil = new ActorPropertyPacketUtil("/_init", this);

            propPacketUtil.AddProperty("charaWork.eventSave.bazaarTax");
            propPacketUtil.AddProperty("charaWork.battleSave.potencial");

            //Properties
            for (int i = 0; i < charaWork.property.Length; i++)
            {
                if (charaWork.property[i] != 0)
                    propPacketUtil.AddProperty(String.Format("charaWork.property[{0}]", i));
            }

            //Parameters
            propPacketUtil.AddProperty("charaWork.parameterSave.hp[0]");
            propPacketUtil.AddProperty("charaWork.parameterSave.hpMax[0]");
            propPacketUtil.AddProperty("charaWork.parameterSave.mp");
            propPacketUtil.AddProperty("charaWork.parameterSave.mpMax");
            propPacketUtil.AddProperty("charaWork.parameterTemp.tp");
            propPacketUtil.AddProperty("charaWork.parameterSave.state_mainSkill[0]");
            propPacketUtil.AddProperty("charaWork.parameterSave.state_mainSkillLevel");

            //Status Times
            for (int i = 0; i < charaWork.statusShownTime.Length; i++)
            {
                if (charaWork.statusShownTime[i] != 0)
                    propPacketUtil.AddProperty(String.Format("charaWork.statusShownTime[{0}]", i));
            }

            //General Parameters
            for (int i = 3; i < charaWork.battleTemp.generalParameter.Length; i++)
            {
                if (charaWork.battleTemp.generalParameter[i] != 0)
                    propPacketUtil.AddProperty(String.Format("charaWork.battleTemp.generalParameter[{0}]", i));
            }

            propPacketUtil.AddProperty("charaWork.battleTemp.castGauge_speed[0]");
            propPacketUtil.AddProperty("charaWork.battleTemp.castGauge_speed[1]");

            //Battle Save Skillpoint
            propPacketUtil.AddProperty(String.Format("charaWork.battleSave.skillPoint[{0}]", charaWork.parameterSave.state_mainSkill[0] - 1));

            //Commands
            propPacketUtil.AddProperty("charaWork.commandBorder");

            propPacketUtil.AddProperty("charaWork.battleSave.negotiationFlag[0]");

            for (int i = 0; i < charaWork.command.Length; i++)
            {
                if (charaWork.command[i] != 0)
                {
                    propPacketUtil.AddProperty(String.Format("charaWork.command[{0}]", i));
                    //Recast Timers
                    if (i >= charaWork.commandBorder)
                    {
                        propPacketUtil.AddProperty(String.Format("charaWork.parameterTemp.maxCommandRecastTime[{0}]", i - charaWork.commandBorder));
                        propPacketUtil.AddProperty(String.Format("charaWork.parameterSave.commandSlot_recastTime[{0}]", i - charaWork.commandBorder));
                    }
                }
            }

            for (int i = 0; i < charaWork.commandCategory.Length; i++)
            {
                charaWork.commandCategory[i] = 1;
                if (charaWork.commandCategory[i] != 0)
                    propPacketUtil.AddProperty(String.Format("charaWork.commandCategory[{0}]", i));
            }

            for (int i = 0; i < charaWork.commandAcquired.Length; i++)
            {
                if (charaWork.commandAcquired[i] != false)
                    propPacketUtil.AddProperty(String.Format("charaWork.commandAcquired[{0}]", i));
            }

            for (int i = 0; i < charaWork.additionalCommandAcquired.Length; i++)
            {
                if (charaWork.additionalCommandAcquired[i] != false)
                    propPacketUtil.AddProperty(String.Format("charaWork.additionalCommandAcquired[{0}]", i));
            }

            for (int i = 0; i < charaWork.parameterSave.commandSlot_compatibility.Length; i++)
            {
                charaWork.parameterSave.commandSlot_compatibility[i] = true;
                if (charaWork.parameterSave.commandSlot_compatibility[i])
                    propPacketUtil.AddProperty(String.Format("charaWork.parameterSave.commandSlot_compatibility[{0}]", i));
            }

            for (int i = 0; i < charaWork.parameterSave.commandSlot_recastTime.Length; i++)
            {
                if (charaWork.parameterSave.commandSlot_recastTime[i] != 0)
                    propPacketUtil.AddProperty(String.Format("charaWork.parameterSave.commandSlot_recastTime[{0}]", i));
            }

            //System
            propPacketUtil.AddProperty("charaWork.parameterTemp.forceControl_float_forClientSelf[0]");
            propPacketUtil.AddProperty("charaWork.parameterTemp.forceControl_float_forClientSelf[1]");
            propPacketUtil.AddProperty("charaWork.parameterTemp.forceControl_int16_forClientSelf[0]");
            propPacketUtil.AddProperty("charaWork.parameterTemp.forceControl_int16_forClientSelf[1]");

            charaWork.parameterTemp.otherClassAbilityCount[0] = 4;
            charaWork.parameterTemp.otherClassAbilityCount[1] = 5;
            charaWork.parameterTemp.giftCount[1] = 5;

            propPacketUtil.AddProperty("charaWork.parameterTemp.otherClassAbilityCount[0]");
            propPacketUtil.AddProperty("charaWork.parameterTemp.otherClassAbilityCount[1]");
            propPacketUtil.AddProperty("charaWork.parameterTemp.giftCount[1]");

            propPacketUtil.AddProperty("charaWork.depictionJudge");

            //Scenario
            for (int i = 0; i < playerWork.questScenario.Length; i++)
            {
                if (playerWork.questScenario[i] != 0)
                    propPacketUtil.AddProperty(String.Format("playerWork.questScenario[{0}]", i));
            }

            //Guildleve - Local
            for (int i = 0; i < playerWork.questGuildleve.Length; i++)
            {
                if (playerWork.questGuildleve[i] != 0)
                    propPacketUtil.AddProperty(String.Format("playerWork.questGuildleve[{0}]", i));
            }

            //Guildleve - Regional
            for (int i = 0; i < work.guildleveId.Length; i++)
            {
                if (work.guildleveId[i] != 0)
                    propPacketUtil.AddProperty(String.Format("work.guildleveId[{0}]", i));
                if (work.guildleveDone[i] != false)
                    propPacketUtil.AddProperty(String.Format("work.guildleveDone[{0}]", i));
                if (work.guildleveChecked[i] != false)
                    propPacketUtil.AddProperty(String.Format("work.guildleveChecked[{0}]", i));
            }

            //Bazaar
            CheckBazaarFlags(true);
            if (charaWork.eventSave.repairType != 0)
                propPacketUtil.AddProperty("charaWork.eventSave.repairType");
            if (charaWork.eventTemp.bazaarRetail)
                propPacketUtil.AddProperty("charaWork.eventTemp.bazaarRetail");
            if (charaWork.eventTemp.bazaarRepair)
                propPacketUtil.AddProperty("charaWork.eventTemp.bazaarRepair");
            if (charaWork.eventTemp.bazaarMateria)
                propPacketUtil.AddProperty("charaWork.eventTemp.bazaarMateria");

            //NPC Linkshell            
            for (int i = 0; i < playerWork.npcLinkshellChatCalling.Length; i++)
            {
                if (playerWork.npcLinkshellChatCalling[i] != false)
                    propPacketUtil.AddProperty(String.Format("playerWork.npcLinkshellChatCalling[{0}]", i));
                if (playerWork.npcLinkshellChatExtra[i] != false)
                    propPacketUtil.AddProperty(String.Format("playerWork.npcLinkshellChatExtra[{0}]", i));
            }

            propPacketUtil.AddProperty("playerWork.restBonusExpRate");

            //Profile
            propPacketUtil.AddProperty("playerWork.tribe");
            propPacketUtil.AddProperty("playerWork.guardian");
            propPacketUtil.AddProperty("playerWork.birthdayMonth");
            propPacketUtil.AddProperty("playerWork.birthdayDay");
            propPacketUtil.AddProperty("playerWork.initialTown");

            return propPacketUtil.Done();
        }

        public void SendSeamlessZoneInPackets(Director previousWeatherDirector)
        {
            QueuePacket(SetDalamudPacket.BuildPacket(actorId, zone.GetDalamudLevel()));
            QueuePacket(SetMusicPacket.BuildPacket(actorId, zone.bgmDay, SetMusicPacket.EFFECT_FADEIN));
            // Bootstrap is required: the local WeatherDirector skips _setWeather when its prior ID is zero.
            QueuePacket(SetWeatherPacket.BuildPacket(actorId, zone.GetCurrentWeather(), NormalWeatherPolicy.EntryTransition));
            QueuePacket(SetMapPacket.BuildPacket(actorId, zone.regionId, zone.GetTerritoryId()));
            Director currentWeatherDirector = zone.GetWeatherDirector();
            if (previousWeatherDirector != currentWeatherDirector)
            {
                if (previousWeatherDirector != null)
                    QueuePacket(RemoveActorPacket.BuildPacket(previousWeatherDirector.actorId));
                if (currentWeatherDirector != null)
                {
                    QueuePackets(currentWeatherDirector.GetSpawnPackets());
                    QueuePackets(currentWeatherDirector.GetInitPackets());
                }
            }

        }

        public void SendZoneInPackets(
            WorldManager world,
            ushort spawnType,
            ZoneInventoryRefreshMode inventoryRefreshMode = ZoneInventoryRefreshMode.Full)
        {
            QueuePacket(SetActorIsZoningPacket.BuildPacket(actorId, false));
            QueuePacket(SetDalamudPacket.BuildPacket(actorId, zone.GetDalamudLevel()));

            //Music Packets
            if (currentMainState == SetActorStatePacket.MAIN_STATE_MOUNTED)
            {
                if (rentalExpireTime != 0)
                    QueuePacket(SetMusicPacket.BuildPacket(actorId, 64, SetMusicPacket.EFFECT_FADEIN)); //Rental
                else
                {
                    if (mountState == 1)
                        QueuePacket(SetMusicPacket.BuildPacket(actorId, 83, SetMusicPacket.EFFECT_FADEIN)); //Mount
                    else
                        QueuePacket(SetMusicPacket.BuildPacket(actorId, 98, 0x01)); //Goobbue
                }
            }
            else
                QueuePacket(SetMusicPacket.BuildPacket(actorId, zone.bgmDay, 0x01)); //Zone

            // Bootstrap is required: the local WeatherDirector skips _setWeather when its prior ID is zero.
            QueuePacket(SetWeatherPacket.BuildPacket(actorId, zone.GetCurrentWeather(), NormalWeatherPolicy.EntryTransition));

            QueuePacket(SetMapPacket.BuildPacket(actorId, zone.regionId, zone.GetTerritoryId()));

            List<SubPacket> selfSpawnPackets = GetSpawnPackets(this, spawnType);
            QueuePackets(selfSpawnPackets);

            #region Inventory & Equipment
            QueuePacket(InventoryBeginChangePacket.BuildPacket(actorId, true));
            bool resendItemDefinitions = ZoneInventoryRefreshPolicy.ShouldResendItemDefinitions(inventoryRefreshMode);
            ushort[] zoneInPackages =
            {
                ItemPackage.NORMAL,
                ItemPackage.CURRENCY_CRYSTALS,
                ItemPackage.KEYITEMS,
                ItemPackage.BAZAAR,
                ItemPackage.MELDREQUEST,
                ItemPackage.LOOT
            };
            foreach (ushort packageCode in zoneInPackages)
            {
                if (resendItemDefinitions)
                    itemPackages[packageCode].SendFullPackage(this);
                else
                    itemPackages[packageCode].SendPackageEnvelope(this);
            }
            equipment.SendUpdate(this);
            playerSession.QueuePacket(InventoryEndChangePacket.BuildPacket(actorId));
            #endregion

            playerSession.QueuePacket(GetInitPackets());

            List<SubPacket> areaMasterSpawn = zone.GetSpawnPackets();
            List<SubPacket> debugSpawn = world.GetDebugActor().GetSpawnPackets(this, 0);
            List<SubPacket> worldMasterSpawn = world.GetActor().GetSpawnPackets(this, 0);

            playerSession.QueuePacket(areaMasterSpawn);
            playerSession.QueuePacket(debugSpawn);
            playerSession.QueuePacket(worldMasterSpawn);

            // Nearby NPCs ride the bundle (Garlemald parity): the client's
            // 0x0007 acks can't complete until the scene's actors are
            // instantiated, so the scene mounts populated instead of waiting
            // on a post-bundle resync. Content instances scan the content
            // area's own pool by radius (base populace is structurally
            // absent per pmeteor's per-Area pool); private areas spawn their
            // WHOLE population (a radius scan drops far push-triggers like
            // the canopy exit); root zones stream the 50-yalm radius. The
            // instance list is re-seeded so continuous movement streaming
            // won't re-AddActor them.
            List<Actor> bundleActors;
            if (zone is PrivateAreaContent)
                bundleActors = zone.GetActorsAroundActor(this, 50);
            else if (zone is PrivateArea)
                bundleActors = zone.GetAllActors();
            else
            {
                bundleActors = zone.GetActorsAroundActor(this, 50);
                bundleActors.AddRange(world.GetSeamlessPartnerActorsAround(this, 50));
            }

            playerSession.ClearInstance();
            playerSession.UpdateInstance(bundleActors, true);
            DevDiagnostics.Trace(
                "zone.in.bundle.npcFanout",
                "player", customDisplayName,
                "zone", zoneId,
                "areaKind", zone == null ? "" : zone.GetType().Name,
                "bundleActorCount", bundleActors.Count,
                "instanceActorCount", playerSession.actorInstanceList.Count);

            int weatherDirectorPackets = 0;
            if (zone.GetWeatherDirector() != null)
            {
                List<SubPacket> weatherDirectorSpawn = zone.GetWeatherDirector().GetSpawnPackets();
                weatherDirectorPackets = weatherDirectorSpawn.Count;
                playerSession.QueuePacket(weatherDirectorSpawn);
                playerSession.QueuePacket(zone.GetWeatherDirector().GetInitPackets());
            }

            int ownedDirectorSpawnPackets = 0;
            int ownedDirectorInitPackets = 0;
            int ownedDirectorEventStatusPackets = 0;
            IEnumerable<Director> zoneInDirectors = ownedDirectors
                .Where(director => director.zoneId == zoneId && !director.IsDeleted());
            if (zone is PrivateAreaContent contentArea)
            {
                Director activeContentDirector = contentArea.GetContentDirector();
                zoneInDirectors = ownedDirectors.Where(director => director == activeContentDirector);
            }
            Director[] sentDirectors = zoneInDirectors.ToArray();
            foreach (Director director in sentDirectors)
            {
                List<SubPacket> directorSpawnPackets = director.GetSpawnPackets();
                List<SubPacket> directorInitPackets = director.GetInitPackets();
                List<SubPacket> directorEventStatusPackets = director.GetSetEventStatusPackets();
                ownedDirectorSpawnPackets += directorSpawnPackets.Count;
                ownedDirectorInitPackets += directorInitPackets.Count;
                ownedDirectorEventStatusPackets += directorEventStatusPackets.Count;
                QueuePackets(directorSpawnPackets);
                QueuePackets(directorInitPackets);
                QueuePackets(directorEventStatusPackets);
            }

            int npcLinkshellOwnedCount = 0;
            int npcLinkshellCallingCount = 0;
            int npcLinkshellExtraCount = 0;
            for (int i = 0; i < playerWork.npcLinkshellChatCalling.Length; i++)
            {
                bool isCalling = playerWork.npcLinkshellChatCalling[i];
                bool isExtra = playerWork.npcLinkshellChatExtra[i];
                if (isCalling || isExtra)
                    npcLinkshellOwnedCount++;
                if (isCalling)
                    npcLinkshellCallingCount++;
                if (isExtra)
                    npcLinkshellExtraCount++;
            }

            // The content-group roster trio is emitted pre-warp by
            // DoZoneChangeContent (EmitContentWarpPreWarpSequence), never in
            // the zone-in bundle — Garlemald's send_zone_in_bundle ships only
            // the party trio here.
            if (currentParty != null)
                currentParty.SendGroupPackets(playerSession);

            DevDiagnostics.Trace(
                "zone.in.packets",
                "player", customDisplayName,
                "zone", zoneId,
                "zoneActor", zone == null ? "0x0" : String.Format("0x{0:X}", zone.actorId),
                "areaKind", zone == null ? "" : zone.GetType().Name,
                "privateArea", privateArea ?? "",
                "privateAreaType", privateAreaType,
                "spawnType", spawnType,
                "inventoryRefreshMode", inventoryRefreshMode.ToString(),
                "inventoryItemDefinitionsResent", resendItemDefinitions,
                "selfSpawnPackets", selfSpawnPackets.Count,
                "areaMasterPackets", areaMasterSpawn.Count,
                "debugPackets", debugSpawn.Count,
                "worldPackets", worldMasterSpawn.Count,
                "weatherDirectorPackets", weatherDirectorPackets,
                "ownedDirectorCount", ownedDirectors.Count,
                "zoneInDirectorCount", sentDirectors.Length,
                "ownedDirectorSpawnPackets", ownedDirectorSpawnPackets,
                "ownedDirectorInitPackets", ownedDirectorInitPackets,
                "ownedDirectorEventStatusPackets", ownedDirectorEventStatusPackets,
                "npcLinkshellOwnedCount", npcLinkshellOwnedCount,
                "npcLinkshellCallingCount", npcLinkshellCallingCount,
                "npcLinkshellExtraCount", npcLinkshellExtraCount,
                "hasContentGroup", currentContentGroup != null,
                "hasParty", currentParty != null);
        }

        /// <summary>
        /// Closes a retail mass-delete transaction by listing every actor the
        /// client must keep. Opcode 0x000A carries a fixed group of 32 IDs;
        /// counted 0x0008 records carry the remainder.
        /// </summary>
        public void SendZoneInstanceSnapshot(WorldManager world)
        {
            if (world == null || zone == null)
                return;

            List<uint> actorIds = new List<uint>();
            HashSet<uint> seenActorIds = new HashSet<uint>();
            Action<uint> addActorId = id =>
            {
                if (id != 0 && seenActorIds.Add(id))
                    actorIds.Add(id);
            };

            addActorId(actorId);
            addActorId(zone.actorId);
            addActorId(world.GetDebugActor().actorId);
            addActorId(world.GetActor().actorId);

            Director weatherDirector = zone.GetWeatherDirector();
            if (weatherDirector != null)
                addActorId(weatherDirector.actorId);

            IEnumerable<Director> zoneInDirectors = ownedDirectors
                .Where(director => director.zoneId == zoneId && !director.IsDeleted());
            if (zone is PrivateAreaContent contentArea)
            {
                Director activeContentDirector = contentArea.GetContentDirector();
                zoneInDirectors = ownedDirectors.Where(director => director == activeContentDirector);
            }

            foreach (Director director in zoneInDirectors)
                addActorId(director.actorId);

            foreach (Actor actor in playerSession.actorInstanceList)
            {
                if (actor != null)
                    addActorId(actor.actorId);
            }

            QueuePacket(ServerZoneInstanceBeginPacket.BuildPacket(actorId));

            int offset = 0;
            bool sentKeepActorsX32 =
                actorIds.Count >= ServerZoneInstanceKeepActorsX32Packet.MAXIMUM_ACTORS;
            if (sentKeepActorsX32)
            {
                QueuePacket(ServerZoneInstanceKeepActorsX32Packet.BuildPacket(
                    actorId,
                    actorIds.GetRange(0, ServerZoneInstanceKeepActorsX32Packet.MAXIMUM_ACTORS)));
                offset = ServerZoneInstanceKeepActorsX32Packet.MAXIMUM_ACTORS;
            }

            int keepActorsX08ChunkCount = 0;
            for (; offset < actorIds.Count; offset += ServerZoneInstanceActorsPacket.MAXIMUM_ACTORS)
            {
                int count = Math.Min(ServerZoneInstanceActorsPacket.MAXIMUM_ACTORS, actorIds.Count - offset);
                QueuePacket(ServerZoneInstanceActorsPacket.BuildPacket(actorId, actorIds.GetRange(offset, count)));
                keepActorsX08ChunkCount++;
            }
            QueuePacket(ServerZoneInstanceEndPacket.BuildPacket(actorId));

            DevDiagnostics.Trace(
                "zone.instance.snapshot",
                "player", customDisplayName,
                "zone", zoneId,
                "zoneActor", String.Format("0x{0:X}", zone.actorId),
                "privateArea", privateArea ?? "",
                "privateAreaType", privateAreaType,
                "actorCount", actorIds.Count,
                "keepActorsX32Opcode", sentKeepActorsX32 ? "0x000A" : "",
                "keepActorsX32Count", sentKeepActorsX32
                    ? ServerZoneInstanceKeepActorsX32Packet.MAXIMUM_ACTORS
                    : 0,
                "keepActorsX08Opcode", "0x0008",
                "keepActorsX08ChunkCount", keepActorsX08ChunkCount,
                "actorIds", String.Join(",", actorIds.Select(id => String.Format("0x{0:X8}", id))));
        }

        private void SendRemoveInventoryPackets(List<ushort> slots)
        {
            int currentIndex = 0;

            while (true)
            {
                if (slots.Count - currentIndex >= 64)
                    QueuePacket(InventoryRemoveX64Packet.BuildPacket(actorId, slots, ref currentIndex));
                else if (slots.Count - currentIndex >= 32)
                    QueuePacket(InventoryRemoveX32Packet.BuildPacket(actorId, slots, ref currentIndex));
                else if (slots.Count - currentIndex >= 16)
                    QueuePacket(InventoryRemoveX16Packet.BuildPacket(actorId, slots, ref currentIndex));
                else if (slots.Count - currentIndex >= 8)
                    QueuePacket(InventoryRemoveX08Packet.BuildPacket(actorId, slots, ref currentIndex));
                else if (slots.Count - currentIndex == 1)
                    QueuePacket(InventoryRemoveX01Packet.BuildPacket(actorId, slots[currentIndex]));
                else
                    break;
            }

        }

        public bool IsMyPlayer(uint otherActorId)
        {
            return actorId == otherActorId;
        }

        public void QueuePacket(SubPacket packet)

        {
            playerSession.QueuePacket(packet);
        }

        public void QueuePackets(List<SubPacket> packets)
        {
            playerSession.QueuePacket(packets);
        }

        public void SendPacket(string path)
        {
            try
            {
                BasePacket packet = new BasePacket(path);

                packet.ReplaceActorID(actorId);
                var packets = packet.GetSubpackets();
                QueuePackets(packets);
            }
            catch (Exception e)
            {
                this.SendMessage(SendMessagePacket.MESSAGE_TYPE_SYSTEM_ERROR, "[SendPacket]", "Unable to send packet.");
                this.SendMessage(SendMessagePacket.MESSAGE_TYPE_SYSTEM_ERROR, "[SendPacket]", e.Message);
            }
        }

        public void BroadcastPackets(List<SubPacket> packets, bool sendToSelf)
        {
            foreach (SubPacket packet in packets)
            {
                if (sendToSelf)
                {

                    SubPacket clonedPacket = new SubPacket(packet, actorId);
                    QueuePacket(clonedPacket);
                }

                foreach (Actor a in playerSession.actorInstanceList)
                {
                    if (a is Player)
                    {
                        Player p = (Player)a;

                        if (p.Equals(this))
                            continue;

                        SubPacket clonedPacket = new SubPacket(packet, a.actorId);
                        p.QueuePacket(clonedPacket);
                    }
                }
            }
        }

        public void BroadcastPacket(SubPacket packet, bool sendToSelf)
        {
            if (sendToSelf)
            {
                SubPacket clonedPacket = new SubPacket(packet, actorId);
                QueuePacket(clonedPacket);
            }

            foreach (Actor a in playerSession.actorInstanceList)
            {
                if (a is Player)
                {
                    Player p = (Player)a;

                    if (p.Equals(this))
                        continue;

                    SubPacket clonedPacket = new SubPacket(packet, a.actorId);
                    p.QueuePacket(clonedPacket);
                }
            }
        }

        public void ChangeAnimation(uint animId)
        {
            Actor a = zone.FindActorInArea(currentTarget);
            if (a is Npc)
                ((Npc)a).animationId = animId;
        }

        public void SetDCFlag(bool flag)
        {
            if (flag)
            {
                BroadcastPacket(SetActorIconPacket.BuildPacket(actorId, SetActorIconPacket.DISCONNECTING), true);
            }
            else
            {
                if (isGM)
                    BroadcastPacket(SetActorIconPacket.BuildPacket(actorId, SetActorIconPacket.ISGM), true);
                else
                    BroadcastPacket(SetActorIconPacket.BuildPacket(actorId, 0), true);
            }
        }

        public void CleanupAndSave()
        {
            playerSession.LockUpdates(true);

            ClearPendingKicks("session-end");
            DetachOwnedDirectorsForSessionEnd("session-end");

            // Purge any _WAIT_EVENT-parked coroutine so a mid-cutscene
            // disconnect can't be resumed by an unrelated talk after relog
            // (Garlemald handle_session_end purge_owner — the stale
            // Charlys→Hobriaut hijack that silently drained quest counter +
            // gil + EndEvent).
            LuaEngine.GetInstance().PurgePlayerEventWaiter(this);

            // Rental state is intentionally session-scoped in 1.x. Logging out
            // (including a disconnect) ends the ride immediately.
            if (GetMountState() != 0 || IsChocoboRentalActive())
                ChocoboService.EndRide(this, false);

            //Remove actor from zone and main server list
            if (zone != null)
            {
                zone.RemoveActorFromZone(this);
            }
            else
            {
                DevDiagnostics.Trace(
                    "player.cleanup.missingZone",
                    "player", customDisplayName,
                    "zone", zoneId,
                    "privateArea", privateArea ?? "",
                    "privateAreaType", privateAreaType,
                    "destinationZone", destinationZone,
                    "spawnType", destinationSpawnType,
                    "x", positionX,
                    "y", positionY,
                    "z", positionZ,
                    "rot", rotation);
            }

            // Keep pending zone-in state if the client disconnected before confirming position.
            bool preservePendingZoneChange = IsInZoneChange() && (this.destinationZone != 0 || this.destinationSpawnType != 0);
            if (preservePendingZoneChange)
            {
                DevDiagnostics.Trace(
                    "zone.change.disconnect.pending",
                    "player", customDisplayName,
                    "zone", zoneId,
                    "privateArea", privateArea ?? "",
                    "privateAreaType", privateAreaType,
                    "destinationZone", destinationZone,
                    "spawnType", destinationSpawnType,
                    "x", positionX,
                    "y", positionY,
                    "z", positionZ,
                    "rot", rotation);
            }
            else
            {
                //Set Destination to 0
                this.destinationZone = 0;
                this.destinationSpawnType = 0;
            }

            //Clean up parties
            RemoveFromCurrentPartyAndCleanup();

            //Save Player
            Database.SavePlayerPlayTime(this);
            if (preservePendingZoneChange)
            {
                DevDiagnostics.Trace(
                    "zone.change.disconnect.positionSaveSkipped",
                    "player", customDisplayName,
                    "zone", zoneId,
                    "privateArea", privateArea ?? "",
                    "privateAreaType", privateAreaType,
                    "destinationZone", destinationZone,
                    "spawnType", destinationSpawnType,
                    "x", positionX,
                    "y", positionY,
                    "z", positionZ,
                    "rot", rotation);
            }
            else
            {
                Database.SavePlayerPosition(this);
            }
            Database.SavePlayerStatusEffects(this);
        }

        public void CleanupAndSave(uint destinationZone, ushort spawnType, float destinationX, float destinationY, float destinationZ, float destinationRot)
        {
            playerSession.LockUpdates(true);

            // A cross-map handoff reconstructs the Player on the destination
            // map. Its source-map directors must not retain this dead object;
            // parked notices would reference actors the destination never saw.
            ClearPendingKicks("map-handoff");
            DetachOwnedDirectorsForSessionEnd("map-handoff");

            //Remove actor from zone and main server list
            if (zone != null)
            {
                zone.RemoveActorFromZone(this);
            }
            else
            {
                DevDiagnostics.Trace(
                    "player.cleanupForZoneChange.missingZone",
                    "player", customDisplayName,
                    "zone", zoneId,
                    "privateArea", privateArea ?? "",
                    "privateAreaType", privateAreaType,
                    "destinationZone", destinationZone,
                    "spawnType", destinationSpawnType,
                    "x", positionX,
                    "y", positionY,
                    "z", positionZ,
                    "rot", rotation);
            }

            //Clean up parties
            RemoveFromCurrentPartyAndCleanup();

            //Set destination
            this.destinationZone = destinationZone;
            this.destinationSpawnType = spawnType;
            this.positionX = destinationX;
            this.positionY = destinationY;
            this.positionZ = destinationZ;
            this.rotation = destinationRot;

            this.statusEffects.RemoveStatusEffectsByFlags((uint)StatusEffectFlags.LoseOnZoning);

            //Save Player
            Database.SavePlayerPlayTime(this);
            Database.SavePlayerPosition(this);
            Database.SavePlayerStatusEffects(this);
        }

        public new Area GetZone()
        {
            return zone;
        }

        public void SendMessage(uint logType, string sender, string message)
        {
            QueuePacket(SendMessagePacket.BuildPacket(actorId, logType, sender, message));
        }

        //Only use at logout since it's intensive
        private byte GetInnCode()
        {
            if (zone.isInn)
            {
                Vector3 position = new Vector3(positionX, 0, positionZ);
                if (Utils.Distance(position, new Vector3(0, 0, 0)) <= 20f)
                    return 3;
                else if (Utils.Distance(position, new Vector3(160, 0, 160)) <= 20f)
                    return 2;
                else if (Utils.Distance(position, new Vector3(-160, 0, -160)) <= 20f)
                    return 1;
            }
            return 0;
        }

        public void SetSleeping()
        {
            playerSession.LockUpdates(true);
            switch(GetInnCode())
            {
                case 1:
                    positionX = -162.42f;
                    positionY = 0f;
                    positionZ = -154.21f;
                    rotation = 1.56f;
                    break;
                case 2:
                    positionX = 157.55f;
                    positionY = 0f;
                    positionZ = 165.05f;
                    rotation = 1.53f;
                    break;
                case 3:
                    positionX = -2.65f;
                    positionY = 0f;
                    positionZ = 3.94f;
                    rotation = 1.52f;
                    break;
            }
        }

        public void Logout()
        {
            EndClientSession("logout", LogoutPacket.BuildPacket(actorId));
        }

        public void QuitGame()
        {
            EndClientSession("quit", QuitPacket.BuildPacket(actorId));
        }

        private void EndClientSession(string reason, SubPacket clientTransitionPacket)
        {
            DevDiagnostics.Trace(
                "player.logout.request",
                "player", actorId,
                "playerName", customDisplayName,
                "reason", reason,
                "session", playerSession.id,
                "zone", zoneId);

            QueuePacket(clientTransitionPacket);

            // Stop accepting client gameplay immediately while the client owns
            // the terminal World-socket transition.
            Server.GetServer().BeginSessionEnd(playerSession.id);

            DevDiagnostics.Trace(
                "player.logout.packet",
                "player", actorId,
                "playerName", customDisplayName,
                "reason", reason,
                "session", playerSession.id,
                "opcode", String.Format("0x{0:X4}", clientTransitionPacket.gameMessage.opcode));

            statusEffects.RemoveStatusEffectsByFlags((uint)StatusEffectFlags.LoseOnLogout);
            CleanupAndSave();

            DevDiagnostics.Trace(
                "player.logout.cleanup",
                "player", actorId,
                "playerName", customDisplayName,
                "reason", reason,
                "session", playerSession.id);

            if (PlayerSessionTransitionPolicy.ClientOwnsWorldDisconnect(
                clientTransitionPacket.gameMessage.opcode))
            {
                DevDiagnostics.Trace(
                    "player.logout.awaitDisconnect",
                    "player", actorId,
                    "playerName", customDisplayName,
                    "reason", reason,
                    "session", playerSession.id);
                return;
            }

            // Reserved for a future transition opcode whose protocol explicitly
            // requires Map to initiate teardown. Logout and Quit never use this
            // path: after the client disconnects, World requests session end and
            // Map returns the normal confirmation through PacketProcessor.
            playerSession.QueuePacket(SessionEndConfirmPacket.BuildPacket(playerSession, 0));

            DevDiagnostics.Trace(
                "player.logout.sessionEnd",
                "player", actorId,
                "playerName", customDisplayName,
                "reason", reason,
                "session", playerSession.id,
                "destinationZone", 0);

        }

        public uint GetPlayTime(bool doUpdate)
        {
            if (doUpdate)
            {
                uint curTime = Utils.UnixTimeStampUTC();
                playTime += curTime - lastPlayTimeUpdate;
                lastPlayTimeUpdate = curTime;
            }

            return playTime;
        }

        public void SavePlayTime()
        {
            Database.SavePlayerPlayTime(this);
        }

        public void ChangeMusic(ushort musicId)
        {
            QueuePacket(SetMusicPacket.BuildPacket(actorId, musicId, 1));
        }

        public void ChangeMusic(ushort musicId, ushort trackMode)
        {
            QueuePacket(SetMusicPacket.BuildPacket(actorId, musicId, trackMode));
        }

        public void SendMountAppearance()
        {
            if (mountState == 1)
                BroadcastPacket(SetCurrentMountChocoboPacket.BuildPacket(actorId, GetRideChocoboAppearance(), rentalExpireTime, rentalMinLeft), true);
            else if (mountState == 2)
                BroadcastPacket(SetCurrentMountGoobbuePacket.BuildPacket(actorId, 1), true);
        }

        public void SetMountState(byte mountState)
        {
            this.mountState = mountState;
            SendMountAppearance();
        }

        public byte GetMountState()
        {
            return mountState;
        }

        public byte GetRideChocoboAppearance()
        {
            return chocoboRideKind == ChocoboRideKind.Rental
                ? rentalChocoboAppearance
                : chocoboAppearance;
        }

        public void DoEmote(uint targettedActor, uint animId, uint descId)
        {
            BroadcastPacket(ActorDoEmotePacket.BuildPacket(actorId, targettedActor, animId, descId), true);
        }

        public void SendGameMessage(Actor sourceActor, Actor textIdOwner, ushort textId, byte log, params object[] msgParams)
        {
            TraceGameMessage("source", sourceActor, textIdOwner, textId, log, "", 0, false, msgParams);

            if (msgParams == null || msgParams.Length == 0)
            {
                QueuePacket(GameMessagePacket.BuildPacket(Server.GetWorldManager().GetActor().actorId, sourceActor.actorId, textIdOwner.actorId, textId, log));
            }
            else
                QueuePacket(GameMessagePacket.BuildPacket(Server.GetWorldManager().GetActor().actorId, sourceActor.actorId, textIdOwner.actorId, textId, log, LuaUtils.CreateLuaParamList(msgParams)));
        }

        public void SendGameMessage(Actor textIdOwner, ushort textId, byte log, params object[] msgParams)
        {
            TraceGameMessage("default", null, textIdOwner, textId, log, "", 0, false, msgParams);

            if (msgParams == null || msgParams.Length == 0)
                QueuePacket(GameMessagePacket.BuildPacket(Server.GetWorldManager().GetActor().actorId, textIdOwner.actorId, textId, log));
            else
                QueuePacket(GameMessagePacket.BuildPacket(Server.GetWorldManager().GetActor().actorId, textIdOwner.actorId, textId, log, LuaUtils.CreateLuaParamList(msgParams)));
        }

        public void SendGameMessageCustomSender(Actor textIdOwner, ushort textId, byte log, string customSender, params object[] msgParams)
        {
            TraceGameMessage("customSender", null, textIdOwner, textId, log, customSender, 0, false, msgParams);

            if (msgParams == null || msgParams.Length == 0)
                QueuePacket(GameMessagePacket.BuildPacket(Server.GetWorldManager().GetActor().actorId, textIdOwner.actorId, textId, customSender, log));
            else
                QueuePacket(GameMessagePacket.BuildPacket(Server.GetWorldManager().GetActor().actorId, textIdOwner.actorId, textId, customSender, log, LuaUtils.CreateLuaParamList(msgParams)));
        }

        public void SendGameMessageDisplayIDSender(Actor textIdOwner, ushort textId, byte log, uint displayId, params object[] msgParams)
        {
            TraceGameMessage("displayIdSender", null, textIdOwner, textId, log, "", displayId, true, msgParams);

            if (msgParams == null || msgParams.Length == 0)
                QueuePacket(GameMessagePacket.BuildPacket(Server.GetWorldManager().GetActor().actorId, textIdOwner.actorId, textId, displayId, log));
            else
                QueuePacket(GameMessagePacket.BuildPacket(Server.GetWorldManager().GetActor().actorId, textIdOwner.actorId, textId, displayId, log, LuaUtils.CreateLuaParamList(msgParams)));
        }

        public void SendGameMessageLocalizedDisplayName(Actor textIdOwner, ushort textId, byte log, uint displayId, params object[] msgParams)
        {
            SendGameMessageDisplayIDSender(textIdOwner, textId, log, displayId, msgParams);
        }

        private static string FormatTraceActorId(Actor actor)
        {
            return actor == null ? "0x0" : String.Format("0x{0:X}", actor.actorId);
        }

        private static string FormatTraceActorName(Actor actor)
        {
            return actor == null ? "" : actor.actorName;
        }

        private static string FormatTraceMessageParams(object[] msgParams)
        {
            if (msgParams == null || msgParams.Length == 0)
                return "";

            string[] parts = new string[msgParams.Length];
            for (int i = 0; i < msgParams.Length; i++)
                parts[i] = msgParams[i] == null ? "nil" : msgParams[i].ToString();

            return String.Join(", ", parts);
        }

        private void TraceGameMessage(string mode, Actor sourceActor, Actor textIdOwner, ushort textId, byte log, string customSender, uint displayId, bool hasDisplayId, object[] msgParams)
        {
            if (!DevDiagnostics.Enabled)
                return;

            DevDiagnostics.Trace(
                "game.message",
                "player", customDisplayName,
                "mode", mode,
                "source", FormatTraceActorId(sourceActor),
                "sourceName", FormatTraceActorName(sourceActor),
                "textOwner", FormatTraceActorId(textIdOwner),
                "textOwnerName", FormatTraceActorName(textIdOwner),
                "textId", textId,
                "log", log,
                "customSender", customSender ?? "",
                "displayId", hasDisplayId ? displayId.ToString() : "",
                "params", FormatTraceMessageParams(msgParams));
        }

        public void BroadcastWorldMessage(ushort worldMasterId, params object[] msgParams)
        {
            //SubPacket worldMasterMessage = 
            //zone.BroadcastPacketAroundActor(this, worldMasterMessage);
        }

        public void GraphicChange(uint slot, uint graphicId)
        {
            appearanceIds[slot] = graphicId;           
        }

        public void GraphicChange(uint slot, uint weapId, uint equipId, uint variantId, uint colorId)
        {

            uint graphicId = EquipmentRequestPolicy.PackAppearance(
                weapId,
                equipId,
                variantId,
                colorId);

            appearanceIds[slot] = graphicId;            
            
        }

        public void GraphicChange(int slot, InventoryItem invItem, bool publish = true)
        {
            if (invItem == null)
                appearanceIds[slot] = 0;
            else
            {
                ItemData item = Server.GetItemGamedata(invItem.itemId);

                if (item is EquipmentItem)
                {
                    EquipmentItem eqItem = (EquipmentItem)item;

                    uint graphicId = EquipmentRequestPolicy.PackAppearance(
                        eqItem.graphicsWeaponId,
                        eqItem.graphicsEquipmentId,
                        eqItem.graphicsVariantId,
                        eqItem.graphicsColorId);

                    appearanceIds[slot] = graphicId;
                }

                //Handle offhand
                if (slot == MAINHAND && item is WeaponItem)
                {
                    WeaponItem wpItem = (WeaponItem)item;

                    uint graphicId =
                            (wpItem.graphicsOffhandWeaponId & 0x3FF) << 20 |
                            (wpItem.graphicsOffhandEquipmentId & 0x3FF) << 10 |
                            (wpItem.graphicsOffhandVariantId & 0x3FF);

                    if (graphicId != 0)
                        appearanceIds[SetActorAppearancePacket.OFFHAND] = graphicId;
                }

                //Handle ALC offhand special case
                if (slot == OFFHAND && item is WeaponItem && item.IsAlchemistWeapon())
                {
                    WeaponItem wpItem = (WeaponItem)item;

                    uint graphicId =
                            ((wpItem.graphicsWeaponId + 1) & 0x3FF) << 20 |
                            (wpItem.graphicsEquipmentId & 0x3FF) << 10 |
                            (wpItem.graphicsVariantId & 0x3FF);

                    if (graphicId != 0)
                        appearanceIds[SetActorAppearancePacket.SPOFFHAND] = graphicId;
                }
            }

            if (publish)
            {
                Database.SavePlayerAppearance(this);
                BroadcastPacket(CreateAppearancePacket(), true);
            }
        }

        private void RefreshEquipmentAppearance(bool publish = true)
        {
            // Reuse the existing packing/paired-weapon path, publishing once.
            for (int graphic = 5; graphic <= 26; graphic++) appearanceIds[graphic] = 0;
            GraphicChange(5, equipment.GetItemAtSlot(SLOT_MAINHAND), false);
            int[,] mapping = { {4,9},{5,10},{6,11},{8,12},{10,13},{12,14},
                {13,15},{14,16},{15,17},{16,18},{17,R_EAR},{18,L_EAR},{19,21},{20,22},
                {21,23},{22,24},{23,25},{24,26} };
            for (int i = 0; i < mapping.GetLength(0); i++)
                GraphicChange(mapping[i,1], equipment.GetItemAtSlot((ushort)mapping[i,0]), false);
            if (equipment.GetItemAtSlot(SLOT_BODY) == null)
                GraphicChange(13, equipment.GetItemAtSlot(SLOT_UNDERSHIRT), false);
            if (equipment.GetItemAtSlot(SLOT_LEGS) == null)
                GraphicChange(14, equipment.GetItemAtSlot(SLOT_UNDERGARMENT), false);
            if (equipment.GetItemAtSlot(SLOT_HANDS) == null) GraphicChange(15, 0, 1, 0, 0);
            if (equipment.GetItemAtSlot(SLOT_BOOTS) == null) GraphicChange(16, 0, 1, 0, 0);
            ItemData main = equipment.GetItemAtSlot(SLOT_MAINHAND)?.itemData;
            if (main != null)
            {
                if (main.IsCarpenterWeapon()) { GraphicChange(7,898,4,0,0); GraphicChange(8,898,4,0,0); }
                else if (main.IsBlackSmithWeapon()) { GraphicChange(7,899,1,0,0); GraphicChange(8,899,1,0,0); }
                else if (main.IsArmorerWeapon()) { GraphicChange(7,899,2,0,0); GraphicChange(8,899,2,0,0); }
                else if (main.IsGoldSmithWeapon()) { GraphicChange(6,729,1,0,0); GraphicChange(7,898,1,0,0); }
                else if (main.IsTannerWeapon()) { GraphicChange(7,898,3,0,0); GraphicChange(8,898,3,0,0); }
                else if (main.IsAlchemistWeapon()) GraphicChange(7,900,1,0,0);
                else if (main.IsCulinarianWeapon()) { GraphicChange(7,900,2,0,0); GraphicChange(8,898,2,0,0); }
            }
            InventoryItem offhand = equipment.GetItemAtSlot(SLOT_OFFHAND);
            if (offhand != null)
                GraphicChange(offhand.itemData.IsWeaverWeapon() || offhand.itemData.IsGoldSmithWeapon() ? 8 : 6, offhand, false);
            if (publish)
            {
                Database.SavePlayerAppearance(this);
                BroadcastPacket(CreateAppearancePacket(), true);
            }
        }

        public void SendAppearance()
        {
            BroadcastPacket(CreateAppearancePacket(), true);
        }

        public void SendCharaExpInfo()
        {
            if (lastStep == 0)
            {
                int maxLength;
                if ((sizeof(short) * charaWork.battleSave.skillLevel.Length)-lastPosition < 0x5E)
                    maxLength = (sizeof(short) * charaWork.battleSave.skillLevel.Length) - lastPosition;
                else
                    maxLength = 0x5E;

                byte[] skillLevelBuffer = new byte[maxLength];
                Buffer.BlockCopy(charaWork.battleSave.skillLevel, 0, skillLevelBuffer, 0, skillLevelBuffer.Length);
                SetActorPropetyPacket charaInfo1 = new SetActorPropetyPacket("charaWork/exp");

                charaInfo1.SetIsArrayMode(true);
                if (maxLength == 0x5E)
                {
                    charaInfo1.AddBuffer(Utils.MurmurHash2("charaWork.battleSave.skillLevel", 0), skillLevelBuffer, 0, skillLevelBuffer.Length, 0x0);
                    lastPosition += maxLength;
                }
                else
                {
                    charaInfo1.AddBuffer(Utils.MurmurHash2("charaWork.battleSave.skillLevel", 0), skillLevelBuffer, 0, skillLevelBuffer.Length, 0x3);
                    lastPosition = 0;
                    lastStep++;
                }

                charaInfo1.AddTarget();

                QueuePacket(charaInfo1.BuildPacket(actorId));
            }
            else if (lastStep == 1)
            {
                int maxLength;
                if ((sizeof(short) * charaWork.battleSave.skillLevelCap.Length) - lastPosition < 0x5E)
                    maxLength = (sizeof(short) * charaWork.battleSave.skillLevelCap.Length) - lastPosition;
                else
                    maxLength = 0x5E;

                byte[] skillCapBuffer = new byte[maxLength];
                Buffer.BlockCopy(charaWork.battleSave.skillLevelCap, lastPosition, skillCapBuffer, 0, skillCapBuffer.Length);
                SetActorPropetyPacket charaInfo1 = new SetActorPropetyPacket("charaWork/exp");

                
                if (maxLength == 0x5E)
                {
                    charaInfo1.SetIsArrayMode(true);
                    charaInfo1.AddBuffer(Utils.MurmurHash2("charaWork.battleSave.skillLevelCap", 0), skillCapBuffer, 0, skillCapBuffer.Length, 0x1);
                    lastPosition += maxLength;
                }
                else
                {
                    charaInfo1.SetIsArrayMode(false);
                    charaInfo1.AddBuffer(Utils.MurmurHash2("charaWork.battleSave.skillLevelCap", 0), skillCapBuffer, 0, skillCapBuffer.Length, 0x3);
                    lastStep = 0;
                    lastPosition = 0;
                }

                charaInfo1.AddTarget();

                QueuePacket(charaInfo1.BuildPacket(actorId));
            }
           
        }

        private void SendAchievedAetheryte(ushort from, ushort to)
        {
            Bitstream achievedAetheryte = new Bitstream(512, true);
            SetActorPropetyPacket update = new SetActorPropetyPacket(
                from,
                to,
                "work/achieveAetheryte");
            update.AddBitfield(
                Utils.MurmurHash2("work.event_achieve_aetheryte", 0),
                achievedAetheryte.GetSlice(from, to));
            update.AddTarget();
            QueuePacket(update.BuildPacket(actorId));
        }

        private void SendCompletedQuests(ushort from, ushort to)
        {
            SetActorPropetyPacket update = new SetActorPropetyPacket(
                from,
                to,
                "playerWork/journal");
            update.AddBitfield(
                Utils.MurmurHash2("playerWork.questScenarioComplete", 0),
                questStateManager.GetCompletionSliceBytes(from, to));
            update.AddTarget();
            QueuePacket(update.BuildPacket(actorId));
        }

        public bool OnWorkSyncRequest(string propertyName, ushort from = 0, ushort to = 0)
        {
            switch (propertyName)
            {
                case "charaWork/exp":
                    SendCharaExpInfo();
                    return true;
                case "work/achieveAetheryte":
                    SendAchievedAetheryte(from, to);
                    return true;
                case "playerWork/questCompleteS":
                    SendCompletedQuests(from, to);
                    return true;
                default:
                    return false;
            }
        }

        public int GetHighestLevel()
        {
            int max = 0;
            foreach (short level in charaWork.battleSave.skillLevel)
            {
                if (level > max)
                    max = level;
            }
            return max;
        }

        public InventoryItem[] GetGearset(ushort classId)
        {
            return Database.GetEquipment(this, classId);
        }

        public void PrepareClassChange(byte classId)
        {
            SendCharaExpInfo();
        }

        public void DoClassChange(byte classId, bool equipmentCommitted = false)
        {
            //load hotbars
            //Calculate stats
            //Calculate hp/mp

            //Get Potenciel ??????

            //Set HP/MP/TP PARAMS

            //Set mainskill and level

            //Set Parameters

            //Set current EXP

            //Set Hotbar Commands 1
            //Set Hotbar Commands 2
            //Set Hotbar Commands 3

            //Check if bonus point available... set

            //Remove buffs that fall off when changing class
            CommandResultContainer resultContainer = new CommandResultContainer();
            statusEffects.RemoveStatusEffectsByFlags((uint)StatusEffectFlags.LoseOnClassChange, resultContainer);
            resultContainer.CombineLists();
            DoBattleAction(0, 0x7c000062, resultContainer.GetList());

            if (currentJob != 0 && ConvertJobIdToClassId((byte)currentJob) != classId)
            {
                currentJob = 0;
                BroadcastPacket(SetCurrentJobPacket.BuildPacket(actorId, 0), true);
                if (!equipmentCommitted) Database.SavePlayerCurrentJob(this);
            }

            bool firstClassUse = charaWork.battleSave.skillLevel[classId - 1] <= 0;
            if (firstClassUse)
            {
                if (equipmentCommitted) charaWork.battleSave.skillLevel[classId - 1] = 1;
                else UpdateClassLevel(classId, 1);
            }

            //Set rested EXP
            charaWork.parameterSave.state_mainSkill[0] = classId;
            charaWork.parameterSave.state_mainSkillLevel = charaWork.battleSave.skillLevel[classId-1];
            playerWork.restBonusExpRate = 0.0f;
            for(int i = charaWork.commandBorder; i < charaWork.command.Length; i++)
            {
                charaWork.command[i] = 0;
                charaWork.commandCategory[i] = 0;
            }

            //If new class, init abilties and level
            if (firstClassUse)
            {
                EquipAbilitiesAtLevel(classId, 1);
            }

            ActorPropertyPacketUtil propertyBuilder = new ActorPropertyPacketUtil("charaWork/stateForAll", this);

            propertyBuilder.AddProperty("charaWork.parameterSave.state_mainSkill[0]");
            propertyBuilder.AddProperty("charaWork.parameterSave.state_mainSkillLevel");
            propertyBuilder.AddProperty(String.Format("charaWork.battleSave.skillLevel[{0}]", classId - 1));
            propertyBuilder.NewTarget("playerWork/expBonus");
            propertyBuilder.AddProperty("playerWork.restBonusExpRate");
            propertyBuilder.NewTarget("charaWork/battleStateForSelf");
            propertyBuilder.AddProperty(String.Format("charaWork.battleSave.skillPoint[{0}]", classId - 1));
            Database.LoadHotbar(this);

            var time = Utils.UnixTimeStampUTC();
            for(int i = charaWork.commandBorder; i < charaWork.command.Length; i++)
            {
                if(charaWork.command[i] != 0)
                {
                    charaWork.parameterSave.commandSlot_recastTime[i - charaWork.commandBorder] = time + charaWork.parameterTemp.maxCommandRecastTime[i - charaWork.commandBorder];
                }
            }

            UpdateHotbar();

            List<SubPacket> packets = propertyBuilder.Done();

            foreach (SubPacket packet in packets)
                BroadcastPacket(packet, true);

            if (!equipmentCommitted)
            {
                Database.SavePlayerCurrentClass(this);
                RecalculateStats("class-change");
            }
        }

        public void UpdateClassLevel(byte classId, short level)
        {
            Database.PlayerCharacterUpdateClassLevel(this, classId, level);
            charaWork.battleSave.skillLevel[classId - 1] = level;
            ActorPropertyPacketUtil propertyBuilder = new ActorPropertyPacketUtil("charaWork/stateForAll", this);
            propertyBuilder.AddProperty(String.Format("charaWork.battleSave.skillLevel[{0}]", classId-1));
            List<SubPacket> packets = propertyBuilder.Done();
            QueuePackets(packets);
        }

        public void SetRepairRequest(byte type)
        {
            charaWork.eventSave.repairType = type;
            ActorPropertyPacketUtil propPacketUtil = new ActorPropertyPacketUtil("charaWork/bazaar", this);
            propPacketUtil.AddProperty("charaWork.eventSave.repairType");
            QueuePackets(propPacketUtil.Done());
        }

        public void CheckBazaarFlags(bool noUpdate = false)
        {
            bool isDealing = false, isRepairing = false, seekingItem = false;
            lock (GetItemPackage(ItemPackage.BAZAAR))
            {
                foreach (InventoryItem item in GetItemPackage(ItemPackage.BAZAAR).GetRawList())
                {
                    if (item == null)
                        break;

                    if (item.GetBazaarMode() == InventoryItem.MODE_SELL_SINGLE || item.GetBazaarMode() == InventoryItem.MODE_SELL_PSTACK || item.GetBazaarMode() == InventoryItem.MODE_SELL_FSTACK)
                        isDealing = true;
                    if (item.GetBazaarMode() == InventoryItem.MODE_SEEK_REPAIR)
                        isRepairing = true;
                    if (item.GetBazaarMode() == InventoryItem.MODE_SEEK_ITEM)
                        isDealing = true;

                    if (isDealing && isRepairing && seekingItem)
                        break;
                }
            }

            bool doUpdate = false;

            ActorPropertyPacketUtil propPacketUtil = new ActorPropertyPacketUtil("charaWork/bazaar", this);
            if (charaWork.eventTemp.bazaarRetail != isDealing)
            {
                charaWork.eventTemp.bazaarRetail = isDealing;
                propPacketUtil.AddProperty("charaWork.eventTemp.bazaarRetail");
                doUpdate = true;
            }

            if (charaWork.eventTemp.bazaarRepair != isRepairing)
            {
                charaWork.eventTemp.bazaarRepair = isRepairing;
                propPacketUtil.AddProperty("charaWork.eventTemp.bazaarRepair");
                doUpdate = true;
            }

            if (charaWork.eventTemp.bazaarMateria != (GetItemPackage(ItemPackage.MELDREQUEST).GetCount() != 0))
            {
                charaWork.eventTemp.bazaarMateria = GetItemPackage(ItemPackage.MELDREQUEST).GetCount() != 0;
                propPacketUtil.AddProperty("charaWork.eventTemp.bazaarMateria");
                doUpdate = true;
            }
            
            if (!noUpdate && doUpdate)            
                BroadcastPackets(propPacketUtil.Done(), true);            
        }        

        private const uint GilCatalogId = 1000001;

        public int GetCurrentGil()
        {
            InventoryItem gil = GetItemPackage(ItemPackage.CURRENCY_CRYSTALS).GetItemByCatelogId(GilCatalogId);
            return gil != null ? gil.quantity : 0;
        }

        public int AddGil(int amount)
        {
            if (amount <= 0)
                return ItemPackage.ERROR_SYSTEM;

            return GetItemPackage(ItemPackage.CURRENCY_CRYSTALS).AddItem(GilCatalogId, amount, 1);
        }

        public Actor GetActorInInstance(uint actorId)
        {
            foreach (Actor a in playerSession.actorInstanceList)
            {
                if (a.actorId == actorId)
                    return a;
            }

            return null;
        }

        public void SetZoneChanging(bool flag)
        {
            isZoneChanging = flag;

            if (!flag)
                ClearExpectedZoneChangePosition();
        }

        public bool IsInZoneChange()
        {
            return isZoneChanging;
        }

        public void PrepareZoneChangePositionValidation(uint expectedZone, float expectedX, float expectedY, float expectedZ, float expectedRotation)
        {
            expectedZoneChangeZone = expectedZone;
            expectedZoneChangeX = expectedX;
            expectedZoneChangeY = expectedY;
            expectedZoneChangeZ = expectedZ;
            expectedZoneChangeRotation = expectedRotation;
            rejectedZoneChangePositionCount = 0;
            hasExpectedZoneChangePosition = true;
            awaitingZoneReadyAcknowledgement = true;
            SetZoneChanging(true);
        }

        public bool IsZoneChangePositionAcceptable(float x, float y, float z)
        {
            // During a reload the 1.x client can continue transmitting its
            // source-area coordinates after the destination actor bundle has
            // been sent. Those samples are movement, not the zone lifecycle
            // acknowledgement. Only RX 0x0007(-1) completes the transition.
            if (awaitingZoneReadyAcknowledgement)
            {
                rejectedZoneChangePositionCount++;
                if (rejectedZoneChangePositionCount == 1 || rejectedZoneChangePositionCount % 10 == 0)
                {
                    DevDiagnostics.Trace(
                        "zone.change.position.ignoredUntilReady",
                        "player", customDisplayName,
                        "zone", zoneId,
                        "expectedZone", expectedZoneChangeZone,
                        "expectedX", expectedZoneChangeX,
                        "expectedY", expectedZoneChangeY,
                        "expectedZ", expectedZoneChangeZ,
                        "expectedRot", expectedZoneChangeRotation,
                        "receivedX", x,
                        "receivedY", y,
                        "receivedZ", z,
                        "rejectedCount", rejectedZoneChangePositionCount);
                }

                return false;
            }

            bool accepted = ZoneTransitionPositionPolicy.IsDestinationConsistent(
                hasExpectedZoneChangePosition,
                zoneId,
                expectedZoneChangeZone,
                expectedZoneChangeX,
                expectedZoneChangeY,
                expectedZoneChangeZ,
                x,
                y,
                z);

            if (accepted)
                return true;

            rejectedZoneChangePositionCount++;

            // A 1.x client commonly repeats its old position while unloading.
            // Keep the trace useful without logging every duplicate packet.
            if (rejectedZoneChangePositionCount == 1 || rejectedZoneChangePositionCount % 10 == 0)
            {
                DevDiagnostics.Trace(
                    "zone.change.position.ignoredStale",
                    "player", customDisplayName,
                    "zone", zoneId,
                    "expectedZone", expectedZoneChangeZone,
                    "expectedX", expectedZoneChangeX,
                    "expectedY", expectedZoneChangeY,
                    "expectedZ", expectedZoneChangeZ,
                    "expectedRot", expectedZoneChangeRotation,
                    "receivedX", x,
                    "receivedY", y,
                    "receivedZ", z,
                    "rejectedCount", rejectedZoneChangePositionCount);
            }

            return false;
        }

        public void MarkZoneChangePending(ushort spawnType)
        {
            destinationZone = 0;
            destinationSpawnType = spawnType;
            PrepareZoneChangePositionValidation(zoneId, positionX, positionY, positionZ, rotation);

            DevDiagnostics.Trace(
                "zone.change.pending",
                "player", customDisplayName,
                "zone", zoneId,
                "privateArea", privateArea ?? "",
                "privateAreaType", privateAreaType,
                "spawnType", spawnType,
                "x", positionX,
                "y", positionY,
                "z", positionZ,
                "rot", rotation);

            Database.SavePlayerPosition(this);
        }

        public void CompleteZoneChange()
        {
            uint completedDestinationZone = destinationZone;
            ushort completedSpawnType = destinationSpawnType;
            uint completedRejectedPositionCount = rejectedZoneChangePositionCount;

            destinationZone = 0;
            destinationSpawnType = 0;
            SetZoneChanging(false);

            DevDiagnostics.Trace(
                "zone.change.complete",
                "player", customDisplayName,
                "zone", zoneId,
                "privateArea", privateArea ?? "",
                "privateAreaType", privateAreaType,
                "destinationZone", completedDestinationZone,
                "spawnType", completedSpawnType,
                "ignoredStalePositions", completedRejectedPositionCount,
                "x", positionX,
                "y", positionY,
                "z", positionZ,
                "rot", rotation);

            if (zone is PrivateAreaContent contentArea)
                contentArea.MarkContentWarpReady();

            // Re-arm the destination zone's quest ENPCs now that the client
            // finished loading and the zone-in actors are instantiated
            // (Garlemald handle_zone_in_complete tail). A sequence whose
            // onStateChange ran in the ORIGIN zone needs its talk/push enables
            // + head markers re-established here, or the destination step is
            // dead until some other re-broadcast.
            ReestablishQuestENpcs("zone-change-ack");
            Database.SavePlayerPosition(this);
        }

        private void ClearExpectedZoneChangePosition()
        {
            hasExpectedZoneChangePosition = false;
            awaitingZoneReadyAcknowledgement = false;
            expectedZoneChangeZone = 0;
            expectedZoneChangeX = 0;
            expectedZoneChangeY = 0;
            expectedZoneChangeZ = 0;
            expectedZoneChangeRotation = 0;
            rejectedZoneChangePositionCount = 0;
        }

        public ReferencedItemPackage GetEquipment()
        {
            return equipment;
        }

        public bool TryChangeEquipment(InventoryItem item, int requestedEquipPoint)
        {
            var timer = DevDiagnostics.Enabled ? System.Diagnostics.Stopwatch.StartNew() : null;
            string requestId = DevDiagnostics.Enabled ? Guid.NewGuid().ToString("N") : null;
            short previousClass = GetClass();
            int previousJob = currentJob;
            bool success = false;
            string outcome = "exception";
            DevDiagnostics.Trace("inventory.equipment.begin", "requestId", requestId,
                "actor", actorId, "equipPoint", requestedEquipPoint,
                "itemId", item?.itemId, "uniqueId", item?.uniqueId,
                "classId", previousClass, "jobId", previousJob);
            try
            {
                success = TryChangeEquipmentCore(item, requestedEquipPoint, requestId, out outcome);
                return success;
            }
            catch (Exception exception)
            {
                outcome = "exception:" + exception.GetType().Name + ":" + outcome;
                throw;
            }
            finally
            {
                DevDiagnostics.Trace("inventory.equipment.end", "requestId", requestId,
                    "actor", actorId, "success", success, "outcome", success ? "applied" : outcome,
                    "previousClass", previousClass, "classId", GetClass(),
                    "previousJob", previousJob, "jobId", currentJob,
                    "elapsedMs", timer?.Elapsed.TotalMilliseconds);
            }
        }

        private bool TryChangeEquipmentCore(InventoryItem item, int requestedEquipPoint,
            string requestId, out string outcome)
        {
            outcome = "invalid-equip-point";
            if (!IsValidEquipmentPoint(requestedEquipPoint) || requestedEquipPoint > 27) return false;
            int slot = requestedEquipPoint - 1;
            outcome = "required-slot";
            if (item == null && (slot == SLOT_MAINHAND || slot == SLOT_UNDERSHIRT || slot == SLOT_UNDERGARMENT))
                return false;

            byte targetClass = charaWork.parameterSave.state_mainSkill[0];
            if (item != null && slot == SLOT_MAINHAND)
            {
                targetClass = EquipmentRequestPolicy.WeaponClass(item.itemId);
                outcome = "unsupported-weapon-class";
                if (targetClass == 0) return false;
            }
            outcome = "item-policy-or-ownership";
            if (item != null && !CanEquipItemAtPoint(item, requestedEquipPoint, targetClass)) return false;
            InventoryItem[] previous = equipment.Snapshot();
            bool changesClass = targetClass != charaWork.parameterSave.state_mainSkill[0];
            InventoryItem[] next = changesClass ? GetGearset(targetClass) : equipment.Snapshot();
            outcome = "gearset-read-failed";
            if (next == null) return false;

            // Shared underwear belongs to class zero and survives a class swap.
            next[SLOT_UNDERSHIRT] = previous[SLOT_UNDERSHIRT];
            next[SLOT_UNDERGARMENT] = previous[SLOT_UNDERGARMENT];
            if (changesClass)
            {
                HashSet<ulong> seen = new HashSet<ulong>();
                HashSet<int> occupiedSavedPoints = new HashSet<int>();
                for (int i = 0; i < next.Length; i++)
                {
                    InventoryItem saved = next[i];
                    if (i == slot || i == SLOT_UNDERSHIRT || i == SLOT_UNDERGARMENT || saved == null) continue;
                    if (!CanEquipItemAtPoint(saved, i + 1, targetClass) || !seen.Add(saved.uniqueId))
                    {
                        next[i] = null;
                        continue;
                    }
                    int[] savedPoints = EquipmentRequestPolicy.OccupiedPoints(((EquipmentItem)saved.itemData).equipPoint, i + 1);
                    if (savedPoints.Any(occupiedSavedPoints.Contains)) next[i] = null;
                    else occupiedSavedPoints.UnionWith(savedPoints);
                }
            }
            if (item != null)
            {
                int[] occupied = EquipmentRequestPolicy.OccupiedPoints(((EquipmentItem)item.itemData).equipPoint, requestedEquipPoint);
                for (int i = 0; i < next.Length; i++)
                {
                    InventoryItem other = next[i];
                    if (i == slot || other == null) continue;
                    EquipmentItem otherData = other.itemData as EquipmentItem;
                    bool conflict = other.uniqueId == item.uniqueId || (otherData != null &&
                        EquipmentRequestPolicy.OccupiedPoints(otherData.equipPoint, i + 1).Any(occupied.Contains));
                    if (!conflict) continue;
                    // An offhand/armor request cannot remove the required main weapon.
                    outcome = "required-slot-conflict";
                    if (i == SLOT_MAINHAND || i == SLOT_UNDERSHIRT || i == SLOT_UNDERGARMENT) return false;
                    next[i] = null;
                }
            }
            next[slot] = item;
            if (DevDiagnostics.Enabled)
                DevDiagnostics.Trace("inventory.equipment.commit.begin", "requestId", requestId,
                    "actor", actorId, "targetClass", targetClass, "changesClass", changesClass,
                    "loadout", String.Join(",", next.Select((entry, index) =>
                        $"{index + 1}:{entry?.uniqueId ?? 0}:{entry?.itemId ?? 0}")));
            outcome = "database-commit-failed";
            if (!Database.CommitEquipmentChange(this, targetClass, next)) return false;
            DevDiagnostics.Trace("inventory.equipment.commit.end", "requestId", requestId,
                "actor", actorId, "targetClass", targetClass);
            outcome = "publication-exception-after-commit";
            using (DeferStatRecalculation())
            {
                equipment.ApplyCommittedList(next);
                if (changesClass) DoClassChange(targetClass, true);
                RefreshEquipmentAppearance();
                RecalculateStats("equip");
            }
            outcome = "applied";
            return true;
        }

        private bool CanEquipItemAtPoint(InventoryItem item, int point, byte classId)
        {
            EquipmentItem data = item.itemData as EquipmentItem;
            if (data == null || !ReferenceEquals(item.owner, this) || item.itemPackage != ItemPackage.NORMAL
                || !ReferenceEquals(GetItemPackage(ItemPackage.NORMAL).GetItemAtSlot(item.slot), item)) return false;
            if (!EquipmentRequestPolicy.FitsPoint(data.equipPoint, point)) return false;
            if (!EquipmentRequestPolicy.FitsTribe(data.equipTribe, playerWork.tribe)) return false;
            if (point == 1 && (!(data is WeaponItem) || EquipmentRequestPolicy.WeaponClass(item.itemId) == 0)) return false;
            return EquipmentRequestPolicy.MeetsRequiredLevel(data.levelType, data.level, Math.Max(1, (int)GetClassLevel(classId)));
        }

        public bool IsValidEquipmentPoint(int requestedEquipPoint)
        {
            return requestedEquipPoint > 0
                && requestedEquipPoint <= equipment.GetCapacity();
        }

        public InventoryItem GetValidatedEquipmentItem(
            ItemRefParam reference,
            int requestedEquipPoint,
            Type9Param itemIds)
        {
            InventoryItem item = reference == null ? null : GetItem(reference);
            EquipmentItem equipmentItem = item == null ? null : item.itemData as EquipmentItem;
            bool equippedElsewhere = false;
            bool itemTypeMatchesSlot = equipmentItem != null;

            if (requestedEquipPoint == SLOT_MAINHAND + 1)
                itemTypeMatchesSlot = equipmentItem is WeaponItem
                    && EquipmentRequestPolicy.WeaponClass(item.itemId) != 0;

            if (item != null && IsValidEquipmentPoint(requestedEquipPoint))
            {
                int requestedSlot = requestedEquipPoint - 1;
                for (ushort slot = 0; slot < equipment.GetCapacity(); slot++)
                {
                    InventoryItem equipped = equipment.GetItemAtSlot(slot);
                    if (slot != requestedSlot
                        && equipped != null
                        && equipped.uniqueId == item.uniqueId)
                    {
                        equippedElsewhere = true;
                        break;
                    }
                }
            }

            EquipmentRequestRejection rejection = EquipmentRequestPolicy.Validate(
                requestedEquipPoint,
                equipment.GetCapacity(),
                actorId,
                reference == null ? 0 : reference.actorId,
                reference == null ? ushort.MaxValue : reference.itemPackage,
                ItemPackage.NORMAL,
                reference == null ? ushort.MaxValue : reference.slot,
                item != null,
                itemIds == null ? ulong.MaxValue : itemIds.item1,
                item == null ? 0 : item.uniqueId,
                item != null && ReferenceEquals(item.owner, this),
                item == null ? ushort.MaxValue : item.itemPackage,
                item == null ? ushort.MaxValue : item.slot,
                equipmentItem != null,
                itemTypeMatchesSlot,
                true,
                equipmentItem == null ? -1 : equipmentItem.equipPoint,
                equippedElsewhere);

            if (rejection == EquipmentRequestRejection.None)
                return item;

            DevDiagnostics.Trace(
                "inventory.equipment.requestRejected",
                "player", String.Format("0x{0:X}", actorId),
                "reason", rejection,
                "requestedEquipPoint", requestedEquipPoint,
                "referenceActor", reference == null ? "" : String.Format("0x{0:X}", reference.actorId),
                "referencePackage", reference == null ? -1 : reference.itemPackage,
                "referenceSlot", reference == null ? -1 : reference.slot,
                "echoedItemId", itemIds == null ? "" : String.Format("0x{0:X16}", itemIds.item1),
                "actualItemId", item == null ? "" : String.Format("0x{0:X16}", item.uniqueId));
            return null;
        }

        public void CompleteEquipmentCommand()
        {
            // All seven retail EquipCommand requests in the equipment corpus
            // complete with the equipment substate + command result and no
            // server 0x0131 EndEvent packet.
            SubState commandSubState = new SubState();
            commandSubState.waste = EquipmentRequestPolicy.EquipSubstateWaste;
            QueuePacket(SetActorSubStatePacket.BuildPacket(actorId, commandSubState));
            BroadcastPacket(
                CommandResultX01Packet.BuildPacket(
                    actorId,
                    EquipmentRequestPolicy.EquipAnimationId,
                    EquipmentRequestPolicy.EquipCommandId,
                    new CommandResult(actorId, 0, 1)),
                true);

            currentEventOwner = 0;
            currentEventName = "";
            currentEventType = 0;
            currentEventRunning = null;
        }

        public byte GetInitialTown()
        {
            return playerWork.initialTown;
        }

        public uint GetHomePoint()
        {
            return homepoint;
        }

        public byte GetHomePointInn()
        {
            return homepointInn;
        }

        public void SetHomePoint(uint aetheryteId)
        {            
            homepoint = aetheryteId;
            Database.SavePlayerHomePoints(this);
        }

        public void SetHomePointInn(byte townId)
        {
            homepointInn = townId;
            Database.SavePlayerHomePoints(this);
        }

        public bool HasAetheryteNodeUnlocked(uint aetheryteId)
        {
            if (aetheryteId == 0)
                return false;

            return unlockedAetherytes.Contains(aetheryteId);
        }

        /// <summary>
        /// First-touch aetheryte attunement — Garlemald apply_unlock_aetheryte
        /// (#46 round 5). Pushed by AetheryteParent.lua / AetheryteChild.lua
        /// onEventStarted when HasAetheryteNodeUnlocked is false. Persists via
        /// characters_aetherytes (INSERT IGNORE) so the TeleportCommand.lua
        /// destination gate and the aetheryte menu's per-child gates survive a
        /// relog. aetheryteId is the aetheryte's actor class id (128xxxx —
        /// the characters.homepoint namespace).
        /// </summary>
        public void UnlockAetheryteNode(uint aetheryteId)
        {
            if (aetheryteId == 0)
                return;

            if (!unlockedAetherytes.Add(aetheryteId))
            {
                DevDiagnostics.Trace(
                    "aetheryte.unlock.duplicate",
                    "player", customDisplayName,
                    "aetheryteId", aetheryteId);
                return;
            }

            Database.SavePlayerAetheryte(this, aetheryteId);
            // No retail 1.x text-sheet id is mapped for the attunement line;
            // Garlemald ships the same literal-system-line path.
            SendMessage(SendMessagePacket.MESSAGE_TYPE_SYSTEM, "", "You are now attuned to the aetheryte.");
            DevDiagnostics.Trace(
                "aetheryte.unlock",
                "player", customDisplayName,
                "aetheryteId", aetheryteId,
                "unlockedCount", unlockedAetherytes.Count);
        }

        public int GetFreeQuestSlot()
        {
            for (int i = 0; i < questScenario.Length; i++)
            {
                if (questScenario[i] == null)
                    return i;
            }

            return -1;
        }

        public int GetFreeGuildleveSlot()
        {
            // The client journal reserves work.guildleveId[0..7] for regional
            // leves. Local leves mirror their compact IDs into [8..15].
            for (int i = 0; i < playerWork.questGuildleve.Length; i++)
            {
                if (work.guildleveId[i] == 0)
                    return i;
            }

            return -1;
        }

        public int GetFreeLocalGuildleveSlot()
        {
            for (int i = 0; i < playerWork.questGuildleve.Length; i++)
            {
                if (playerWork.questGuildleve[i] == 0)
                    return i;
            }

            return -1;
        }

        //For Lua calls, cause MoonSharp goes retard with uint
        public void AddQuest(int id, bool isSilent = false)
        {
            AddQuest((uint)id, isSilent);
        }       
        public void CompleteQuest(int id)
        {
            CompleteQuest((uint)id);
        }
        public bool HasQuest(int id)
        {
            return HasQuest((uint)id);
        }
        public Quest GetQuest(int id)
        {
            return GetQuest((uint)id);
        }
        public bool IsQuestCompleted(int id)
        {
            return IsQuestCompleted((uint)id);
        }
        public bool CanAcceptQuest(int id)
        {
            return CanAcceptQuest((uint)id);
        }
        public bool CanAcceptClassQuest(int id)
        {
            return CanAcceptClassQuest((uint)id);
        }
        //For Lua calls, cause MoonSharp goes retard with uint

        public bool AddGuildleve(uint id)
        {
            if (id == 0 || id > ushort.MaxValue)
            {
                TraceGuildleveAcceptance("regional", id, -1, "invalid-id");
                return false;
            }

            if (HasGuildleve(id))
            {
                TraceGuildleveAcceptance("regional", id, -1, "already-present");
                return false;
            }

            int freeSlot = GetFreeGuildleveSlot();

            if (freeSlot == -1)
            {
                TraceGuildleveAcceptance("regional", id, -1, "journal-full");
                return false;
            }

            if (!Database.SaveGuildleve(this, id, freeSlot))
            {
                TraceGuildleveAcceptance("regional", id, freeSlot, "persist-failed");
                return false;
            }

            work.ResetGuildleveSlot(freeSlot, (ushort)id);
            SendGameMessage(Server.GetWorldManager().GetActor(), 50152, 0x20, (object)id);
            SendGuildleveClientUpdate(freeSlot);
            TraceGuildleveAcceptance("regional", id, freeSlot, "accepted");
            return true;
        }

        public bool AddLocalGuildleve(uint id)
        {
            const uint localGuildleveBase = 120000;
            uint compactId = id - localGuildleveBase;

            if (id <= localGuildleveBase || compactId > ushort.MaxValue)
            {
                TraceGuildleveAcceptance("local", id, -1, "invalid-id");
                return false;
            }

            if (HasLocalGuildleve(id))
            {
                TraceGuildleveAcceptance("local", id, -1, "already-present");
                return false;
            }

            int freeSlot = GetFreeLocalGuildleveSlot();
            if (freeSlot == -1)
            {
                TraceGuildleveAcceptance("local", id, -1, "journal-full");
                return false;
            }

            if (!Database.SaveLocalGuildleve(this, id, freeSlot))
            {
                TraceGuildleveAcceptance("local", id, freeSlot, "persist-failed");
                return false;
            }

            uint actorId = 0xA0F00000 | id;
            int workSlot = freeSlot + playerWork.questGuildleve.Length;
            playerWork.questGuildleve[freeSlot] = actorId;
            questGuildleve[freeSlot] = actorId;
            work.ResetGuildleveSlot(workSlot, checked((ushort)compactId));

            SendGameMessage(Server.GetWorldManager().GetActor(), 50152, 0x20, (object)id);

            ActorPropertyPacketUtil propPacketUtil = new ActorPropertyPacketUtil("playerWork/journal", this);
            propPacketUtil.AddProperty(String.Format("playerWork.questGuildleve[{0}]", freeSlot));
            propPacketUtil.NewTarget("work/guildleve");
            propPacketUtil.AddProperty(String.Format("work.guildleveId[{0}]", workSlot));
            propPacketUtil.AddProperty(String.Format("work.guildleveDone[{0}]", workSlot));
            propPacketUtil.AddProperty(String.Format("work.guildleveChecked[{0}]", workSlot));
            QueuePackets(propPacketUtil.Done());

            TraceGuildleveAcceptance("local", id, freeSlot, "accepted");
            return true;
        }

        // The positional booleans are the client work flags, not reward-turn-in state.
        // Captures set checked at activation and done at completion; cleared requires both.
        public void MarkGuildleve(uint id, bool done, bool checkedLeve)
        {
            if (HasGuildleve(id))
            {
                for (int i = 0; i < playerWork.questGuildleve.Length; i++)
                {
                    if (work.guildleveId[i] == id)
                    {
                        if (!Database.MarkGuildleve(this, id, done, checkedLeve))
                            return;
                        work.guildleveChecked[i] = checkedLeve;
                        work.guildleveDone[i] = done;
                        SendGuildleveMarkClientUpdate(i);
                    }
                }
            }
        }

        public void RemoveGuildleve(uint id)
        {
            if (HasGuildleve(id))
            {
                for (int i = 0; i < playerWork.questGuildleve.Length; i++)
                {
                    if (work.guildleveId[i] == id)
                    {
                        if (!Database.RemoveGuildleve(this, id))
                            return;
                        work.ResetGuildleveSlot(i);
                        SendGuildleveClientUpdate(i);
                        break;
                    }
                }
            }
        }

        public void AddQuest(uint id, bool isSilent = false)
        {
            Actor actor = Server.GetStaticActors((0xA0F00000 | id));
            if (actor != null)
                AddQuest(actor.actorName, isSilent);
        }

        public void AddQuest(string name, bool isSilent = false)
        {
            Quest staticQuest = Server.GetStaticActors(name) as Quest;

            if (staticQuest == null)
                return;

            Quest quest = new Quest(this, staticQuest);
            AcceptQuest(quest, isSilent);
        }

        public bool AcceptQuest(Quest instance, bool isSilent = false)
        {
            if (instance == null || HasQuest(instance.GetQuestId()))
                return false;

            int freeSlot = GetFreeQuestSlot();
            if (freeSlot == -1)
            {
                SendGameMessage(Server.GetWorldManager().GetActor(), 25234, 0x20);
                return false;
            }

            playerWork.questScenario[freeSlot] = instance.actorId;
            questScenario[freeSlot] = instance;
            SendQuestClientUpdate(freeSlot);

            if (!isSilent)
            {
                SendDataPacket(
                    "attention",
                    Server.GetWorldManager().GetActor(),
                    "",
                    25224,
                    (object)(int)instance.GetQuestId());
                SendGameMessage(Server.GetWorldManager().GetActor(), 25224, 0x20, (object)(int)instance.GetQuestId());
            }

            instance.OnAccept();
            Database.SaveQuest(this, instance, freeSlot);
            questStateManager?.DiagnoseConsistency(
                questScenario,
                playerWork.questScenario,
                "quest-accepted");
            return true;
        }        

        public void CompleteQuest(uint id)
        {
            Actor actor = Server.GetStaticActors((0xA0F00000 | id));
            if (actor != null)
                CompleteQuest(actor.actorName);
        }

        public void CompleteQuest(string name)
        {
            Quest completed = GetQuest(name);
            if (completed != null)
                CompleteQuest(completed);
        }

        public void CompleteQuest(Quest completed)
        {
            int slot = completed == null ? -1 : GetQuestSlot(completed.GetQuestId());
            if (slot < 0)
                return;

            uint questId = completed.GetQuestId();
            if (questId >= QuestStateManager.ScenarioStart
                && questId < QuestStateManager.ScenarioStart + QuestStateManager.ScenarioCount)
            {
                playerWork.questScenarioComplete[questId - QuestStateManager.ScenarioStart] = true;
            }

            questScenario[slot] = null;
            playerWork.questScenario[slot] = 0;
            SendQuestClientUpdate(slot);

            completed.OnComplete();
            Database.CompleteQuest(this, completed.actorId);
            Database.RemoveQuest(this, completed.actorId);
            questStateManager?.UpdateQuestCompleted(completed);
            questStateManager?.DiagnoseConsistency(
                questScenario,
                playerWork.questScenario,
                "quest-completed");
            RefreshEarnedActions();
            SendGameMessage(Server.GetWorldManager().GetActor(), 25086, 0x20, (object)questId);
        }

        public bool AbandonQuest(uint id)
        {
            if (zone is PrivateArea)
            {
                SendGameMessage(Server.GetWorldManager().GetActor(), 25235, 0x20);
                return false;
            }

            Quest quest = GetQuest(id);
            if (quest == null)
                return false;
            if (quest.IsMainScenario())
            {
                SendGameMessage(Server.GetWorldManager().GetActor(), 25233, 0x20);
                return false;
            }

            int slot = GetQuestSlot(id);
            questScenario[slot] = null;
            playerWork.questScenario[slot] = 0;
            SendQuestClientUpdate(slot);
            quest.OnAbandon();
            Database.RemoveQuest(this, quest.actorId);
            questStateManager?.UpdateQuestAbandoned();
            questStateManager?.DiagnoseConsistency(
                questScenario,
                playerWork.questScenario,
                "quest-abandoned");
            SendGameMessage(this, Server.GetWorldManager().GetActor(), 25236, 0x20, (object)(int)quest.GetQuestId());
            return true;
        }

        public void RemoveQuestByQuestId(uint id)
        {
            RemoveQuest((0xA0F00000 | id));
        }

        public void RemoveQuest(uint id)
        {
            if (HasQuest(id))
            {
                for (int i = 0; i < questScenario.Length; i++)
                {
                    if (questScenario[i] != null && questScenario[i].actorId == id)
                    {
                        Database.RemoveQuest(this, questScenario[i].actorId);
                        questScenario[i].DeleteENpcState();
                        questScenario[i] = null;
                        playerWork.questScenario[i] = 0;
                        SendQuestClientUpdate(i);
                        questStateManager?.UpdateQuestAbandoned();
                        break;
                    }
                }
            }
        }

        public void ReplaceQuest(uint oldId, uint newId)
        {
            Quest oldQuest = GetQuest(oldId);
            Quest newStaticQuest = Server.GetStaticActors(0xA0F00000 | newId) as Quest;
            if (oldQuest != null && newStaticQuest != null)
                ReplaceQuest(oldQuest, newStaticQuest.actorName);
        }

        public void ReplaceQuest(Quest oldQuestInstance, string questName)
        {
            int slot = oldQuestInstance == null ? -1 : GetQuestSlot(oldQuestInstance.GetQuestId());
            Quest newStaticQuest = Server.GetStaticActors(questName) as Quest;
            if (slot < 0 || newStaticQuest == null)
                return;

            uint oldQuestId = oldQuestInstance.GetQuestId();
            if (oldQuestId >= QuestStateManager.ScenarioStart
                && oldQuestId < QuestStateManager.ScenarioStart + QuestStateManager.ScenarioCount)
            {
                playerWork.questScenarioComplete[oldQuestId - QuestStateManager.ScenarioStart] = true;
            }

            oldQuestInstance.OnComplete();
            Database.CompleteQuest(this, oldQuestInstance.actorId);
            questScenario[slot] = null;
            playerWork.questScenario[slot] = 0;
            questStateManager?.UpdateQuestCompleted(oldQuestInstance);

            Quest newQuestInstance = new Quest(this, newStaticQuest);

            questScenario[slot] = newQuestInstance;
            playerWork.questScenario[slot] = newQuestInstance.actorId;
            SendQuestClientUpdate(slot);
            newQuestInstance.OnAccept(true);
            Database.SaveQuest(this, newQuestInstance, slot);
            RefreshEarnedActions();
        }

        public bool CanAcceptQuest(string name)
        {
            if (!IsQuestCompleted(name) && !HasQuest(name))
                return true;
            else
                return false;
        }

        public bool CanAcceptQuest(uint id)
        {
            Actor actor = Server.GetStaticActors((0xA0F00000 | id));
            return actor != null && CanAcceptQuest(actor.actorName);
        }

        public bool CanAcceptClassQuest(uint id)
        {
            return ClassQuestProgressionPolicy.TryGet(id, out ClassQuestRequirement requirement)
                && CanAcceptQuest(id)
                && ClassQuestProgressionPolicy.MeetsRequirements(
                    requirement,
                    GetCurrentClassOrJob(),
                    GetClassLevel,
                    IsQuestCompleted);
        }

        public bool IsQuestCompleted(string questName)
        {
            Actor actor = Server.GetStaticActors(questName);
            return actor != null && IsQuestCompleted(actor.actorId);
        }

        public bool IsQuestCompleted(uint questId)
        {
            uint compactQuestId = 0xFFFFF & questId;
            return questStateManager != null
                ? questStateManager.IsQuestComplete(compactQuestId)
                : Database.IsQuestCompleted(this, compactQuestId);
        }

        public Quest GetQuest(uint id)
        {
            for (int i = 0; i < questScenario.Length; i++)
            {
                if (questScenario[i] != null && questScenario[i].actorId == (0xA0F00000 | id))
                    return questScenario[i];
            }

            return null;
        }

        public Quest GetQuest(string name)
        {
            for (int i = 0; i < questScenario.Length; i++)
            {
                if (questScenario[i] != null && questScenario[i].actorName.ToLower().Equals(name.ToLower()))
                    return questScenario[i];
            }

            return null;
        }

        public bool HasQuest(string name)
        {
            for (int i = 0; i < questScenario.Length; i++)
            {
                if (questScenario[i] != null && questScenario[i].actorName.ToLower().Equals(name.ToLower()))
                    return true;
            }

            return false;
        }

        public bool HasQuest(uint id)
        {
            for (int i = 0; i < questScenario.Length; i++)
            {
                if (questScenario[i] != null && questScenario[i].actorId == (0xA0F00000 | id))
                    return true;
            }

            return false;
        }

        public bool HasQuest(Quest questInstance)
        {
            return GetQuestSlot(questInstance) != -1;
        }

        public void SetQuestComplete(uint id, bool flag)
        {
            if (flag)
            {
                Quest currentQuest = GetQuest(id);
                if (currentQuest != null)
                {
                    CompleteQuest(currentQuest);
                    return;
                }
            }

            questStateManager?.ForceQuestCompleteFlag(id, flag);
            if (id >= QuestStateManager.ScenarioStart
                && id < QuestStateManager.ScenarioStart + QuestStateManager.ScenarioCount)
            {
                playerWork.questScenarioComplete[id - QuestStateManager.ScenarioStart] = flag;
            }
        }

        public Quest[] GetQuestsForNpc(Npc npc)
        {
            if (npc == null)
                return Array.Empty<Quest>();

            Quest[] quests = questScenario
                .Where(quest => quest != null && quest.IsQuestENPC(this, npc))
                .ToArray();
            Array.Sort(quests, (left, right) => left.HasData().CompareTo(right.HasData()));
            return quests;
        }

        /// <summary>
        /// Merged quest-ENPC overlay across every active quest slot for one
        /// actor class (later slots win). See QuestStateManager for the
        /// Garlemald quest_enpc_overrides contract. (#46.)
        /// </summary>
        public QuestENpc GetQuestEnpcOverlay(uint actorClassId)
        {
            QuestENpc overlay = null;
            foreach (Quest quest in questScenario)
            {
                if (quest == null)
                    continue;

                QuestENpc enpc = quest.GetQuestState().GetENpc(actorClassId);
                if (enpc != null)
                    overlay = enpc;
            }

            return overlay;
        }

        public Quest GetDefaultTalkQuest(Npc npc)
        {
            if (npc?.zone == null)
                return null;

            string questName = npc.zone.regionId switch
            {
                101 => "DftSea",
                102 => "DftRoc",
                103 => "DftFst",
                104 or 107 => "DftWil",
                105 => "DftLak",
                805 => "DftSrt",
                _ => null
            };

            Quest defaultTalk = questName == null
                ? null
                : Server.GetStaticActors(questName) as Quest;
            return defaultTalk != null && defaultTalk.IsQuestENPCByScript(this, npc)
                ? defaultTalk
                : null;
        }

        public Quest GetTutorialQuest(Npc npc)
        {
            if (npc?.zone == null
                || (npc.zone.regionId != 101 && npc.zone.regionId != 103 && npc.zone.regionId != 104))
            {
                return null;
            }

            string questName = npc.GetActorClassId() switch
            {
                1000137 => "Trl0l1",
                1000230 => "Trl0g1",
                1000841 => "Trl0u1",
                _ => null
            };
            return questName == null ? null : Server.GetStaticActors(questName) as Quest;
        }

        public void ForceQuestStateUpdate()
        {
            questStateManager?.ForceQuestStateUpdate();
        }

        public void HandleBNpcKill(uint actorClassId)
        {
            foreach (Quest quest in questScenario)
            {
                quest?.OnKillBNpc(this, actorClassId);
            }
        }

        /// <summary>
        /// Re-runs each active quest's onStateChange + ENPC diff so the
        /// destination zone's quest-set is armed after a relog or a warp where
        /// the arm ran in the origin zone (Garlemald apply_quest_update_enpcs
        /// parity — login, RX 0x0007 ack, and seamless flips).
        /// </summary>
        public void ReestablishQuestENpcs(string reason)
        {
            if (zone is PrivateAreaContent)
            {
                DevDiagnostics.Trace(
                    "quest.enpc.reestablish.skipped",
                    "player", customDisplayName,
                    "reason", reason ?? "",
                    "zone", zoneId,
                    "privateArea", privateArea ?? "",
                    "privateAreaType", privateAreaType,
                    "guard", "content-instance");
                return;
            }

            int activeQuestCount = 0;
            foreach (Quest quest in questScenario)
            {
                if (quest != null && quest.HasData())
                {
                    activeQuestCount++;
                    quest.GetQuestState().UpdateState();
                }
            }

            DevDiagnostics.Trace(
                "quest.enpc.reestablish",
                "player", customDisplayName,
                "reason", reason ?? "",
                "zone", zoneId,
                "privateArea", privateArea ?? "",
                "privateAreaType", privateAreaType,
                "activeQuestCount", activeQuestCount);
        }

        public bool HandleNpcLs(uint id)
        {
            // Garlemald handle_npc_ls_chat (#46): the clicked linkshell id is
            // a best-effort HINT only. The client speaks ZERO-BASED (it
            // mirrors the playerWork.npcLinkshellChatCalling[N] index that
            // SetNpcLs stores zero-based), while the quest stores the RAW
            // 1-based value passed to NewNpcLsMsg(from) — man0l1/man0g1
            // branch on `from == 1`. A hint H therefore matches stored H (raw)
            // or H+1 (zero-based). When the hint is absent (nil arrives as 0)
            // or does not match, fall back to the sole pending pearl (the
            // first quest whose npcLsFrom != 0), mirroring pmeteor's "the
            // linkshell window only lists pearls with a pending message"
            // click contract.
            Quest pendingFallback = null;

            foreach (Quest quest in questScenario)
            {
                if (quest == null || quest.GetNpcLsFrom() == 0)
                    continue;

                if (pendingFallback == null)
                    pendingFallback = quest;

                if (quest.GetNpcLsFrom() == id || quest.GetNpcLsFrom() == id + 1)
                {
                    DevDiagnostics.Trace(
                        "npcLinkshell.command",
                        "player", customDisplayName,
                        "hint", id,
                        "hintInterpretation", quest.GetNpcLsFrom() == id ? "one-based" : "zero-based",
                        "matchedFrom", quest.GetNpcLsFrom(),
                        "matchedQuestId", quest.GetQuestId(),
                        "matchedQuestName", quest.GetName(),
                        "matchedQuestSequence", quest.GetSequence(),
                        "matchedStep", quest.GetNpcLsMessageStep(),
                        "fallback", false);
                    quest.OnNpcLs(this);
                    return true;
                }
            }

            if (pendingFallback != null)
            {
                DevDiagnostics.Trace(
                    "npcLinkshell.command",
                    "player", customDisplayName,
                    "hint", id,
                    "hintInterpretation", "pending-quest-fallback",
                    "matchedFrom", pendingFallback.GetNpcLsFrom(),
                    "matchedQuestId", pendingFallback.GetQuestId(),
                    "matchedQuestName", pendingFallback.GetName(),
                    "matchedQuestSequence", pendingFallback.GetSequence(),
                    "matchedStep", pendingFallback.GetNpcLsMessageStep(),
                    "fallback", true);
                pendingFallback.OnNpcLs(this);
                return true;
            }

            return false;
        }

        public bool HasGuildleve(uint id)
        {
            for (int i = 0; i < playerWork.questGuildleve.Length; i++)
            {
                if (work.guildleveId[i] == id)
                    return true;
            }

            return false;
        }

        public void SetSNpc(string nickname, uint actorClassId, byte classType)
        {
            SNpcNickname = nickname;
            SNpcSkin = (byte)(actorClassId - 1070000);

            switch (SNpcSkin % 16)
            {
                case 1:
                    SNpcPersonality = 1;
                    break;
                case 2:
                case 16:
                    SNpcPersonality = 2;
                    break;
                case 3:
                case 4:
                    SNpcPersonality = 3;
                    break;
                case 5:
                case 6:
                    SNpcPersonality = 4;
                    break;
                case 7:
                case 8:
                    SNpcPersonality = 5;
                    break;
                case 9:
                case 10:
                    SNpcPersonality = 6;
                    break;
                case 11:
                case 12:
                    SNpcPersonality = 8;
                    break;
                case 13:
                case 14:
                    SNpcPersonality = 7;
                    break;
                case 15:
                    SNpcPersonality = 9;
                    break;
            }

            Database.CreateOrUpdateSNpc(this, SNpcNickname, SNpcSkin, SNpcPersonality);
        }

        public string GetSNpcNickname()
        {
            return SNpcNickname ?? "???";
        }

        public byte GetSNpcSkin()
        {
            return SNpcSkin;
        }

        public byte GetSNpcPersonality()
        {
            return SNpcPersonality;
        }

        public short GetSNpcCoordinate()
        {
            return SNpcCoordinate;
        }

        public bool HasLocalGuildleve(uint id)
        {
            uint actorId = 0xA0F00000 | id;
            for (int i = 0; i < playerWork.questGuildleve.Length; i++)
            {
                if (playerWork.questGuildleve[i] == actorId)
                    return true;
            }

            return false;
        }

        private void TraceGuildleveAcceptance(string kind, uint id, int slot, string status)
        {
            if (!DevDiagnostics.Enabled)
                return;

            DevDiagnostics.Trace(
                "guildleve.accept",
                "playerActorId", actorId,
                "player", customDisplayName,
                "kind", kind,
                "guildleveId", id,
                "slot", slot,
                "status", status);
        }

        public int GetQuestSlot(uint id)
        {
            for (int i = 0; i < questScenario.Length; i++)
            {
                if (questScenario[i] != null && questScenario[i].actorId == (0xA0F00000 | id))
                    return i;
            }

            return -1;
        }

        public int GetQuestSlot(Quest quest)
        {
            if (quest == null)
                return -1;

            for (int slot = 0; slot < questScenario.Length; slot++)
            {
                if (questScenario[slot] != null && questScenario[slot].actorId == quest.actorId)
                    return slot;
            }

            return -1;
        }

        public void SetNpcLs(uint npcLsId, uint state)
        {
            if (npcLsId < 1 || npcLsId > NPC_LINKSHELL_COUNT ||
                npcLsId > (uint)playerWork.npcLinkshellChatCalling.Length ||
                npcLsId > (uint)playerWork.npcLinkshellChatExtra.Length)
            {
                Program.Log.Error("Ignoring invalid NPC linkshell id {0} for player {1}.", npcLsId, actorId);
                return;
            }

            if (state > NPCLS_ALERT)
            {
                Program.Log.Error("Ignoring invalid NPC linkshell state {0} for player {1}.", state, actorId);
                return;
            }

            uint npcLsIndex = npcLsId - 1;
            bool wasOwned = playerWork.npcLinkshellChatCalling[npcLsIndex] ||
                            playerWork.npcLinkshellChatExtra[npcLsIndex];
            bool isCalling, isExtra;
            isCalling = isExtra = false;

            switch (state)
            {
                case NPCLS_INACTIVE:

                    if (playerWork.npcLinkshellChatExtra[npcLsIndex] == true && playerWork.npcLinkshellChatCalling[npcLsIndex] == false)
                    {
                        TraceNpcLinkshellState(npcLsId, state, playerWork.npcLinkshellChatCalling[npcLsIndex], playerWork.npcLinkshellChatExtra[npcLsIndex], true);
                        return;
                    }

                    isExtra = true;
                    break;
                case NPCLS_ACTIVE:

                    if (playerWork.npcLinkshellChatExtra[npcLsIndex] == false && playerWork.npcLinkshellChatCalling[npcLsIndex] == true)
                    {
                        TraceNpcLinkshellState(npcLsId, state, playerWork.npcLinkshellChatCalling[npcLsIndex], playerWork.npcLinkshellChatExtra[npcLsIndex], true);
                        return;
                    }

                    isCalling = true;
                    break;
                case NPCLS_ALERT:

                    if (playerWork.npcLinkshellChatExtra[npcLsIndex] == true && playerWork.npcLinkshellChatCalling[npcLsIndex] == true)
                    {
                        TraceNpcLinkshellState(npcLsId, state, playerWork.npcLinkshellChatCalling[npcLsIndex], playerWork.npcLinkshellChatExtra[npcLsIndex], true);
                        return;
                    }

                    isExtra = isCalling = true;
                    break;
            }

            playerWork.npcLinkshellChatExtra[npcLsIndex] = isExtra;
            playerWork.npcLinkshellChatCalling[npcLsIndex] = isCalling;

            Database.SaveNpcLS(this, npcLsIndex, isCalling, isExtra);

            TraceNpcLinkshellState(npcLsId, state, isCalling, isExtra, false);

            ActorPropertyPacketUtil propPacketUtil = new ActorPropertyPacketUtil("playerWork/npcLinkshellChat", this);
            propPacketUtil.AddProperty(String.Format("playerWork.npcLinkshellChatExtra[{0}]", npcLsIndex));
            propPacketUtil.AddProperty(String.Format("playerWork.npcLinkshellChatCalling[{0}]", npcLsIndex));
            QueuePackets(propPacketUtil.Done());

            // Match the legacy/original grant contract: publish the requested
            // final state once, then tell the client that the slot was acquired.
            // Calling AddNpcLs before an ALERT transition publishes an extra
            // transient INACTIVE state and makes the client rebuild this UI twice.
            if (!wasOwned && (isCalling || isExtra))
            {
                DevDiagnostics.Trace(
                    "npcLinkshell.ownershipGranted",
                    "player", customDisplayName,
                    "npcLsId", npcLsId,
                    "state", state,
                    "isCalling", isCalling,
                    "isExtra", isExtra);
                SendGameMessage(Server.GetWorldManager().GetActor(), 25118, 0x20, (object)npcLsId);
            }
        }

        public void AddNpcLs(uint npcLsId)
        {
            if (HasNpcLs(npcLsId))
                return;

            SetNpcLs(npcLsId, NPCLS_INACTIVE);
        }

        public bool HasNpcLs(uint npcLsId)
        {
            if (npcLsId < 1 || npcLsId > NPC_LINKSHELL_COUNT ||
                npcLsId > (uint)playerWork.npcLinkshellChatCalling.Length ||
                npcLsId > (uint)playerWork.npcLinkshellChatExtra.Length)
                return false;

            uint npcLsIndex = npcLsId - 1;
            return playerWork.npcLinkshellChatCalling[npcLsIndex] ||
                   playerWork.npcLinkshellChatExtra[npcLsIndex];
        }

        private void TraceNpcLinkshellState(uint npcLsId, uint state, bool isCalling, bool isExtra, bool unchanged)
        {
            if (!DevDiagnostics.Enabled)
                return;

            uint npcLsIndex = npcLsId > 0 ? npcLsId - 1 : 0;
            DevDiagnostics.Trace(
                "npcLinkshell.state",
                "player", customDisplayName,
                "actor", String.Format("0x{0:X}", actorId),
                "npcLsId", npcLsId,
                "zeroBasedIndex", npcLsIndex,
                "state", state,
                "isCalling", isCalling,
                "isExtra", isExtra,
                "storedCalling", npcLsId > 0 && npcLsIndex < playerWork.npcLinkshellChatCalling.Length
                    ? playerWork.npcLinkshellChatCalling[npcLsIndex]
                    : false,
                "storedExtra", npcLsId > 0 && npcLsIndex < playerWork.npcLinkshellChatExtra.Length
                    ? playerWork.npcLinkshellChatExtra[npcLsIndex]
                    : false,
                "unchanged", unchanged);
        }

        private void SendQuestClientUpdate(int slot)
        {
            ActorPropertyPacketUtil propPacketUtil = new ActorPropertyPacketUtil("playerWork/journal", this);
            propPacketUtil.AddProperty(String.Format("playerWork.questScenario[{0}]", slot));
            QueuePackets(propPacketUtil.Done());
        }

        private void SendGuildleveClientUpdate(int slot)
        {
            ActorPropertyPacketUtil propPacketUtil = new ActorPropertyPacketUtil("work/guildleve", this);
            propPacketUtil.AddProperty(String.Format("work.guildleveId[{0}]", slot));
            propPacketUtil.AddProperty(String.Format("work.guildleveDone[{0}]", slot));
            propPacketUtil.AddProperty(String.Format("work.guildleveChecked[{0}]", slot));
            QueuePackets(propPacketUtil.Done());
        }

        private void SendGuildleveMarkClientUpdate(int slot)
        {
            ActorPropertyPacketUtil propPacketUtil = new ActorPropertyPacketUtil("work/guildleve", this);
            propPacketUtil.AddProperty(String.Format("work.guildleveDone[{0}]", slot));
            propPacketUtil.AddProperty(String.Format("work.guildleveChecked[{0}]", slot));
            QueuePackets(propPacketUtil.Done());
        }

        public void SendStartCastbar(uint commandId, uint endTime)
        {
            playerWork.castCommandClient = commandId;
            playerWork.castEndClient = endTime;
            ActorPropertyPacketUtil propPacketUtil = new ActorPropertyPacketUtil("playerWork/castState", this);
            propPacketUtil.AddProperty("playerWork.castEndClient");
            propPacketUtil.AddProperty("playerWork.castCommandClient");
            QueuePackets(propPacketUtil.Done());
        }

        public void SendEndCastbar()
        {
            playerWork.castCommandClient = 0;
            playerWork.castEndClient = 0;
            ActorPropertyPacketUtil propPacketUtil = new ActorPropertyPacketUtil("playerWork/castState", this);
            propPacketUtil.AddProperty("playerWork.castCommandClient");
            QueuePackets(propPacketUtil.Done());
        }

        public void SetLoginDirector(Director director)
        {
            bool owned = director != null && ownedDirectors.Contains(director);
            if (owned)
            {
                loginInitDirector = director;
            }

            DevDiagnostics.Trace(
                "director.login.select",
                "player", customDisplayName,
                "playerActorId", String.Format("0x{0:X}", actorId),
                "playerZone", zoneId,
                "playerAreaKind", zone == null ? "" : zone.GetType().Name,
                "playerPrivateArea", privateArea ?? "",
                "playerPrivateAreaType", privateAreaType,
                "path", director == null ? "" : director.GetScriptPath(),
                "directorActorId", director == null ? "" : String.Format("0x{0:X}", director.actorId),
                "directorZone", director == null || director.zone == null ? 0 : director.zone.GetTerritoryId(),
                "directorAreaKind", director == null || director.zone == null ? "" : director.zone.GetType().Name,
                "owned", owned,
                "accepted", owned,
                "ownedDirectorCount", ownedDirectors.Count,
                "loginDirectorActorId", loginInitDirector == null ? "" : String.Format("0x{0:X}", loginInitDirector.actorId));
        }

        public void AddDirector(Director director, bool spawnImmediatly = false)
        {
            if (director == null)
            {
                DevDiagnostics.Trace(
                    "director.owner.add",
                    "player", customDisplayName,
                    "playerActorId", String.Format("0x{0:X}", actorId),
                    "playerZone", zoneId,
                    "playerAreaKind", zone == null ? "" : zone.GetType().Name,
                    "playerPrivateArea", privateArea ?? "",
                    "playerPrivateAreaType", privateAreaType,
                    "action", "ignored-null",
                    "ownedDirectorCount", ownedDirectors.Count);
                return;
            }

            bool added = !ownedDirectors.Contains(director);
            if (added)
            {
                ownedDirectors.Add(director);
                director.AddMember(this);
            }

            DevDiagnostics.Trace(
                "director.owner.add",
                "player", customDisplayName,
                "playerActorId", String.Format("0x{0:X}", actorId),
                "playerZone", zoneId,
                "playerAreaKind", zone == null ? "" : zone.GetType().Name,
                "playerPrivateArea", privateArea ?? "",
                "playerPrivateAreaType", privateAreaType,
                "path", director.GetScriptPath(),
                "directorActorId", String.Format("0x{0:X}", director.actorId),
                "directorZone", director.zone == null ? 0 : director.zone.GetTerritoryId(),
                "directorAreaKind", director.zone == null ? "" : director.zone.GetType().Name,
                "directorIsCreated", director.IsCreated(),
                "directorIsDeleted", director.IsDeleted(),
                "action", added ? "added" : "already-owned",
                "ownedDirectorCount", ownedDirectors.Count);
        }

        public void SendDirectorPackets(Director director)
        {
            QueuePackets(director.GetSpawnPackets());
            QueuePackets(director.GetInitPackets());
            QueuePackets(director.GetSetEventStatusPackets());
        }

        /// <summary>
        /// Retail condition re-arm after the client's type-101 notice ack.
        /// war_quest_update2 shows the server re-sending the notice conditions
        /// (SetNotice, 0x016B) after the ack: the notice owner's full
        /// 3-condition set (noticeEvent 0xE/0, noticeRequest 0/1, reqForChild
        /// 0/1) plus a per-target noticeEvent(0,1) on each quest ENPC, so the
        /// blinking icon stays armed for the next interaction instead of being
        /// consumed by the ack. Deliberately narrow: only notice conditions
        /// are re-sent — never talk/push/emote — matching retail's payload
        /// exactly (see GetNoticeEventConditionPackets).
        /// </summary>
        public void SendNoticeConditionReArm(Actor noticeOwner, Quest quest)
        {
            int ownerPackets = 0;
            if (noticeOwner != null)
            {
                List<SubPacket> ownerConditions = noticeOwner.GetNoticeEventConditionPackets();
                ownerPackets = ownerConditions.Count;
                QueuePackets(ownerConditions);
            }

            int targetPackets = 0;
            int targetCount = 0;
            if (quest != null && playerSession != null)
            {
                foreach (Actor actor in playerSession.actorInstanceList)
                {
                    if (actor is Npc npc && quest.HasENpc(npc.GetActorClassId()))
                    {
                        List<SubPacket> targetConditions = npc.GetNoticeEventConditionPackets();
                        targetPackets += targetConditions.Count;
                        targetCount++;
                        QueuePackets(targetConditions);
                    }
                }
            }

            DevDiagnostics.Trace(
                "event.notice.rearm",
                "player", customDisplayName,
                "actor", String.Format("0x{0:X}", actorId),
                "owner", noticeOwner == null ? "" : String.Format("0x{0:X}", noticeOwner.actorId),
                "ownerName", noticeOwner == null ? "" : noticeOwner.GetName(),
                "quest", quest == null ? "" : quest.GetName(),
                "ownerConditionPackets", ownerPackets,
                "targetCount", targetCount,
                "targetConditionPackets", targetPackets);
        }

        public void RemoveDirector(Director director)
        {
            if (ownedDirectors.Contains(director))
            {
                QueuePacket(RemoveActorPacket.BuildPacket(director.actorId));
                ownedDirectors.Remove(director);
                if (loginInitDirector == director)
                    loginInitDirector = null;
                director.RemoveMember(this);
            }
        }

        private void DetachOwnedDirectorsForSessionEnd(string reason)
        {
            Director[] directors = ownedDirectors.ToArray();
            loginInitDirector = null;

            // Teardown may run after the socket is already gone. Remove
            // ownership without queuing RemoveActor packets to that dead
            // client, then let the director end when its final member leaves.
            foreach (Director director in directors)
            {
                ownedDirectors.Remove(director);
                director.RemoveMember(this);
            }

            DevDiagnostics.Trace(
                "director.owner.detachAll",
                "player", customDisplayName,
                "playerActorId", String.Format("0x{0:X}", actorId),
                "reason", reason ?? "",
                "detachedCount", directors.Length,
                "remainingCount", ownedDirectors.Count);
        }
        
        public GuildleveDirector GetGuildleveDirector()
        {
            foreach (Director d in ownedDirectors)
            {
                if (d is GuildleveDirector)
                    return (GuildleveDirector)d;
            }

            return null;
        }

        public Director GetDirector(string directorName)
        {
            foreach (Director d in ownedDirectors)
            {
                if (d.GetScriptPath().Equals(directorName))                
                    return d;                
            }

            return null;
        }

        public Director GetDirector(uint id)
        {
            foreach (Director d in ownedDirectors)
            {
                if (d.actorId == id)
                    return d;
            }

            return null;
        }

        public void ExaminePlayer(Actor examinee)
        {
            Player toBeExamined;
            if (examinee is Player)
                toBeExamined = (Player)examinee;
            else
                return;

            QueuePacket(InventoryBeginChangePacket.BuildPacket(toBeExamined.actorId, true));
            toBeExamined.GetEquipment().SendUpdateAsItemPackage(this, ItemPackage.MAXSIZE_EQUIPMENT_OTHERPLAYER, ItemPackage.EQUIPMENT_OTHERPLAYER);
            QueuePacket(InventoryEndChangePacket.BuildPacket(toBeExamined.actorId));
        }        

        public void SendDataPacket(params object[] parameters)
        {
            List<LuaParam> lParams = LuaUtils.CreateLuaParamList(parameters);
            DevDiagnostics.Trace(
                "event.data",
                "player", customDisplayName,
                "actor", String.Format("0x{0:X}", actorId),
                "paramCount", lParams.Count,
                "params", LuaUtils.DumpParams(lParams));

            if (parameters != null && parameters.Length > 0 && parameters[0] is int)
            {
                int packetKind = (int)parameters[0];
                if (packetKind == 2 || packetKind == 4 || packetKind == 5 || packetKind == 7 || packetKind == 9)
                {
                    DevDiagnostics.Trace(
                        "tutorial.packet",
                        "player", customDisplayName,
                        "actor", String.Format("0x{0:X}", actorId),
                        "packetKind", packetKind,
                        "semantic", packetKind == 2 ? "success-widget"
                            : packetKind == 4 ? "open-widget"
                            : packetKind == 5 ? "close-widget"
                            : packetKind == 7 ? "end-tutorial-mode"
                            : "start-tutorial-mode",
                        "parameters", LuaUtils.DumpParams(lParams),
                        "zone", zoneId,
                        "privateArea", privateArea ?? "",
                        "privateAreaType", privateAreaType);
                }
            }

            SubPacket spacket = GenericDataPacket.BuildPacket(actorId, lParams);
            spacket.DebugPrintSubPacket();
            QueuePacket(spacket);
        }

        public void StartEvent(Actor owner, EventStartPacket start)
        {
            bool isRideCommand = owner != null && owner.GetName() == "ChocoboRideCommand";
            uint ownerActorClassId = owner is Npc ? ((Npc)owner).GetActorClassId() : 0;
            bool isChocoboStop = owner != null && ChocoboStopPolicy.CanStartWhileMounted(
                ownerActorClassId,
                owner.GetClassName(),
                start.eventName);
            if (GetMountState() != 0 && !isRideCommand && !isChocoboStop)
            {
                DevDiagnostics.Trace(
                    "event.start.blocked",
                    "reason", "mounted",
                    "player", customDisplayName,
                    "owner", String.Format("0x{0:X}", start.ownerActorID),
                    "eventName", start.eventName);
                QueuePacket(EndEventPacket.BuildPacket(actorId, start.ownerActorID, start.eventName, start.eventType));
                SendGameMessage(Server.GetWorldManager().GetActor(), 32553, 0x20);
                return;
            }

            currentEventOwner = start.ownerActorID;
            currentEventName = start.eventName;
            currentEventType = start.eventType;
            DevDiagnostics.Trace(
                "event.start",
                "player", customDisplayName,
                "actor", String.Format("0x{0:X}", actorId),
                "owner", String.Format("0x{0:X}", start.ownerActorID),
                "ownerName", owner == null ? "(none)" : owner.GetName(),
                "ownerClass", owner == null ? "" : owner.GetClassName(),
                "ownerUniqueId", owner is Npc ? ((Npc)owner).GetUniqueId() : "",
                "ownerZone", owner == null || owner.zone == null ? 0 : owner.zone.GetTerritoryId(),
                "ownerAreaKind", owner == null || owner.zone == null ? "" : owner.zone.GetType().Name,
                "ownerPrivateArea", owner == null || owner.zone == null ? "" : owner.zone.GetPrivateAreaName(),
                "ownerPrivateAreaType", owner == null || owner.zone == null ? 0 : owner.zone.GetPrivateAreaType(),
                "trigger", String.Format("0x{0:X}", start.triggerActorID),
                "eventName", start.eventName,
                "eventType", start.eventType,
                "zone", zoneId,
                "playerPrivateArea", privateArea ?? "",
                "playerPrivateAreaType", privateAreaType,
                "currentEventOwnerBefore", String.Format("0x{0:X}", currentEventOwner),
                "currentEventNameBefore", currentEventName,
                "currentEventTypeBefore", currentEventType,
                "params", LuaUtils.DumpParams(start.luaParams));

            Npc transitionNpc = owner as Npc;
            if (transitionNpc != null &&
                AnonymousTransitionActorPolicy.IsExpectedNoOp(
                    transitionNpc.GetZone() == null ? 0 : transitionNpc.GetZone().GetTerritoryId(),
                    transitionNpc.GetActorClassId(),
                    transitionNpc.GetUniqueId(),
                    start.eventName))
            {
                DevDiagnostics.Trace(
                    "event.expectedNoOp",
                    "player", customDisplayName,
                    "actor", String.Format("0x{0:X}", actorId),
                    "owner", String.Format("0x{0:X}", start.ownerActorID),
                    "ownerClassId", transitionNpc.GetActorClassId(),
                    "ownerUniqueId", transitionNpc.GetUniqueId(),
                    "zone", transitionNpc.GetZone() == null ? 0 : transitionNpc.GetZone().GetTerritoryId(),
                    "eventName", start.eventName,
                    "eventType", start.eventType,
                    "action", "end-without-populace-fallback");
                EndEvent();
                return;
            }

            // Legacy Meteor ENPC-membership routing: a quest that registers
            // this NPC as its ENPC owns the interaction and its Lua hook
            // (onTalk/onPush/onEmote) is fired directly. Only when no quest
            // claims the NPC does the event fall through to the generic
            // dispatch (child script first, base NPC script fallback).
            Npc questNpc = owner as Npc;
            if (questNpc != null)
            {
                Quest[] routeCandidates = GetQuestsForNpc(questNpc);
                if (DevDiagnostics.Enabled)
                {
                    List<string> candidateDetails = new List<string>();
                    foreach (Quest candidate in routeCandidates)
                    {
                        QuestENpc candidateEnpc = candidate.GetENpc(questNpc.GetActorClassId());
                        candidateDetails.Add(String.Format(
                            "{0}:{1}:seq={2}:flag={3}:talk={4}:push={5}:emote={6}",
                            candidate.GetQuestId(),
                            candidate.GetName(),
                            candidate.GetSequence(),
                            candidateEnpc == null ? 0 : candidateEnpc.QuestFlagType,
                            candidateEnpc != null && candidateEnpc.IsTalkEnabled,
                            candidateEnpc != null && candidateEnpc.IsPushEnabled,
                            candidateEnpc != null && candidateEnpc.IsEmoteEnabled));
                    }

                    DevDiagnostics.Trace(
                        "quest.event.route.decision",
                        "player", customDisplayName,
                        "triggerActor", String.Format("0x{0:X}", start.triggerActorID),
                        "ownerActor", String.Format("0x{0:X}", start.ownerActorID),
                        "ownerClassId", questNpc.GetActorClassId(),
                        "ownerUniqueId", questNpc.GetUniqueId(),
                        "ownerAreaKind", questNpc.zone == null ? "" : questNpc.zone.GetType().Name,
                        "ownerPrivateArea", questNpc.zone == null ? "" : questNpc.zone.GetPrivateAreaName(),
                        "ownerPrivateAreaType", questNpc.zone == null ? 0 : questNpc.zone.GetPrivateAreaType(),
                        "eventName", start.eventName,
                        "eventType", start.eventType,
                        "candidateCount", routeCandidates.Length,
                        "candidates", String.Join(",", candidateDetails),
                        "selectedRoute", routeCandidates.Length == 0 ? "npc-script" : "quest-candidate-scan");
                }

                foreach (Quest quest in questScenario)
                {
                    if (quest != null && quest.TryHandleNpcEvent(this, questNpc, start))
                    {
                        DevDiagnostics.Trace(
                            "quest.event.route.selected",
                            "player", customDisplayName,
                            "quest", quest.GetName(),
                            "questId", quest.GetQuestId(),
                            "sequence", quest.GetSequence(),
                            "npcClassId", questNpc.GetActorClassId(),
                            "npcActor", String.Format("0x{0:X}", questNpc.actorId),
                            "eventName", start.eventName,
                            "eventType", start.eventType,
                            "selectedRoute", "quest");
                        return;
                    }
                }

                DevDiagnostics.Trace(
                    "quest.event.route.selected",
                    "player", customDisplayName,
                    "npcClassId", questNpc.GetActorClassId(),
                    "npcActor", String.Format("0x{0:X}", questNpc.actorId),
                    "eventName", start.eventName,
                    "eventType", start.eventType,
                    "selectedRoute", "npc-script");
            }

            LuaEngine.GetInstance().EventStarted(this, owner, start);
        }

        public void TraceLinkpearlNoticeInvariants(string action)
        {
            int pendingCount = 0;
            List<string> pending = new List<string>();
            foreach (Quest quest in questScenario)
            {
                if (quest == null || quest.GetNpcLsFrom() == 0)
                    continue;

                pendingCount++;
                pending.Add(String.Format(
                    "{0}:seq={1}:from={2}:step={3}",
                    quest.GetQuestId(),
                    quest.GetSequence(),
                    quest.GetNpcLsFrom(),
                    quest.GetNpcLsMessageStep()));
            }

            Director director = GetDirector(currentEventOwner);
            Quest man0g1 = GetQuest(110002) ?? GetQuest(110006);
            DevDiagnostics.Trace(
                "linkpearl.notice.invariants",
                "player", customDisplayName,
                "playerActor", String.Format("0x{0:X}", actorId),
                "action", action ?? "",
                "zone", zoneId,
                "areaKind", zone == null ? "" : zone.GetType().Name,
                "privateArea", privateArea ?? "",
                "privateAreaType", privateAreaType,
                "eventOwner", String.Format("0x{0:X}", currentEventOwner),
                "eventName", currentEventName,
                "eventType", currentEventType,
                "eventOwnerResolved", director != null,
                "eventOwnerPath", director == null ? "" : director.GetScriptPath(),
                "eventOwnerCreated", director != null && director.IsCreated(),
                "eventOwnerDeleted", director != null && director.IsDeleted(),
                "ownedDirector", director != null && ownedDirectors.Contains(director),
                "questId", man0g1 == null ? 0 : man0g1.GetQuestId(),
                "questSequence", man0g1 == null ? 0 : man0g1.GetSequence(),
                "questHasMiounneEnpc", man0g1 != null && man0g1.HasENpc(1000230),
                "questHasVkorolonEnpc", man0g1 != null && man0g1.HasENpc(1000458),
                "pendingNpcLsCount", pendingCount,
                "pendingNpcLs", String.Join(",", pending),
                "instanceActorCount", playerSession == null ? 0 : playerSession.actorInstanceList.Count);
        }

        public void UpdateCutsceneState(CutsceneStatePacket packet)
        {
            if (packet == null || packet.invalidPacket)
                return;

            currentCutsceneState = packet.state;
            currentCutsceneName = packet.cutsceneName;
            currentCutsceneDetail = packet.detail;
            DevDiagnostics.Trace(
                "client.cutscene.state",
                "player", customDisplayName,
                "actor", String.Format("0x{0:X}", actorId),
                "state", currentCutsceneState,
                "cutscene", currentCutsceneName,
                "detail", String.Format("0x{0:X8}", currentCutsceneDetail));
        }

        public void UpdateEvent(EventUpdatePacket update)
        {
            if (update == null || update.invalidPacket)
            {
                DevDiagnostics.Trace(
                    "event.update.ignored",
                    "player", customDisplayName,
                    "actor", String.Format("0x{0:X}", actorId),
                    "reason", "invalid-packet");
                return;
            }

            DevDiagnostics.Trace(
                "event.update",
                "player", customDisplayName,
                "actor", String.Format("0x{0:X}", actorId),
                "trigger", String.Format("0x{0:X}", update.triggerActorID),
                "serverCodes", String.Format("0x{0:X8}", update.serverCodes),
                "unknown1", String.Format("0x{0:X8}", update.unknown1),
                "unknown2", String.Format("0x{0:X8}", update.unknown2),
                "wireStep", update.eventType,
                "owner", String.Format("0x{0:X}", currentEventOwner),
                "eventName", currentEventName,
                "eventType", currentEventType,
                "params", LuaUtils.DumpParams(update.luaParams));
            LuaEngine.GetInstance().OnEventUpdate(
                this,
                update.luaParams);
        }

        public void KickEvent(Actor actor, string eventName, params object[] parameters)
        {
            if (actor == null)
                return;

            List<LuaParam> lParams = LuaUtils.CreateLuaParamList(parameters);
            DevDiagnostics.Trace(
                "event.kick",
                "player", customDisplayName,
                "actor", String.Format("0x{0:X}", actorId),
                "playerZone", zoneId,
                "playerAreaKind", zone == null ? "" : zone.GetType().Name,
                "playerPrivateArea", privateArea ?? "",
                "playerPrivateAreaType", privateAreaType,
                "owner", String.Format("0x{0:X}", actor.actorId),
                "ownerName", actor.GetName(),
                "ownerZone", actor.zone == null ? 0 : actor.zone.GetTerritoryId(),
                "ownerAreaKind", actor.zone == null ? "" : actor.zone.GetType().Name,
                "ownerIsCreated", actor is Director && ((Director)actor).IsCreated(),
                "ownerIsDeleted", actor is Director && ((Director)actor).IsDeleted(),
                "eventName", eventName,
                "eventType", 5,
                "loginDirectorSelected", loginInitDirector == actor,
                "ownedDirector", actor is Director && ownedDirectors.Contains((Director)actor),
                "params", LuaUtils.DumpParams(lParams));
            SubPacket spacket = KickEventPacket.BuildPacket(actorId, actor.actorId, eventName, 5, lParams);
            spacket.DebugPrintSubPacket();
            QueuePacket(spacket);
        }

        /// <summary>
        /// Parks a content-director event until the client acknowledges the
        /// destination actor bootstrap. A kick sent before that acknowledgement
        /// can arrive before the director has been committed to the client.
        /// </summary>
        public void DeferContentKickEvent(Actor actor, string eventName, params object[] parameters)
        {
            if (actor == null || String.IsNullOrEmpty(eventName))
                return;

            deferredContentKickOwner = actor;
            deferredContentKickEventName = eventName;
            deferredContentKickParameters = parameters == null ? new object[0] : (object[])parameters.Clone();
            DevDiagnostics.Trace(
                "event.kick.content.deferred",
                "player", customDisplayName,
                "actor", String.Format("0x{0:X}", actorId),
                "owner", String.Format("0x{0:X}", actor.actorId),
                "ownerName", actor.GetName(),
                "eventName", eventName);
        }

        public void ReleaseDeferredContentKickEvent()
        {
            Actor owner = deferredContentKickOwner;
            string eventName = deferredContentKickEventName;
            object[] parameters = deferredContentKickParameters;

            deferredContentKickOwner = null;
            deferredContentKickEventName = null;
            deferredContentKickParameters = null;

            if (owner == null || String.IsNullOrEmpty(eventName))
                return;

            DevDiagnostics.Trace(
                "event.kick.content.release",
                "player", customDisplayName,
                "actor", String.Format("0x{0:X}", actorId),
                "owner", String.Format("0x{0:X}", owner.actorId),
                "ownerName", owner.GetName(),
                "eventName", eventName);
            KickEvent(owner, eventName, parameters ?? new object[0]);
        }

        public void ClearDeferredContentKickEvent()
        {
            deferredContentKickOwner = null;
            deferredContentKickEventName = null;
            deferredContentKickParameters = null;
        }

        /// <summary>
        /// Emits the content group's pre-warp registration sequence. Event
        /// kicks remain owned by KickEvent and are not deferred here.
        /// </summary>
        public bool EmitContentWarpPreWarpSequence(Actor contentDirector)
        {
            // The kick receiver dispatches against charaWork/currentContentGroup,
            // so Garlemald's pre-warp block emits it FIRST, session-targeted.
            // SetCurrentContentGroup broadcasts it at member-add time; this is
            // the explicit targeted send that rides the pre-warp sequence.
            if (currentContentGroup != null)
            {
                ActorPropertyPacketUtil propPacketUtil =
                    new ActorPropertyPacketUtil("charaWork/currentContentGroup", this);
                propPacketUtil.AddProperty("charaWork.currentContentGroup");
                QueuePackets(propPacketUtil.Done());

                currentContentGroup.SendGroupPackets(playerSession);
                currentContentGroup.StartAfterZoneIn();
            }

            return false;
        }

        public void ClearPendingKicks(string reason)
        {
            ClearDeferredContentKickEvent();
        }

        public void KickEventSpecial(Actor actor, uint unknown, string eventName, params object[] parameters)
        {
            if (actor == null)
                return;

            List<LuaParam> lParams = LuaUtils.CreateLuaParamList(parameters);
            DevDiagnostics.Trace(
                "event.kickSpecial",
                "player", customDisplayName,
                "actor", String.Format("0x{0:X}", actorId),
                "owner", String.Format("0x{0:X}", actor.actorId),
                "ownerName", actor.GetName(),
                "eventName", eventName,
                "eventType", 0,
                "unknown", String.Format("0x{0:X}", unknown),
                "params", LuaUtils.DumpParams(lParams));
            SubPacket spacket = KickEventPacket.BuildPacket(actorId, actor.actorId, eventName, 0, lParams);
            spacket.DebugPrintSubPacket();
            QueuePacket(spacket);
        }

        public void SetEventStatus(Actor actor, string conditionName, bool enabled, byte type)
        {
            if (actor == null)
                return;

            DevDiagnostics.Trace(
                "event.status",
                "player", customDisplayName,
                "playerActor", String.Format("0x{0:X}", actorId),
                "actor", String.Format("0x{0:X}", actor.actorId),
                "actorName", actor.GetName(),
                "condition", conditionName ?? "",
                "enabled", enabled,
                "type", type,
                "result", "queued");

            QueuePacket(SetEventStatusPacket.BuildPacket(actor.actorId, enabled, type, conditionName));
        }       

        public void RunEventFunction(string functionName, params object[] parameters)
        {
            List<LuaParam> lParams =
                LuaUtils.CreateRunEventFunctionParamList(
                    functionName,
                    parameters);
            bool isLinkpearlTutorialDispatch = parameters != null
                && parameters.Any(parameter =>
                    parameter is string
                    && String.Equals(
                        (string)parameter,
                        "processEventTu_001",
                        StringComparison.Ordinal));
            if (isLinkpearlTutorialDispatch)
                TraceLinkpearlNoticeInvariants("before-processEventTu_001");

            DevDiagnostics.Trace(
                "event.runFunction",
                "player", customDisplayName,
                "actor", String.Format("0x{0:X}", actorId),
                "owner", String.Format("0x{0:X}", currentEventOwner),
                "eventName", currentEventName,
                "eventType", currentEventType,
                "envelopeSize",
                    RunEventFunctionPacket.GetPacketSize(currentEventType),
                "function", functionName,
                "delegatedFunction", isLinkpearlTutorialDispatch ? "processEventTu_001" : "",
                "params", LuaUtils.DumpParams(lParams));
            SubPacket spacket = RunEventFunctionPacket.BuildPacket(
                actorId,
                currentEventOwner,
                currentEventName,
                currentEventType,
                functionName,
                lParams);
            spacket.DebugPrintSubPacket();
            QueuePacket(spacket);
            if (isLinkpearlTutorialDispatch)
                TraceLinkpearlNoticeInvariants("after-processEventTu_001-queued");
        }

        public void EndEvent()
        {
            uint endingOwner = currentEventOwner;
            string endingName = currentEventName;
            byte endingType = currentEventType;
            SubPacket p = EndEventPacket.BuildPacket(
                actorId,
                endingOwner,
                endingName,
                endingType);
            DevDiagnostics.Trace(
                "event.end.beforeClear",
                "player", customDisplayName,
                "actor", String.Format("0x{0:X}", actorId),
                "owner", String.Format("0x{0:X}", endingOwner),
                "eventName", endingName,
                "eventType", endingType,
                "packetOpcode", String.Format("0x{0:X4}", p.gameMessage.opcode),
                "packetSize", p.header.subpacketSize);
            p.DebugPrintSubPacket();
            QueuePacket(p);

            currentEventOwner = 0;
            currentEventName = "";
            currentEventType = 0;
            currentEventRunning = null;

            DevDiagnostics.Trace(
                "event.end.afterClear",
                "player", customDisplayName,
                "actor", String.Format("0x{0:X}", actorId),
                "owner", String.Format("0x{0:X}", currentEventOwner),
                "eventName", currentEventName,
                "eventType", currentEventType);
            if (String.Equals(endingName, "noticeEvent", StringComparison.Ordinal)
                || endingType == 5)
                TraceLinkpearlNoticeInvariants("after-event-end");
        }



        public void BroadcastCountdown(byte countdownLength, ulong syncTime)
        {
            BroadcastPacket(StartCountdownPacket.BuildPacket(actorId, countdownLength, syncTime, "Go!"), true);
        }
        
        public void SendInstanceUpdate(bool force = false)
        {
            Server.GetWorldManager().SeamlessCheck(this);

            //Update Instance
            List<Actor> aroundMe = new List<Actor>();

            if (zone != null)
                aroundMe.AddRange(zone.GetActorsAroundActor(this, 50));
            if (zone2 != null)
                aroundMe.AddRange(zone2.GetActorsAroundActor(this, 50));

            DevDiagnostics.Trace(
                "session.instance.update",
                "player", customDisplayName,
                "force", force,
                "updatesLocked", playerSession.isUpdatesLocked,
                "zone", zoneId,
                "zoneActor", zone == null ? "0x0" : String.Format("0x{0:X}", zone.actorId),
                "areaKind", zone == null ? "" : zone.GetType().Name,
                "privateArea", privateArea ?? "",
                "privateAreaType", privateAreaType,
                "zoneActorCount", zone == null ? 0 : zone.GetActorCount(),
                "nearbyActorCount", aroundMe.Count,
                "instanceActorCountBefore", playerSession.actorInstanceList.Count);
            playerSession.UpdateInstance(aroundMe, force);
            DevDiagnostics.Trace(
                "session.instance.update.done",
                "player", customDisplayName,
                "force", force,
                "instanceActorCountAfter", playerSession.actorInstanceList.Count);
        }

        public string GetPrivateAreaName()
        {
            return privateArea ?? "";
        }

        public bool IsInParty()
        {
            return currentParty != null;
        }

        public bool IsPartyLeader()
        {
            if (IsInParty())
            {
                Party party = (Party)currentParty;
                return party.GetLeader() == actorId;
            }
            else
                return false;
        }

        public void PartyOustPlayer(uint actorId)
        {
            SubPacket oustPacket = PartyModifyPacket.BuildPacket(playerSession, 1, actorId);
            QueuePacket(oustPacket);
        }

        public void PartyOustPlayer(string name)
        {
            SubPacket oustPacket = PartyModifyPacket.BuildPacket(playerSession, 1, name);
            QueuePacket(oustPacket);
        }

        //Legacy script name (PartyDisbandCommand.lua calls player:PartyKickPlayer(name)).
        //Same semantics as PartyOustPlayer: the leader ousts the named member.
        public void PartyKickPlayer(string name)
        {
            PartyOustPlayer(name);
        }

        public void PartyLeave()
        {
            SubPacket leavePacket = PartyLeavePacket.BuildPacket(playerSession, false);
            QueuePacket(leavePacket);
        }

        public void PartyDisband()
        {
            SubPacket disbandPacket = PartyLeavePacket.BuildPacket(playerSession, true);
            QueuePacket(disbandPacket);
        }

        public void PartyPromote(uint actorId)
        {
            SubPacket promotePacket = PartyModifyPacket.BuildPacket(playerSession, 0, actorId);
            QueuePacket(promotePacket);
        }

        public void PartyPromote(string name)
        {
            SubPacket promotePacket = PartyModifyPacket.BuildPacket(playerSession, 0, name);
            QueuePacket(promotePacket);
        }

        //A party member list packet came, set the party
        public void SetParty(Party group)
        {
            if (group is Party && currentParty != group)
            {
                RemoveFromCurrentPartyAndCleanup();
                currentParty = group;
            }
        }

        //Removes the player from the party and cleans it up if needed
        public void RemoveFromCurrentPartyAndCleanup()
        {
            if (currentParty == null)
                return;

            Party partyGroup = (Party) currentParty;
            partyGroup.SendDeletePacket(playerSession);

            DevDiagnostics.Trace(
                "party.player.leave",
                "player", customDisplayName,
                "actor", String.Format("0x{0:X}", actorId),
                "group", partyGroup.groupIndex,
                "membersBefore", partyGroup.members.Count);

            partyGroup.RemoveMember(actorId);

            bool hasSessionMember = false;
            for (int i = 0; i < partyGroup.members.Count; i++)
            {
                if (Server.GetServer().GetSession(partyGroup.members[i]) != null)
                {
                    hasSessionMember = true;
                    break;
                }
            }

            if (!hasSessionMember)
                Server.GetWorldManager().NoMembersInParty(partyGroup);

            currentParty = null;
        }
        
        public void ChangeChocoboAppearance(byte appearanceId)
        {
            Database.ChangePlayerChocoboAppearance(this, appearanceId);
            chocoboAppearance = appearanceId;
        }
        
        public bool IsChocoboRentalActive()
        {
            return rentalExpireTime != 0;
        }

        public int GetNpcRepairQuoteResult(LuaUtils.ItemRefParam reference)
        {
            return (int)RepairService.QuoteItem(this, reference).result;
        }

        public int GetNpcRepairCandidateCount()
        {
            return RepairService.GetCandidateCount(this);
        }

        public LuaUtils.ItemRefParam GetNpcRepairCandidate(int candidateIndex)
        {
            return RepairService.GetCandidate(this, candidateIndex);
        }

        public int GetNpcRepairFee(LuaUtils.ItemRefParam reference)
        {
            return RepairService.QuoteItem(this, reference).fee;
        }

        public uint GetNpcRepairItemId(LuaUtils.ItemRefParam reference)
        {
            return RepairService.QuoteItem(this, reference).itemId;
        }

        public int TryNpcRepair(LuaUtils.ItemRefParam reference, uint expectedItemId, int expectedFee)
        {
            return (int)RepairService.TryRepairItem(this, reference, expectedItemId, expectedFee);
        }

        public bool CanRentChocobo()
        {
            return ChocoboPolicy.IsRentalLevelEligible(GetHighestLevel()) && GetCurrentGil() >= ChocoboPolicy.RentalPrice;
        }

        public int GetCurrentGrandCompanyRank()
        {
            return ChocoboPolicy.GetRank(this, gcCurrent);
        }

        public int TryPurchaseChocoboIssuance(uint companyShopActorClassId, uint itemId, int price)
        {
            return (int)GrandCompanyShopService.TryPurchaseChocoboIssuance(
                this,
                companyShopActorClassId,
                itemId,
                price);
        }

        public bool CanPresentChocoboIssuance(uint stablemasterActorClassId)
        {
            if (!ChocoboPolicy.TryGetStablemaster(stablemasterActorClassId, out StablemasterPolicy stablemaster))
                return false;
            return !hasChocobo
                && gcCurrent == stablemaster.grandCompany
                && ChocoboPolicy.IsPrivateThirdClassOrHigher(
                    ChocoboPolicy.GetRank(this, stablemaster.grandCompany))
                && HasItem(stablemaster.issuanceItemId);
        }

        public int TryIssuePersonalChocobo(uint stablemasterActorClassId, string name)
        {
            return (int)ChocoboService.TryIssuePersonal(this, stablemasterActorClassId, name);
        }

        public int TryStartChocoboRental(uint stablemasterActorClassId)
        {
            return (int)ChocoboService.TryStartRental(this, stablemasterActorClassId);
        }

        public int TryStartStablemasterChocoboRide(uint stablemasterActorClassId)
        {
            if (!ChocoboPolicy.TryGetStablemaster(stablemasterActorClassId, out StablemasterPolicy unused))
                return (int)ChocoboResult.InvalidStablemaster;
            return (int)ChocoboService.TryMountPersonal(this, true);
        }

        public int TryMountPersonalChocobo()
        {
            return (int)ChocoboService.TryMountPersonal(this, false);
        }

        public void EndChocoboRide()
        {
            ChocoboService.EndRide(this);
        }

        public Retainer SpawnMyRetainer(Npc bell, int retainerIndex)
        {
            Retainer retainer = Database.LoadRetainer(this, retainerIndex);

            float distance = (float)Math.Sqrt(((positionX - bell.positionX) * (positionX - bell.positionX)) + ((positionZ - bell.positionZ) * (positionZ - bell.positionZ)));
            float posX = bell.positionX - ((-1.0f * (bell.positionX - positionX)) / distance);
            float posZ = bell.positionZ - ((-1.0f * (bell.positionZ - positionZ)) / distance);

            retainer.positionX = posX;
            retainer.positionY = positionY;
            retainer.positionZ = posZ;
            retainer.rotation = (float)Math.Atan2(positionX - posX, positionZ - posZ);

            retainerMeetingGroup = Server.GetWorldManager()
                .CreateRetainerMeetingRelationGroup(this, retainer);
            retainerMeetingGroup.SendGroupPackets(playerSession);

            currentSpawnedRetainer = retainer;
            sentRetainerSpawn = false;

            return retainer;
        }

        public void DespawnMyRetainer()
        {
            if (currentSpawnedRetainer != null)
            {
                Retainer despawnedRetainer = currentSpawnedRetainer;
                currentSpawnedRetainer = null;
                retainerMeetingGroup.SendDeletePacket(playerSession);
                retainerMeetingGroup = null;
                despawnedRetainer.GetZone().ReleaseTransientActorNumber(
                    despawnedRetainer.actorId);
            }
        }
        
        public override void Update(DateTime tick)
        {
            
            // Chocobo Rental Expirey
            if (rentalExpireTime != 0)
            {
                uint tickUTC = Utils.UnixTimeStampUTC(tick);

                //Rental has expired, dismount
                if (rentalExpireTime <= tickUTC)
                {
                    ChocoboService.EndRide(this);
                }
                else
                {
                    rentalMinLeft = (byte) ((rentalExpireTime - tickUTC) /60);
                }
            }

            aiContainer.Update(tick);
            statusEffects.Update(tick);
            questStateManager?.Update(tick);
            // A parked notice that no zone change consumed in the same
            // synchronous script stretch dispatches now (Garlemald
            // burst-scoped capture semantics on the Meteor inline engine).
        }

        public override void PostUpdate(DateTime tick, List<SubPacket> packets = null)
        {
            // todo: is this correct?
            if (this.playerSession == null)
            {
                DevDiagnostics.Trace(
                    "player.postUpdate.missingSession",
                    "player", customDisplayName,
                    "actor", String.Format("0x{0:X}", actorId),
                    "updateFlags", updateFlags.ToString());
                return;
            }

            if (this.playerSession.isUpdatesLocked)
                return;           

            // todo: should probably add another flag for battleTemp since all this uses reflection
            packets = new List<SubPacket>();

            // we only want the latest update for the player
            if ((updateFlags & ActorUpdateFlags.Position) != 0)
            {
                if (positionUpdates != null && positionUpdates.Count > 1)
                {
                    var latestPosition = positionUpdates[positionUpdates.Count - 1];
                    positionUpdates.Clear();
                    positionUpdates.Add(latestPosition);
                }
            }

            if ((updateFlags & ActorUpdateFlags.HpTpMp) != 0)
            {
                var propPacketUtil = new ActorPropertyPacketUtil("charaWork/stateAtQuicklyForAll", this);

                // todo: should this be using job as index?
                propPacketUtil.AddProperty("charaWork.parameterSave.hp[0]");
                propPacketUtil.AddProperty("charaWork.parameterSave.hpMax[0]");
                propPacketUtil.AddProperty("charaWork.parameterSave.state_mainSkill[0]");
                propPacketUtil.AddProperty("charaWork.parameterSave.state_mainSkillLevel");

                packets.AddRange(propPacketUtil.Done());
            }


            if ((updateFlags & ActorUpdateFlags.Stats) != 0)
            {
                var propPacketUtil = new ActorPropertyPacketUtil("charaWork/battleParameter", this);

                for (uint i = 0; i < 35; i++)
                {
                    if (GetMod(i) != charaWork.battleTemp.generalParameter[i])
                    {
                        charaWork.battleTemp.generalParameter[i] = (short)GetMod(i);
                        propPacketUtil.AddProperty(String.Format("charaWork.battleTemp.generalParameter[{0}]", i));
                    }
                }

                QueuePackets(propPacketUtil.Done());
            }

            if ((updateFlags & ActorUpdateFlags.Hotbar) != 0)
            {
                UpdateHotbar(hotbarSlotsToUpdate);
                hotbarSlotsToUpdate.Clear();

                updateFlags ^= ActorUpdateFlags.Hotbar;
            }
            
            base.PostUpdate(tick, packets);
        }

        public override void Die(DateTime tick, CommandResultContainer actionContainer = null)
        {
            // todo: death timer
            aiContainer.InternalDie(tick, 60);
        }

        //Update commands and recast timers for the entire hotbar
        public void UpdateHotbar()
        {
            for (ushort i = charaWork.commandBorder; i < charaWork.commandBorder + 30; i++)
            {
                hotbarSlotsToUpdate.Add(i);
            }
            updateFlags |= ActorUpdateFlags.Hotbar;
        }

        //Updates the hotbar and recast timers for only certain hotbar slots
        public void UpdateHotbar(List<ushort> slotsToUpdate)
        {
            UpdateHotbarCommands(slotsToUpdate);
            UpdateRecastTimers(slotsToUpdate);
        }

        //Update command ids for the passed in hotbar slots
        public void UpdateHotbarCommands(List<ushort> slotsToUpdate)
        {
            ActorPropertyPacketUtil propPacketUtil = new ActorPropertyPacketUtil("charaWork/command", this);
            foreach (ushort slot in slotsToUpdate)
            {
                propPacketUtil.AddProperty(String.Format("charaWork.command[{0}]", slot));
                propPacketUtil.AddProperty(String.Format("charaWork.commandCategory[{0}]", slot));
            }

            propPacketUtil.NewTarget("charaWork/commandDetailForSelf");
            //Enable or disable slots based on whether there is an ability in that slot
            foreach (ushort slot in slotsToUpdate)
            {
                charaWork.parameterSave.commandSlot_compatibility[slot - charaWork.commandBorder] = charaWork.command[slot] != 0;
                propPacketUtil.AddProperty(String.Format("charaWork.parameterSave.commandSlot_compatibility[{0}]", slot - charaWork.commandBorder));
            }

            QueuePackets(propPacketUtil.Done());
            //QueuePackets(compatibiltyUtil.Done());
        }

        //Update recast timers for the passed in hotbar slots
        public void UpdateRecastTimers(List<ushort> slotsToUpdate)
        {
            ActorPropertyPacketUtil recastPacketUtil = new ActorPropertyPacketUtil("charaWork/commandDetailForSelf", this);

            foreach (ushort slot in slotsToUpdate)
            {
                recastPacketUtil.AddProperty(String.Format("charaWork.parameterTemp.maxCommandRecastTime[{0}]", slot - charaWork.commandBorder));
                recastPacketUtil.AddProperty(String.Format("charaWork.parameterSave.commandSlot_recastTime[{0}]", slot - charaWork.commandBorder));
            }

            QueuePackets(recastPacketUtil.Done());
        }

        //Find the first open slot in classId's hotbar and equip an ability there.
        public void EquipAbilityInFirstOpenSlot(byte classId, uint commandId, bool printMessage = true)
        {
            //Find first open slot on class's hotbar slot, then call EquipAbility with that slot.
            ushort hotbarSlot = 0;

            //If the class we're equipping for is the current class, we can just look at charawork.command
            if(classId == charaWork.parameterSave.state_mainSkill[0])
                hotbarSlot = FindFirstCommandSlotById(0);
            //Otherwise, we need to check the database.
            else
                hotbarSlot = (ushort) (Database.FindFirstCommandSlot(this, classId) + charaWork.commandBorder);

            EquipAbility(classId, commandId, hotbarSlot, printMessage);
        }

        //Add commandId to classId's hotbar at hotbarSlot.
        //If classId is not the current class, do it in the database
        //hotbarSlot starts at 32
        public void EquipAbility(byte classId, uint commandId, ushort hotbarSlot, bool printMessage = true)
        {
            var ability = Server.GetWorldManager().GetBattleCommand(commandId);
            uint trueCommandId = 0xA0F00000 | commandId;
            ushort lowHotbarSlot = (ushort)(hotbarSlot - charaWork.commandBorder);
            ushort maxRecastTime = (ushort)(ability != null ? ability.maxRecastTimeSeconds : 5);
            uint recastEnd = Utils.UnixTimeStampUTC() + maxRecastTime;
            
            Database.EquipAbility(this, classId, (ushort) (hotbarSlot - charaWork.commandBorder), commandId, recastEnd);
            //If the class we're equipping for is the current class (need to find out if state_mainSkill is supposed to change when you're a job)
            //then equip the ability in charawork.commands and save in databse, otherwise just save in database
            if (classId == GetCurrentClassOrJob())
            {
                charaWork.command[hotbarSlot] = trueCommandId;
                charaWork.commandCategory[hotbarSlot] = 1;
                charaWork.parameterTemp.maxCommandRecastTime[lowHotbarSlot] = maxRecastTime;
                charaWork.parameterSave.commandSlot_recastTime[lowHotbarSlot] = recastEnd;

                hotbarSlotsToUpdate.Add(hotbarSlot);
                updateFlags |= ActorUpdateFlags.Hotbar;
            }


            if(printMessage)
                SendGameMessage(Server.GetWorldManager().GetActor(), 30603, 0x20, 0, commandId);
        }

        //Doesn't take a classId because the only way to swap abilities is through the ability equip widget oe /eaction, which only apply to current class
        //hotbarSlot 1 and 2 are 32-indexed.
        public void SwapAbilities(ushort hotbarSlot1, ushort hotbarSlot2)
        {
            //0 indexed hotbar slots for saving to database and recast timers
            uint lowHotbarSlot1 = (ushort)(hotbarSlot1 - charaWork.commandBorder);
            uint lowHotbarSlot2 = (ushort)(hotbarSlot2 - charaWork.commandBorder);
            
            //Store information about first command
            uint commandId = charaWork.command[hotbarSlot1];
            uint recastEnd = charaWork.parameterSave.commandSlot_recastTime[lowHotbarSlot1];
            ushort recastMax = charaWork.parameterTemp.maxCommandRecastTime[lowHotbarSlot1];

            //Move second command's info to first hotbar slot
            charaWork.command[hotbarSlot1] = charaWork.command[hotbarSlot2];
            charaWork.parameterTemp.maxCommandRecastTime[lowHotbarSlot1] = charaWork.parameterTemp.maxCommandRecastTime[lowHotbarSlot2];
            charaWork.parameterSave.commandSlot_recastTime[lowHotbarSlot1] = charaWork.parameterSave.commandSlot_recastTime[lowHotbarSlot2];

            //Move first command's info to second slot
            charaWork.command[hotbarSlot2] = commandId;
            charaWork.parameterTemp.maxCommandRecastTime[lowHotbarSlot2] = recastMax;
            charaWork.parameterSave.commandSlot_recastTime[lowHotbarSlot2] = recastEnd;

            //Save changes to both slots
            Database.EquipAbility(this, GetCurrentClassOrJob(), (ushort)(lowHotbarSlot1), 0xA0F00000 ^ charaWork.command[hotbarSlot1], charaWork.parameterSave.commandSlot_recastTime[lowHotbarSlot1]);
            Database.EquipAbility(this, GetCurrentClassOrJob(), (ushort)(lowHotbarSlot2), 0xA0F00000 ^ charaWork.command[hotbarSlot2], charaWork.parameterSave.commandSlot_recastTime[lowHotbarSlot2]);

            //Update slots on client
            hotbarSlotsToUpdate.Add(hotbarSlot1);
            hotbarSlotsToUpdate.Add(hotbarSlot2);
            updateFlags |= ActorUpdateFlags.Hotbar;
        }

        public void UnequipAbility(ushort hotbarSlot, bool printMessage = true)
        {
            ushort trueHotbarSlot = (ushort)(hotbarSlot + charaWork.commandBorder - 1);
            uint commandId = charaWork.command[trueHotbarSlot];
            Database.UnequipAbility(this,  hotbarSlot);
            charaWork.command[trueHotbarSlot] = 0;
            hotbarSlotsToUpdate.Add(trueHotbarSlot);

            if (printMessage && commandId != 0)
                SendGameMessage(Server.GetWorldManager().GetActor(), 30604, 0x20, 0, 0xA0F00000 ^ commandId);

            updateFlags |= ActorUpdateFlags.Hotbar;
        }

        //Finds the first hotbar slot with a given commandId.
        //If the returned value is outside the hotbar, it indicates it wasn't found.
        public ushort FindFirstCommandSlotById(uint commandId)
        {
            if(commandId != 0)
                commandId |= 0xA0F00000;

            ushort firstSlot = (ushort)(charaWork.commandBorder + 30);

            for (ushort i = charaWork.commandBorder; i < charaWork.commandBorder + 30; i++)
            {
                if (charaWork.command[i] == commandId)
                {
                    firstSlot = i;
                    break;
                }
            }

            return firstSlot;
        }
        
        private void UpdateHotbarTimer(uint commandId, uint recastTimeMs)
        {
            ushort slot = FindFirstCommandSlotById(commandId);
            charaWork.parameterSave.commandSlot_recastTime[slot - charaWork.commandBorder] = Utils.UnixTimeStampUTC(DateTime.Now.AddMilliseconds(recastTimeMs));
            var slots = new List<ushort>();
            slots.Add(slot);
            UpdateRecastTimers(slots);
        }

        private uint GetHotbarTimer(uint commandId)
        {
            ushort slot = FindFirstCommandSlotById(commandId);
            return charaWork.parameterSave.commandSlot_recastTime[slot - charaWork.commandBorder];
        }

        public override void Cast(uint spellId, uint targetId = 0)
        {
            if (aiContainer.CanChangeState())
                aiContainer.Cast(zone.FindActorInArea<Character>(targetId == 0 ? currentTarget : targetId), spellId);
            else if (aiContainer.IsCurrentState<MagicState>())
                // You are already casting.
                SendGameMessage(Server.GetWorldManager().GetActor(), 32536, 0x20);
            else
                // Please wait a moment and try again.
                SendGameMessage(Server.GetWorldManager().GetActor(), 32535, 0x20);
        }

        public override void Ability(uint abilityId, uint targetId = 0)
        {
            if (aiContainer.CanChangeState())
                aiContainer.Ability(zone.FindActorInArea<Character>(targetId == 0 ? currentTarget : targetId), abilityId);
            else
                // Please wait a moment and try again.
                SendGameMessage(Server.GetWorldManager().GetActor(), 32535, 0x20);
        }

        public override void WeaponSkill(uint skillId, uint targetId = 0)
        {
            if (aiContainer.CanChangeState())
                aiContainer.WeaponSkill(zone.FindActorInArea<Character>(targetId == 0 ? currentTarget : targetId), skillId);
            else
                // Please wait a moment and try again.
                SendGameMessage(Server.GetWorldManager().GetActor(), 32535, 0x20);
        }

        public override bool IsValidTarget(Character target, ValidTarget validTarget)
        {
            if (target == null)
            {
                // Target does not exist.
                SendGameMessage(Server.GetWorldManager().GetActor(), 32511, 0x20);
                return false;
            }

            if (target.isMovingToSpawn)
            {
                // A player may resume the closed Gridania tutorial encounter
                // after an older server process or reconnect left a wolf on
                // the generic world-mob return path. Clear the stale path;
                // the attack/retaliation flow will establish combat normally.
                if (target is BattleNpc tutorialNpc &&
                    GridaniaOpeningTutorialPolicy.IsLiveContentCombat(tutorialNpc, this))
                {
                    tutorialNpc.isMovingToSpawn = false;
                    tutorialNpc.aiContainer.pathFind.Clear();
                    DevDiagnostics.Trace(
                        "battle.target.returnCancelled",
                        "player", String.Format("0x{0:X}", actorId),
                        "target", String.Format("0x{0:X}", tutorialNpc.actorId),
                        "content", GridaniaOpeningTutorialPolicy.ContentAreaName);
                }
                else
                {
                    // That command cannot be performed on the current target.
                    SendGameMessage(Server.GetWorldManager().GetActor(), 32547, 0x20);
                    return false;
                }
            }

            // enemy only
            if ((validTarget & ValidTarget.Enemy) != 0)
            {
                if (!target.CanBeAttackedBy(this))
                {
                    // That command cannot be performed on the current target.
                    SendGameMessage(Server.GetWorldManager().GetActor(), 32547, 0x20);
                    DevDiagnostics.Trace(
                        "battle.target.blocked",
                        "reason", "target is not attackable",
                        "actor", String.Format("0x{0:X}", actorId),
                        "actorName", customDisplayName != null ? customDisplayName : actorName,
                        "target", String.Format("0x{0:X}", target.actorId),
                        "targetName", target.customDisplayName != null ? target.customDisplayName : target.actorName,
                        "targetType", target.GetType().Name);
                    return false;
                }

                // todo: this seems ambiguous
                if (target.isStatic)
                {
                    // That command cannot be performed on the current target.
                    SendGameMessage(Server.GetWorldManager().GetActor(), 32547, 0x20);
                    return false;
                }
                if (currentParty != null && target.currentParty == currentParty)
                {
                    // That command cannot be performed on a party member.
                    SendGameMessage(Server.GetWorldManager().GetActor(), 32548, 0x20);
                    return false;
                }
                // todo: pvp?
                if (target.allegiance == allegiance)
                {
                    // That command cannot be performed on an ally.
                    SendGameMessage(Server.GetWorldManager().GetActor(), 32549, 0x20);
                    return false;
                }

                bool partyEngaged = false;
                // todo: replace with confrontation status effect? (see how dsp does it)
                if (target.aiContainer.IsEngaged())
                {
                    if (currentParty != null)
                    {
                        if (target is BattleNpc)
                        {
                            var helpingActorId = ((BattleNpc)target).GetMobMod((uint)MobModifier.CallForHelp);
                            partyEngaged = this.actorId == helpingActorId || (((BattleNpc)target).GetMobMod((uint)MobModifier.FreeForAll) != 0);
                        }

                        if (!partyEngaged)
                        {
                            foreach (var memberId in ((Party)currentParty).members)
                            {
                                if (memberId == target.currentLockedTarget)
                                {
                                    partyEngaged = true;
                                    break;
                                }
                            }
                        }
                    }
                    else if (target.currentLockedTarget == actorId)
                    {
                        partyEngaged = true;
                    }
                }
                else
                {
                    partyEngaged = true;
                }

                if (!partyEngaged)
                {
                    // That target is already engaged.
                    SendGameMessage(Server.GetWorldManager().GetActor(), 32520, 0x20);
                    return false;
                }
            }

            if ((validTarget & ValidTarget.Ally) != 0 && target.allegiance != allegiance)
            {
                // That command cannot be performed on the current target.
                SendGameMessage(Server.GetWorldManager().GetActor(), 32547, 0x20);
                return false;
            }

            // todo: isStatic seems ambiguous?
            if ((validTarget & ValidTarget.NPC) != 0 && target.isStatic)
                return true;

            // todo: why is player always zoning?
            // cant target if zoning
            if (target is Player && ((Player)target).playerSession.isUpdatesLocked)
            {
                // That command cannot be performed on the current target.
                SendGameMessage(Server.GetWorldManager().GetActor(), 32547, 0x20);
                return false;
            }

            return true;
        }

        //Do we need separate functions? they check the same things
        public override bool CanUse(Character target, BattleCommand skill, CommandResult error = null)
        {
            if (!skill.IsValidMainTarget(this, target, error) || !IsValidTarget(target, skill.mainTarget))
            {
                // error packet is set in IsValidTarget
                return false;
            }

            //Might want to do these with a BattleAction instead to be consistent with the rest of command stuff
            if (GetHotbarTimer(skill.id) > Utils.UnixTimeStampUTC())
            {
                // todo: this needs confirming
                // Please wait a moment and try again.
                error?.SetTextId(32535);
                return false;
            }

            float xzDistance = Utils.XZDistance(positionX, positionZ, target.positionX, target.positionZ);
            if (xzDistance > skill.range)
            {
                DevDiagnostics.Trace(
                    "battle.command.blocked",
                    "reason", "out_of_range",
                    "player", String.Format("0x{0:X}", actorId),
                    "playerName", customDisplayName != null ? customDisplayName : actorName,
                    "target", String.Format("0x{0:X}", target.actorId),
                    "targetName", target.customDisplayName != null ? target.customDisplayName : target.actorName,
                    "commandId", skill.id,
                    "commandName", skill.name,
                    "distance", xzDistance,
                    "range", skill.range,
                    "playerX", positionX,
                    "playerY", positionY,
                    "playerZ", positionZ,
                    "targetX", target.positionX,
                    "targetY", target.positionY,
                    "targetZ", target.positionZ,
                    "queuedPositions", positionUpdates == null ? 0 : positionUpdates.Count);

                // The target is too far away.
                error?.SetTextId(32539);
                return false;
            }

            if (xzDistance < skill.minRange)
            {
                // The target is too close.
                error?.SetTextId(32538);
                return false;
            }

            if (target.positionY - positionY > (skill.rangeHeight / 2))
            {
                // The target is too far above you.
                error?.SetTextId(32540);
                return false;
            }

            if (positionY - target.positionY > (skill.rangeHeight / 2))
            {
                // The target is too far below you.
                error?.SetTextId(32541);
                return false;
            }

            if (skill.CalculateMpCost(this) > GetMP())
            {
                // You do not have enough MP.
                error?.SetTextId(32545);
                return false;
            }

            if (skill.CalculateTpCost(this) > GetTP())
            {
                // You do not have enough TP.
                error?.SetTextId(32546);
                return false;
            }

            //Proc requirement
            if (skill.procRequirement != BattleCommandProcRequirement.None && !charaWork.battleTemp.timingCommandFlag[(int)skill.procRequirement - 1])
            {
                //Conditions for use are not met
                error?.SetTextId(32556);
                return false;
            }


            return true;
        }

        public override void OnAttack(State state, CommandResult action, ref CommandResult error)
        {
            var target = state.GetTarget();

            base.OnAttack(state, action, ref error);

            // todo: switch based on main weap (also probably move this anim assignment somewhere else)
            action.animation = 0x19001000;
            if (error == null)
            {
                // melee attack animation
                //action.animation = 0x19001000;
            }
            if (target is BattleNpc)
            {
                ((BattleNpc)target).hateContainer.UpdateHate(this, action.enmity);
            }

            EmitContentProgressSignal("playerAttack");
        }

        public override void OnCast(State state, CommandResult[] actions, BattleCommand spell, ref CommandResult[] errors)
        {
            // todo: update hotbar timers to skill's recast time (also needs to be done on class change or equip crap)
            base.OnCast(state, actions, spell, ref errors);
            // todo: should just make a thing that updates the one slot cause this is dumb as hell            
            UpdateHotbarTimer(spell.id, spell.recastTimeMs);
            //LuaEngine.GetInstance().OnSignal("spellUse");
        }

        public override void OnWeaponSkill(State state, CommandResult[] actions, BattleCommand skill, ref CommandResult[] errors)
        {
            // todo: update hotbar timers to skill's recast time (also needs to be done on class change or equip crap)
            base.OnWeaponSkill(state, actions, skill, ref errors);

            // todo: should just make a thing that updates the one slot cause this is dumb as hell
            UpdateHotbarTimer(skill.id, skill.recastTimeMs);
            // todo: this really shouldnt be called on each ws?
            lua.LuaEngine.CallLuaBattleFunction(this, "onWeaponSkill", this, state.GetTarget(), skill);
            LuaEngine.GetInstance().OnSignal("weaponskillUse");
        }

        public override void OnAbility(State state, CommandResult[] actions, BattleCommand ability, ref CommandResult[] errors)
        {
            base.OnAbility(state, actions, ability, ref errors);
            UpdateHotbarTimer(ability.id, ability.recastTimeMs);
            LuaEngine.GetInstance().OnSignal("abilityUse");
            LuaEngine.GetInstance().OnSignal("abilityUsed");
        }

        //Handles exp being added, does not handle figuring out exp bonus from buffs or skill/link chains or any of that
        //Returns CommandResults that can be sent to display the EXP gained number and level ups
        //exp should be a ushort single the exp graphic overflows after ~65k
        public List<CommandResult> AddExp(int exp, byte classId, byte bonusPercent = 0)
        {
            List<CommandResult> actionList = new List<CommandResult>();
            int originalExp = exp;
            short startingLevel = GetLevel();
            int startingClassExp = charaWork.battleSave.skillPoint[classId - 1];

            exp += (int) Math.Ceiling((exp * bonusPercent / 100.0f));
            int awardedExp = exp;

            DevDiagnostics.Trace(
                "player.exp.grant.begin",
                "player", String.Format("0x{0:X}", actorId),
                "playerName", customDisplayName != null ? customDisplayName : actorName,
                "classId", classId,
                "currentClassId", GetClass(),
                "level", startingLevel,
                "oldExp", startingClassExp,
                "baseExp", originalExp,
                "bonusPercent", bonusPercent,
                "finalExp", awardedExp);

            //You earn [exp] (+[bonusPercent]%) experience points.
            //In non-english languages there are unique messages for each language, hence the use of ClassExperienceTextIds
            actionList.Add(new CommandResult(actorId, BattleUtils.ClassExperienceTextIds[classId], 0, (ushort)exp, bonusPercent));

            bool leveled = false;
            int diff = MAXEXP[GetLevel() - 1] - charaWork.battleSave.skillPoint[classId - 1];            
            //While there is enough experience to level up, keep leveling up, unlocking skills and removing experience from exp until we don't have enough to level up
            while (exp >= diff && GetLevel() < charaWork.battleSave.skillLevelCap[classId])
            {
                //Level up
                LevelUp(classId, actionList);
                leveled = true;
                //Reduce exp based on how much exp is needed to level
                exp -= diff;
                diff = MAXEXP[GetLevel() - 1];
            }

            if(leveled)
            {
                //Set exp to current class to 0 so that exp is added correctly
                charaWork.battleSave.skillPoint[classId - 1] = 0;
                //send new level
                ActorPropertyPacketUtil levelPropertyPacket = new ActorPropertyPacketUtil("charaWork/stateForAll", this);
                levelPropertyPacket.AddProperty(String.Format("charaWork.battleSave.skillLevel[{0}]", classId - 1));
                levelPropertyPacket.AddProperty("charaWork.parameterSave.state_mainSkillLevel");
                QueuePackets(levelPropertyPacket.Done());

                Database.SetLevel(this, classId, GetLevel());
                Database.SavePlayerCurrentClass(this);
            }
            //Cap experience for level 50
            charaWork.battleSave.skillPoint[classId - 1] = Math.Min(charaWork.battleSave.skillPoint[classId - 1] + exp, MAXEXP[GetLevel() - 1]);

            ActorPropertyPacketUtil expPropertyPacket = new ActorPropertyPacketUtil("charaWork/battleStateForSelf", this);
            expPropertyPacket.AddProperty(String.Format("charaWork.battleSave.skillPoint[{0}]", classId - 1));
            
            QueuePackets(expPropertyPacket.Done());
            Database.SetExp(this, classId, charaWork.battleSave.skillPoint[classId - 1]);

            DevDiagnostics.Trace(
                "player.exp.grant.end",
                "player", String.Format("0x{0:X}", actorId),
                "playerName", customDisplayName != null ? customDisplayName : actorName,
                "classId", classId,
                "currentClassId", GetClass(),
                "oldLevel", startingLevel,
                "newLevel", GetLevel(),
                "oldExp", startingClassExp,
                "newExp", charaWork.battleSave.skillPoint[classId - 1],
                "baseExp", originalExp,
                "bonusPercent", bonusPercent,
                "finalExp", awardedExp,
                "remainderApplied", exp,
                "leveled", leveled);

            return actionList;
        }

        //Equips any abilities for the given classId at the given level. If actionList is not null, adds a "You learn Command" message
        private void EquipAbilitiesAtLevel(byte classId, short level, List<CommandResult> actionList = null)
        {
            // Reconcile all earned levels, including characters advanced by GM/database tools.
            RefreshEarnedActions();
            if (actionList != null && ConvertJobIdToClassId(GetCurrentClassOrJob()) == classId)
                foreach (ushort commandId in Server.GetWorldManager().GetBattleCommandIdByLevel(classId, level))
                    actionList.Add(new CommandResult(actorId, 33926, 0, commandId));
        }

        public bool HasLearnedBattleCommand(uint commandId)
        {
            var ability = Server.GetWorldManager().GetBattleCommand(commandId);
            if (ability == null) return false;
            byte baseClass = ConvertJobIdToClassId(ability.job);
            if (!IsDiscipleOfWarOrMagicClass(baseClass)) return false;
            if (baseClass == ability.job) return GetClassLevel(baseClass) >= ability.level;
            if (GetCurrentClassOrJob() != ability.job) return false;
            bool hasSoul = JobProgressionPolicy.TryGetForBaseClass(baseClass, out var job)
                && HasItem(job.SoulCrystalItemId)
                && JobProgressionPolicy.MeetsLevelRequirements(job, GetClassLevel);
            return AbilityUnlockPolicy.GetEligibleActions(ability.job, GetClassLevel(baseClass),
                hasSoul, IsQuestCompleted, Server.GetWorldManager().GetBattleCommandIdByLevel)
                .Contains((ushort)commandId);
        }

        private void RefreshEarnedActions(bool preserveActiveRecasts = true)
        {
            Database.LoadHotbar(this, preserveActiveRecasts);
            for (ushort slot = charaWork.commandBorder; slot < charaWork.commandBorder + AbilityUnlockPolicy.HotbarCapacity; slot++)
                hotbarSlotsToUpdate.Add(slot);
            updateFlags |= ActorUpdateFlags.Hotbar;
        }

        //Increaess level of current class and equips new abilities earned at that level
        public void LevelUp(byte classId, List<CommandResult> actionList = null)
        {
            if (charaWork.battleSave.skillLevel[classId - 1] < charaWork.battleSave.skillLevelCap[classId])
            {
                short oldLevel = charaWork.battleSave.skillLevel[classId - 1];

                //Increase level
                charaWork.battleSave.skillLevel[classId - 1]++;
                charaWork.parameterSave.state_mainSkillLevel++;
                short newLevel = charaWork.battleSave.skillLevel[classId - 1];

                DevDiagnostics.Trace(
                    "player.level.up",
                    "player", String.Format("0x{0:X}", actorId),
                    "playerName", customDisplayName != null ? customDisplayName : actorName,
                    "classId", classId,
                    "currentClassId", GetClass(),
                    "oldLevel", oldLevel,
                    "newLevel", newLevel);

                //33909: You attain level [level].
                if (actionList != null)
                    actionList.Add(new CommandResult(actorId, 33909, 0, (ushort)newLevel));

                EquipAbilitiesAtLevel(classId, newLevel, actionList);

                if (classId == GetClass())
                    RecalculateStats("level-up");

                questStateManager?.UpdateLevel(GetHighestLevel());
            }
        }

        public void SetClassAttributeAllocation(PlayerClassAttributeAllocation allocation)
        {
            if (allocation == null)
                return;

            classAttributeAllocations[allocation.classId] = allocation;
        }

        private PlayerClassAttributeAllocation GetClassAttributeAllocation(byte classId)
        {
            PlayerClassAttributeAllocation allocation;
            if (classAttributeAllocations.TryGetValue(classId, out allocation))
                return allocation;

            return new PlayerClassAttributeAllocation(classId, 0, 0, 0, 0, 0, 0, 0);
        }

        public PlayerAttributePointState GetAttributePoints()
        {
            byte classId = GetAttributeAllocationClassId();
            // Patch 1.20: only Disciples of War/Magic earn allotment points.
            // Do not advertise unusable points (or legacy invalid allocations)
            // for crafting/gathering classes merely because they have levels.
            if (!IsDiscipleOfWarOrMagicClass(classId))
                return new PlayerAttributePointState(0, 0,
                    new PlayerClassAttributeAllocation(classId, 0, 0, 0, 0, 0, 0, 0));
            PlayerClassAttributeAllocation allocation = GetClassAttributeAllocation(classId);
            return new PlayerAttributePointState(
                GetEarnedAttributePointsForLevel(GetLevel()),
                GetAttributePointCapForLevel(GetLevel()),
                allocation);
        }

        public bool TrySetAttributePoints(int strength, int vitality, int dexterity, int intelligence, int mind, int piety)
        {
            byte classId = GetAttributeAllocationClassId();
            short level = GetLevel();
            short earnedPoints = GetEarnedAttributePointsForLevel(level);
            short statCap = GetAttributePointCapForLevel(level);
            int[] requested = { strength, vitality, dexterity, intelligence, mind, piety };
            PlayerClassAttributeAllocation previous = GetClassAttributeAllocation(classId);
            int[] existing = { previous.strength, previous.vitality, previous.dexterity,
                previous.intelligence, previous.mind, previous.piety };
            int spentPoints = 0;

            for (int i = 0; i < requested.Length; i++)
            {
                if (requested[i] < 0 || requested[i] > statCap)
                {
                    DevDiagnostics.Trace(
                        "stats.allocation.rejected",
                        "player", String.Format("0x{0:X}", actorId),
                        "classId", classId,
                        "level", level,
                        "reason", "stat-out-of-range",
                        "statIndex", i,
                        "requested", requested[i],
                        "statCap", statCap);
                    return false;
                }

                spentPoints += requested[i];
            }

            // Patch 1.21a separates paid guild-NPC resets from ordinary allotment.
            // The UI's Undo only cancels edits within the open window; it must
            // not refund a previously committed allocation through this path.
            for (int i = 0; i < requested.Length; i++)
            {
                if (requested[i] < existing[i])
                {
                    DevDiagnostics.Trace("stats.allocation.rejected",
                        "player", String.Format("0x{0:X}", actorId),
                        "classId", classId, "level", level,
                        "reason", "reset-required", "statIndex", i,
                        "previous", existing[i], "requested", requested[i]);
                    return false;
                }
            }

            if (!IsDiscipleOfWarOrMagicClass(classId) || spentPoints > earnedPoints)
            {
                DevDiagnostics.Trace(
                    "stats.allocation.rejected",
                    "player", String.Format("0x{0:X}", actorId),
                    "classId", classId,
                    "level", level,
                    "reason", !IsDiscipleOfWarOrMagicClass(classId) ? "unsupported-class" : "total-over-allotment",
                    "spentPoints", spentPoints,
                    "earnedPoints", earnedPoints,
                    "statCap", statCap);
                return false;
            }

            PlayerClassAttributeAllocation allocation = new PlayerClassAttributeAllocation(
                classId,
                (short)(earnedPoints - spentPoints),
                (short)strength,
                (short)vitality,
                (short)dexterity,
                (short)intelligence,
                (short)mind,
                (short)piety);

            if (!Database.SavePlayerClassAttributeAllocation(this, allocation))
                return false;

            SetClassAttributeAllocation(allocation);
            RecalculateStats("attribute-allocation");
            DevDiagnostics.Trace(
                "stats.allocation.committed",
                "player", String.Format("0x{0:X}", actorId),
                "classId", classId,
                "level", level,
                "spentPoints", spentPoints,
                "remainingPoints", allocation.pointsRemaining,
                "statCap", statCap,
                "str", strength,
                "vit", vitality,
                "dex", dexterity,
                "int", intelligence,
                "mnd", mind,
                "pie", piety);
            return true;
        }

        public static short GetEarnedAttributePointsForLevel(short level)
        {
            if (level < 10)
                return 0;

            return (short)(level - 5);
        }

        public static short GetAttributePointCapForLevel(short level)
        {
            if (level < 10)
                return 0;

            return (short)(3 + ((level - 10) / 2));
        }

        public static bool IsDiscipleOfWarOrMagicClass(byte classId)
        {
            return classId == CLASSID_PUG ||
                   classId == CLASSID_GLA ||
                   classId == CLASSID_MRD ||
                   classId == CLASSID_ARC ||
                   classId == CLASSID_LNC ||
                   classId == CLASSID_THM ||
                   classId == CLASSID_CNJ;
        }
        
        public static byte ConvertClassIdToJobId(byte classId)
        {
            byte jobId = classId;

            switch(classId)
            {
                case CLASSID_PUG:
                case CLASSID_GLA:
                case CLASSID_MRD:
                    jobId += 13;
                    break;
                case CLASSID_ARC:
                case CLASSID_LNC:
                    jobId += 11;
                    break;
                case CLASSID_THM:
                case CLASSID_CNJ:
                    jobId += 4;
                    break;
            }

            return jobId;
        }

        public static byte ConvertJobIdToClassId(byte jobId)
        {
            switch (jobId)
            {
                case JOBID_MNK:
                    return CLASSID_PUG;
                case JOBID_PLD:
                    return CLASSID_GLA;
                case JOBID_WAR:
                    return CLASSID_MRD;
                case JOBID_BRD:
                    return CLASSID_ARC;
                case JOBID_DRG:
                    return CLASSID_LNC;
                case JOBID_BLM:
                    return CLASSID_THM;
                case JOBID_WHM:
                    return CLASSID_CNJ;
                default:
                    return jobId;
            }
        }

        public byte GetAttributeAllocationClassId()
        {
            return ConvertJobIdToClassId(GetCurrentClassOrJob());
        }

        public byte GetBaseStatClassOrJobId()
        {
            return GetCurrentClassOrJob();
        }

        public void SetCurrentJob(byte jobId)
        {
            currentJob = jobId;
            BroadcastPacket(SetCurrentJobPacket.BuildPacket(actorId, jobId), true);
            Database.SavePlayerCurrentJob(this);
            RefreshEarnedActions(false);
            SendCharaExpInfo();
            RecalculateStats("job-change");
        }

        public short GetClassLevel(byte classId)
        {
            if (classId == 0 || classId > charaWork.battleSave.skillLevel.Length)
                return 0;

            return charaWork.battleSave.skillLevel[classId - 1];
        }

        public bool TryChangeToCurrentClassJob()
        {
            var timer = DevDiagnostics.Enabled ? System.Diagnostics.Stopwatch.StartNew() : null;
            int previousJob = currentJob;
            bool success = false;
            string outcome = "exception";
            DevDiagnostics.Trace("job.change.begin", "actor", actorId,
                "classId", GetClass(), "jobId", previousJob);
            try
            {
                success = TryChangeToCurrentClassJobCore(out outcome);
                return success;
            }
            catch (Exception exception)
            {
                outcome = "exception:" + exception.GetType().Name + ":" + outcome;
                throw;
            }
            finally
            {
                DevDiagnostics.Trace("job.change.end", "actor", actorId,
                    "classId", GetClass(), "previousJob", previousJob, "jobId", currentJob,
                    "success", success, "outcome", outcome, "elapsedMs", timer?.Elapsed.TotalMilliseconds);
            }
        }

        private bool TryChangeToCurrentClassJobCore(out string outcome)
        {
            outcome = "unsupported-base-class";
            byte baseClassId = charaWork.parameterSave.state_mainSkill[0];
            if (!JobProgressionPolicy.TryGetForBaseClass(baseClassId, out JobProgressionRequirement requirement))
                return false;

            if (currentJob == requirement.JobId)
            {
                outcome = "return-to-base-class";
                SetCurrentJob(0);
                return true;
            }

            outcome = "missing-soul-crystal";
            if (!HasItem(requirement.SoulCrystalItemId))
                return false;
            outcome = "level-requirements";
            if (!JobProgressionPolicy.MeetsLevelRequirements(requirement, GetClassLevel))
                return false;

            outcome = "job-selected";
            SetCurrentJob(requirement.JobId);
            return true;
        }

        //Gets the id of the player's current job. If they aren't a job, gets the id of their class
        public byte GetCurrentClassOrJob()
        {
            if (currentJob != 0)
                return (byte) currentJob;
            return charaWork.parameterSave.state_mainSkill[0];
        }

        public void hpstuff(uint hp)
        {
            SetMaxHP(hp);
            SetHP(hp);            
            mpMaxBase = (ushort)hp;
            charaWork.parameterSave.mpMax = (short)hp;
            charaWork.parameterSave.mp = (short)hp;
            AddTP(3000);
            updateFlags |= ActorUpdateFlags.HpTpMp;
        }
        
        public void SetCombos(int comboId1 = 0, int comboId2 = 0)
        {
            SetCombos(new int[] { comboId1, comboId2 });
        }

        public void SetCombos(int[] comboIds)
        {
            Array.Copy(comboIds, playerWork.comboNextCommandId, 2);

            //If we're starting or continuing a combo chain, add the status effect and combo cost bonus
            if (comboIds[0] != 0)
            {
                StatusEffect comboEffect = new StatusEffect(this, Server.GetWorldManager().GetStatusEffect((uint) StatusEffectId.Combo));
                comboEffect.SetDuration(13);
                comboEffect.SetOverwritable(1);
                statusEffects.AddStatusEffect(comboEffect, this);
                playerWork.comboCostBonusRate = 1;
            }
            //Otherwise we're ending a combo, remove the status
            else
            {
                statusEffects.RemoveStatusEffect(statusEffects.GetStatusEffectById((uint) StatusEffectId.Combo));
                playerWork.comboCostBonusRate = 0;
            }

            ActorPropertyPacketUtil comboPropertyPacket = new ActorPropertyPacketUtil("playerWork/combo", this);
            comboPropertyPacket.AddProperty("playerWork.comboCostBonusRate");
            comboPropertyPacket.AddProperty("playerWork.comboNextCommandId[0]");
            comboPropertyPacket.AddProperty("playerWork.comboNextCommandId[1]");
            QueuePackets(comboPropertyPacket.Done());
        }

        private string GetBaseStatProfileKey(byte classOrJobId, byte tribe, short level)
        {
            return String.Format("{0}:{1}:{2}", classOrJobId, tribe, level);
        }

        private PlayerBaseStatProfile GetCurrentBaseStatProfile()
        {
            byte classOrJobId = GetBaseStatClassOrJobId();
            byte tribe = playerWork.tribe;
            short level = GetLevel();
            string key = GetBaseStatProfileKey(classOrJobId, tribe, level);

            if (baseStatProfiles.ContainsKey(key))
                return baseStatProfiles[key];

            if (missingBaseStatProfiles.Contains(key))
                return null;

            PlayerBaseStatProfile profile = Database.GetPlayerBaseStats(classOrJobId, tribe, level);
            if (profile != null)
            {
                baseStatProfiles[key] = profile;
                return profile;
            }

            // Never erase a character's established base layer merely because
            // the next exact growth row has not yet been recovered. Reuse the
            // closest trace/client-backed lower row without inventing growth.
            // Once an exact row is added it wins on the next process start.
            profile = Database.GetPlayerBaseStatsAtOrBelow(classOrJobId, tribe, level);
            if (profile != null)
            {
                baseStatProfiles[key] = profile;
                DevDiagnostics.Trace(
                    "stats.base.fallback",
                    "player", String.Format("0x{0:X}", actorId),
                    "playerName", customDisplayName != null ? customDisplayName : actorName,
                    "classOrJobId", classOrJobId,
                    "tribe", tribe,
                    "requestedLevel", level,
                    "profileLevel", profile.level,
                    "source", profile.source);
                return profile;
            }

            missingBaseStatProfiles.Add(key);
            DevDiagnostics.Trace(
                "stats.base.missing",
                "player", String.Format("0x{0:X}", actorId),
                "playerName", customDisplayName != null ? customDisplayName : actorName,
                "classOrJobId", classOrJobId,
                "allocationClassId", GetAttributeAllocationClassId(),
                "tribe", tribe,
                "level", level);

            return null;
        }

        private void ApplyBaseStatProfile()
        {
            PlayerBaseStatProfile profile = GetCurrentBaseStatProfile();
            if (profile == null)
                return;

            AddRecalculatedMod(Modifier.Hp, profile.hp);
            AddRecalculatedMod(Modifier.Mp, profile.mp);
            AddRecalculatedMod(Modifier.Strength, profile.strength);
            AddRecalculatedMod(Modifier.Vitality, profile.vitality);
            AddRecalculatedMod(Modifier.Dexterity, profile.dexterity);
            AddRecalculatedMod(Modifier.Intelligence, profile.intelligence);
            AddRecalculatedMod(Modifier.Mind, profile.mind);
            AddRecalculatedMod(Modifier.Piety, profile.piety);

            DevDiagnostics.Trace(
                "stats.layer.base",
                "player", String.Format("0x{0:X}", actorId),
                "classOrJobId", profile.classId,
                "tribe", profile.tribe,
                "level", profile.level,
                "source", profile.source,
                "hp", profile.hp,
                "mp", profile.mp,
                "str", profile.strength,
                "vit", profile.vitality,
                "dex", profile.dexterity,
                "int", profile.intelligence,
                "mnd", profile.mind,
                "pie", profile.piety);
        }

        private void ApplyClassAttributeAllocation()
        {
            byte classId = GetAttributeAllocationClassId();
            if (!IsDiscipleOfWarOrMagicClass(classId))
                return;

            PlayerClassAttributeAllocation allocation = GetClassAttributeAllocation(classId);
            short earnedPoints = GetEarnedAttributePointsForLevel(GetLevel());
            short statCap = GetAttributePointCapForLevel(GetLevel());

            AddRecalculatedMod(Modifier.Strength, allocation.strength);
            AddRecalculatedMod(Modifier.Vitality, allocation.vitality);
            AddRecalculatedMod(Modifier.Dexterity, allocation.dexterity);
            AddRecalculatedMod(Modifier.Intelligence, allocation.intelligence);
            AddRecalculatedMod(Modifier.Mind, allocation.mind);
            AddRecalculatedMod(Modifier.Piety, allocation.piety);

            DevDiagnostics.Trace(
                "stats.layer.allocation",
                "player", String.Format("0x{0:X}", actorId),
                "classId", classId,
                "level", GetLevel(),
                "earnedPoints", earnedPoints,
                "storedRemaining", allocation.pointsRemaining,
                "storedSpent", allocation.SpentPoints(),
                "statCap", statCap,
                "str", allocation.strength,
                "vit", allocation.vitality,
                "dex", allocation.dexterity,
                "int", allocation.intelligence,
                "mnd", allocation.mind,
                "pie", allocation.piety);
        }

        private bool ApplyEquipmentParamBonus(int paramType, short value)
        {
            if (value == 0)
                return false;

            uint modifierId;
            if (!EquipmentStatPolicy.TryGetModifierId(paramType, out modifierId))
                return false;

            AddRecalculatedMod((Modifier)modifierId, value);
            return true;
        }

        private static bool HasEquipmentBenefits(InventoryItem item)
        {
            if (item == null || item.itemData == null)
                return false;

            // Non-durable equipment has no condition gate. Durable equipment is
            // always created with a modifier row at maximum condition; a missing
            // row or zero condition therefore means it provides no benefits.
            if (item.itemData.durability <= 0)
                return true;

            return item.modifiers != null && item.modifiers.durability > 0;
        }

        private int ApplyEquipmentBonusPairs(EquipmentItem itemData, bool includeHighQualityBonus)
        {
            int applied = 0;
            int[] types =
            {
                itemData.paramBonusType1,
                itemData.paramBonusType2,
                itemData.paramBonusType3,
                itemData.paramBonusType4,
                itemData.paramBonusType5,
                itemData.paramBonusType6,
                itemData.paramBonusType7,
                itemData.paramBonusType8,
                itemData.paramBonusType9,
                itemData.paramBonusType10
            };
            short[] values =
            {
                itemData.paramBonusValue1,
                itemData.paramBonusValue2,
                itemData.paramBonusValue3,
                itemData.paramBonusValue4,
                itemData.paramBonusValue5,
                itemData.paramBonusValue6,
                itemData.paramBonusValue7,
                itemData.paramBonusValue8,
                itemData.paramBonusValue9,
                itemData.paramBonusValue10
            };

            for (int i = 0; i < types.Length; i++)
            {
                int pairNumber = i + 1;
                if (!EquipmentStatPolicy.ShouldApplyBonusPair(pairNumber, includeHighQualityBonus))
                    continue;

                if (ApplyEquipmentParamBonus(types[i], values[i]))
                    applied++;
            }

            return applied;
        }

        private void ApplyMainHandToolStats(WeaponItem weapon, bool isHighQuality)
        {
            AddRecalculatedMod(Modifier.Craftsmanship,
                EquipmentStatPolicy.CalculateToolValue(weapon.craftProcessing, isHighQuality));
            AddRecalculatedMod(Modifier.MagicCraftsmanship,
                EquipmentStatPolicy.CalculateToolValue(weapon.craftMagicProcessing, isHighQuality));
            AddRecalculatedMod(Modifier.Control,
                EquipmentStatPolicy.CalculateToolValue(weapon.craftProcessControl, isHighQuality));
            AddRecalculatedMod(Modifier.Gathering,
                EquipmentStatPolicy.CalculateToolValue(weapon.harvestPotency, isHighQuality));
            AddRecalculatedMod(Modifier.Output,
                EquipmentStatPolicy.CalculateToolValue(weapon.harvestLimit, isHighQuality));
            AddRecalculatedMod(Modifier.Perception,
                EquipmentStatPolicy.CalculateToolValue(weapon.harvestRate, isHighQuality));
        }

        private void ApplyEquipmentStats()
        {
            int equippedItems = 0;
            int activeItems = 0;
            int appliedBonusPairs = 0;
            int armorDefense = 0;

            for (ushort slot = 0; slot < equipment.GetCapacity(); slot++)
            {
                InventoryItem equippedItem = equipment.GetItemAtSlot(slot);
                if (equippedItem == null)
                    continue;

                EquipmentItem itemData = equippedItem.itemData as EquipmentItem;
                if (itemData == null)
                    continue;

                equippedItems++;
                bool hasBenefits = HasEquipmentBenefits(equippedItem);
                bool atOrBelowLevel = itemData.level <= GetLevel();
                bool isHighQuality = equippedItem.quality >= 2;
                bool hasHighQualityBonus = equippedItem.modifiers != null
                    && equippedItem.modifiers.mainQuality > 1;
                int itemDefense = 0;
                int itemBonusPairs = 0;

                if (hasBenefits)
                {
                    activeItems++;

                    ArmorItem armor = itemData as ArmorItem;
                    if (armor != null)
                    {
                        itemDefense = EquipmentStatPolicy.CalculateArmorDefense(
                            armor.defense,
                            GetLevel(),
                            itemData.level,
                            isHighQuality);
                        AddRecalculatedMod(Modifier.Defense, itemDefense);
                        armorDefense += itemDefense;
                    }

                    // The 1.23b client exposes pair 3 as the append parameter,
                    // pair 4 only for HQ instances, and pairs 5-10 as ordinary
                    // bonuses. Official traces show those ordinary bonuses are
                    // absent when the item is above the player's level.
                    if (atOrBelowLevel)
                    {
                        itemBonusPairs = ApplyEquipmentBonusPairs(itemData, hasHighQualityBonus);
                        appliedBonusPairs += itemBonusPairs;
                    }

                    // Official captures show the displayed crafting/gathering
                    // trio comes from the main tool only. Above-level tool
                    // scaling is deliberately deferred until a capture proves
                    // whether the generic level-adjust curve applies here.
                    WeaponItem weapon = itemData as WeaponItem;
                    if (slot == SLOT_MAINHAND && weapon != null && atOrBelowLevel)
                        ApplyMainHandToolStats(weapon, isHighQuality);
                }

                DevDiagnostics.Trace(
                    "stats.layer.equipment.item",
                    "player", String.Format("0x{0:X}", actorId),
                    "slot", slot,
                    "itemId", equippedItem.itemId,
                    "itemLevel", itemData.level,
                    "playerLevel", GetLevel(),
                    "quality", equippedItem.quality,
                    "mainQuality", equippedItem.modifiers != null ? equippedItem.modifiers.mainQuality : 0,
                    "durability", equippedItem.modifiers != null ? equippedItem.modifiers.durability : 0,
                    "hasBenefits", hasBenefits,
                    "atOrBelowLevel", atOrBelowLevel,
                    "armorDefense", itemDefense,
                    "bonusPairs", itemBonusPairs);
            }

            DevDiagnostics.Trace(
                "stats.layer.equipment",
                "player", String.Format("0x{0:X}", actorId),
                "classId", GetClass(),
                "level", GetLevel(),
                "equippedItems", equippedItems,
                "activeItems", activeItems,
                "appliedBonusPairs", appliedBonusPairs,
                "armorDefense", armorDefense,
                "hp", GetMod(Modifier.Hp),
                "mp", GetMod(Modifier.Mp),
                "str", GetMod(Modifier.Strength),
                "vit", GetMod(Modifier.Vitality),
                "dex", GetMod(Modifier.Dexterity),
                "int", GetMod(Modifier.Intelligence),
                "mnd", GetMod(Modifier.Mind),
                "pie", GetMod(Modifier.Piety),
                "attack", GetMod(Modifier.Attack),
                "defense", GetMod(Modifier.Defense));
        }

        public override void CalculateBaseStats()
        {
            ApplyBaseStatProfile();
            ApplyClassAttributeAllocation();

            //Add weapon property mod
            var equip = GetEquipment();
            var mainHandItem = equip.GetItemAtSlot(SLOT_MAINHAND);
            var damageAttribute = 0;
            var attackDelay = 3000;
            var hitCount = 1;

            if (mainHandItem != null)
            {
                var mainHandWeapon = mainHandItem.itemData as WeaponItem;
                if (mainHandWeapon != null)
                {
                    damageAttribute = mainHandWeapon.damageAttributeType1;
                    attackDelay = (int)(mainHandWeapon.damageInterval * 1000);
                    hitCount = mainHandWeapon.frequency;
                }
            }

            var hasShield = equip.GetItemAtSlot(SLOT_OFFHAND)?.itemData.IsShieldWeapon() == true ? 1 : 0;
            SetMod((uint)Modifier.CanBlock, hasShield);

            SetMod((uint)Modifier.AttackType, damageAttribute);
            SetMod((uint)Modifier.Delay, attackDelay);
            SetMod((uint)Modifier.HitCount, hitCount);

            //These stats all correlate in a 3:2 fashion
            //It seems these stats don't actually increase their respective stats. The magic stats do, however
            AddRecalculatedMod(Modifier.Attack, (long)(GetMod(Modifier.Strength) * 0.667));
            AddRecalculatedMod(Modifier.Accuracy, (long)(GetMod(Modifier.Dexterity) * 0.667));
            AddRecalculatedMod(Modifier.Defense, (long)(GetMod(Modifier.Vitality) * 0.667));

            //These stats correlate in a 4:1 fashion. (Unsure if MND is accurate but it would make sense for it to be)
            AddRecalculatedMod(Modifier.AttackMagicPotency, (long)((float)GetMod(Modifier.Intelligence) * 0.25));

            AddRecalculatedMod(Modifier.MagicAccuracy, (long)((float)GetMod(Modifier.Mind) * 0.25));
            AddRecalculatedMod(Modifier.HealingMagicPotency, (long)((float)GetMod(Modifier.Mind) * 0.25));

            AddRecalculatedMod(Modifier.MagicEvasion, (long)((float)GetMod(Modifier.Piety) * 0.25));
            AddRecalculatedMod(Modifier.EnfeeblingMagicPotency, (long)((float)GetMod(Modifier.Piety) * 0.25));

            //VIT correlates to HP in a 1:1 fashion
            AddRecalculatedMod(Modifier.Hp, (long)GetMod(Modifier.Vitality));

            // Equipment is intentionally applied after the incomplete base
            // derivation above. Official gear-change captures show that primary
            // attributes granted by equipment do not cascade into the displayed
            // Attack/Accuracy/Defense values in 1.23b.
            ApplyEquipmentStats();

            CalculateTraitMods();
            base.CalculateBaseStats();
        }

        public bool HasTrait(ushort id)
        {
            BattleTrait trait = Server.GetWorldManager().GetBattleTrait(id);

            return HasTrait(trait);
        }

        public bool HasTrait(BattleTrait trait)
        {
            return (trait != null) && (trait.job == GetClass()) && (trait.level <= GetLevel());
        }

        public void CalculateTraitMods()
        {
            var traitIds = Server.GetWorldManager().GetAllBattleTraitIdsForClass((byte) GetClass());

            foreach(var traitId in traitIds)
            {
                var trait = Server.GetWorldManager().GetBattleTrait(traitId);
                if(HasTrait(trait))
                {
                    AddRecalculatedMod((Modifier)trait.modifier, trait.bonus);
                }
            }
        }

        public bool HasItemEquippedInSlot(uint itemId, ushort slot)
        {
            var equippedItem = equipment.GetItemAtSlot(slot);

            return equippedItem != null && equippedItem.itemId == itemId;
        }

        public Retainer GetSpawnedRetainer()
        {
            return currentSpawnedRetainer;
        }

        public void StartTradeTransaction(Player otherPlayer)
        {
            myOfferings = new ReferencedItemPackage(this, ItemPackage.MAXSIZE_TRADE, ItemPackage.TRADE);            
            otherTrader = otherPlayer;
            isTradeAccepted = false;
        }

        public Player GetOtherTrader()
        {
            return otherTrader;
        }

        public ReferencedItemPackage GetTradeOfferings()
        {
            return myOfferings;
        }

        public bool IsTrading()
        {
            return otherTrader != null;
        }

        public bool IsTradeAccepted()
        {
            return isTradeAccepted;
        }
        
        public void AddTradeItem(ushort slot, ItemRefParam chosenItem, int tradeQuantity)
        {
            if (!IsTrading())
                return;
            
            //Get chosen item
            InventoryItem offeredItem = itemPackages[chosenItem.itemPackage].GetItemAtSlot(chosenItem.slot);
            offeredItem.SetTradeQuantity(tradeQuantity);
            
            myOfferings.Set(slot, offeredItem);
            SendTradePackets();
        }
        
        public void RemoveTradeItem(ushort slot)
        {
            if (!IsTrading())
                return;

            InventoryItem offeredItem = myOfferings.GetItemAtSlot(slot);
            offeredItem.SetNormal();

            myOfferings.Clear(slot);
            SendTradePackets();
        }

        public void ClearTradeItems()
        {
            if (!IsTrading())
                return;

            for (ushort i = 0; i < myOfferings.GetCapacity(); i++)
            {
                InventoryItem offeredItem = myOfferings.GetItemAtSlot(i);
                if (offeredItem != null)
                    offeredItem.SetNormal();
            }

            myOfferings.ClearAll();
            SendTradePackets();
        }

        private void SendTradePackets()
        {
            //Send to self
            QueuePacket(InventoryBeginChangePacket.BuildPacket(actorId, true));
            myOfferings.SendUpdate(this);
            QueuePacket(InventoryEndChangePacket.BuildPacket(actorId));

            //Send to other trader
            otherTrader.QueuePacket(InventoryBeginChangePacket.BuildPacket(actorId, true));
            myOfferings.SendUpdateAsItemPackage(otherTrader);
            otherTrader.QueuePacket(InventoryEndChangePacket.BuildPacket(actorId));
        }

        public void AcceptTrade(bool accepted)
        {
            if (!IsTrading())
                return;
            isTradeAccepted = accepted;            
        }

        public void FinishTradeTransaction()
        {
            if (myOfferings != null)
            {
                myOfferings.ClearAll();
                for (ushort i = 0; i < myOfferings.GetCapacity(); i++)
                {
                    InventoryItem offeredItem = myOfferings.GetItemAtSlot(i);
                    if (offeredItem != null)
                        offeredItem.SetNormal();
                }

                QueuePacket(InventoryBeginChangePacket.BuildPacket(actorId, true));
                myOfferings.SendUpdate(this);
                QueuePacket(InventoryEndChangePacket.BuildPacket(actorId));
            }

            isTradeAccepted = false;
            myOfferings = null;
            otherTrader = null;
        }
        
    }
}
