using AetherXIV.Core.Map.actors.director;
using AetherXIV.Core.Map.Actors;
using AetherXIV.Core.Map.dataobjects;
using AetherXIV.Core.Map.packets.receive.events;
using AetherXIV.Core.Map.packets.send;
using AetherXIV.Core.Map.packets.send.events;
using MoonSharp.Interpreter;
using MoonSharp.Interpreter.Interop;
using MoonSharp.Interpreter.Loaders;
using System;
using System.Collections.Generic;
using System.IO;
using AetherXIV.Core.Common;
using AetherXIV.Core.Map.actors.area;
using System.Threading;
using AetherXIV.Core.Map.actors.chara.ai;
using AetherXIV.Core.Map.actors.chara.ai.controllers;

namespace AetherXIV.Core.Map.lua
{
    class LuaEngine
    {
        public const string FILEPATH_PLAYER = "./scripts/player.lua";
        public const string FILEPATH_ZONE = "./scripts/unique/{0}/zone.lua";
        public const string FILEPATH_CONTENT = "./scripts/content/{0}.lua";
        public const string FILEPATH_COMMANDS = "./scripts/commands/{0}.lua";
        public const string FILEPATH_DIRECTORS = "./scripts/directors/{0}.lua";
        public const string FILEPATH_NPCS = "./scripts/unique/{0}/{1}/{2}.lua";
        public const string FILEPATH_QUEST = "./scripts/quests/{0}/{1}.lua";

        private static LuaEngine mThisEngine;
        private Dictionary<Coroutine, ulong> mSleepingOnTime = new Dictionary<Coroutine, ulong>();
        private Dictionary<string, List<Coroutine>> mSleepingOnSignal = new Dictionary<string, List<Coroutine>>();
        private readonly Object mWaiterLock = new Object();
        private sealed class PlayerEventWaiter
        {
            public Coroutine Coroutine;
            public uint ExpectedOwner;
            public string ExpectedName;
            public byte ExpectedType;
        }

        private Dictionary<uint, PlayerEventWaiter> mSleepingOnPlayerEvent = new Dictionary<uint, PlayerEventWaiter>();

        private Timer luaTimer;


        private LuaEngine()
        {
            UserData.RegistrationPolicy = InteropRegistrationPolicy.Automatic;
            RegisterLuaSequenceConversions();

            luaTimer = new Timer(new TimerCallback(PulseSleepingOnTime),
                           null, TimeSpan.Zero, TimeSpan.FromMilliseconds(50));
        }

        private static void RegisterLuaSequenceConversions()
        {
            // Legacy quest scripts consume these return values as ordinary Lua
            // sequences (unpack, #, and one-based indexing). MoonSharp otherwise
            // exposes CLR arrays as userdata, which breaks the journal command and
            // the generic PopulaceStandard quest selector.
            Script.GlobalOptions.CustomConverters.SetClrToScriptCustomConversion<object[]>(
                (script, values) => CreateLuaSequence(script, values));
            Script.GlobalOptions.CustomConverters.SetClrToScriptCustomConversion<Quest[]>(
                (script, values) => CreateLuaSequence(script, values));
        }

        private static DynValue CreateLuaSequence<T>(Script script, IEnumerable<T> values)
        {
            Table table = new Table(script);
            if (values == null)
                return DynValue.NewTable(table);

            int index = 1;
            foreach (T value in values)
            {
                table.Set(index, DynValue.FromObject(script, value));
                index++;
            }

            return DynValue.NewTable(table);
        }

        public static LuaEngine GetInstance()
        {
            if (mThisEngine == null)
                mThisEngine = new LuaEngine();

            return mThisEngine;
        }

        public void AddWaitCoroutine(Coroutine coroutine, float seconds)
        {
            ulong time = Utils.MilisUnixTimeStampUTC() + (ulong)(seconds * 1000);
            int waiterCount;
            lock (mWaiterLock)
            {
                mSleepingOnTime.Add(coroutine, time);
                waiterCount = mSleepingOnTime.Count;
            }
            DevDiagnostics.Trace(
                "lua.wait.register",
                "waitType", "_WAIT_TIME",
                "seconds", seconds,
                "wakeTime", time,
                "coroutine", coroutine.GetHashCode(),
                "timeWaiters", waiterCount);
        }

        public void AddWaitSignalCoroutine(Coroutine coroutine, string signal)
        {
            int waiterCount;
            lock (mWaiterLock)
            {
                if (!mSleepingOnSignal.ContainsKey(signal))
                    mSleepingOnSignal.Add(signal, new List<Coroutine>());
                mSleepingOnSignal[signal].Add(coroutine);
                waiterCount = mSleepingOnSignal[signal].Count;
            }
            DevDiagnostics.Trace(
                "lua.wait.register",
                "waitType", "_WAIT_SIGNAL",
                "signal", signal,
                "coroutine", coroutine.GetHashCode(),
                "signalWaiters", waiterCount);
        }

        public void AddWaitEventCoroutine(
            Player player,
            Coroutine coroutine,
            uint expectedOwner = 0,
            string expectedName = null,
            byte expectedType = 0)
        {
            int waiterCount;
            lock (mWaiterLock)
            {
                if (!mSleepingOnPlayerEvent.ContainsKey(player.actorId))
                {
                    mSleepingOnPlayerEvent.Add(player.actorId, new PlayerEventWaiter
                    {
                        Coroutine = coroutine,
                        ExpectedOwner = expectedOwner,
                        ExpectedName = expectedName ?? "",
                        ExpectedType = expectedType
                    });
                }
                waiterCount = mSleepingOnPlayerEvent.Count;
            }
            DevDiagnostics.Trace(
                "lua.wait.register",
                "player", player.customDisplayName,
                "actor", String.Format("0x{0:X}", player.actorId),
                "waitType", "_WAIT_EVENT",
                "coroutine", coroutine.GetHashCode(),
                "expectedOwner", String.Format("0x{0:X}", expectedOwner),
                "expectedName", expectedName ?? "",
                "expectedType", expectedType,
                "eventWaiters", waiterCount);
        }

        /// <summary>
        /// Drops a player's parked _WAIT_EVENT coroutine without resuming it.
        /// Called on session teardown (logout / disconnect / map handoff) so a
        /// mid-cutscene park can't be resumed by an unrelated event after
        /// relog — Garlemald handle_session_end purge_owner parity (the stale
        /// Charlys→Hobriaut hijack).
        /// </summary>
        public void PurgePlayerEventWaiter(Player player)
        {
            if (player == null)
                return;

            bool purged;
            lock (mWaiterLock)
            {
                purged = mSleepingOnPlayerEvent.Remove(player.actorId);
            }

            if (purged)
            {
                DevDiagnostics.Trace(
                    "lua.wait.purged",
                    "player", player.customDisplayName,
                    "actor", String.Format("0x{0:X}", player.actorId),
                    "action", "purge-on-session-end");
            }
        }

        public void PulseSleepingOnTime(object state)
        {
            ulong currentTime = Utils.MilisUnixTimeStampUTC();
            List<Coroutine> mToAwake = new List<Coroutine>();
            int remainingWaiters;
            lock (mWaiterLock)
            {
                foreach (KeyValuePair<Coroutine, ulong> entry in mSleepingOnTime)
                {
                    if (entry.Value <= currentTime)
                        mToAwake.Add(entry.Key);
                }

                foreach (Coroutine coroutine in mToAwake)
                    mSleepingOnTime.Remove(coroutine);
                remainingWaiters = mSleepingOnTime.Count;
            }

            foreach (Coroutine key in mToAwake)
            {
                DevDiagnostics.Trace(
                    "lua.time.resume",
                    "coroutine", key.GetHashCode(),
                    "remainingTimeWaiters", remainingWaiters);
                DynValue value = key.Resume();
                ResolveResume(null, key, value);
            }
        }

        public void OnSignal(string signal, params object[] args)
        {
            List<Coroutine> mToAwake = new List<Coroutine>();
            int waiterCount;
            lock (mWaiterLock)
            {
                waiterCount = mSleepingOnSignal.ContainsKey(signal)
                    ? mSleepingOnSignal[signal].Count
                    : 0;
                if (mSleepingOnSignal.ContainsKey(signal))
                {
                    mToAwake.AddRange(mSleepingOnSignal[signal]);
                    mSleepingOnSignal.Remove(signal);
                }
            }
            DevDiagnostics.Trace(
                "lua.signal.emit",
                "signal", signal,
                "args", args == null ? 0 : args.Length,
                "waiters", waiterCount);

            foreach (Coroutine key in mToAwake)
            {
                DevDiagnostics.Trace(
                    "lua.signal.resume",
                    "signal", signal,
                    "coroutine", key.GetHashCode());
                DynValue value = key.Resume(args);
                ResolveResume(null, key, value);
            }
        }

        internal static bool MatchesExpectedPlayerEvent(
            uint expectedOwner,
            string expectedName,
            byte expectedType,
            uint actualOwner,
            string actualName,
            byte actualType)
        {
            if (expectedOwner != 0 && expectedOwner != actualOwner)
                return false;
            if (!String.IsNullOrEmpty(expectedName) && !String.Equals(expectedName, actualName, StringComparison.Ordinal))
                return false;
            return expectedType == 0 || expectedType == actualType;
        }

        private bool TryTakePlayerEventWaiter(
            Player player,
            uint actualOwner,
            string actualName,
            byte actualType,
            out PlayerEventWaiter waiter)
        {
            lock (mWaiterLock)
            {
                if (!mSleepingOnPlayerEvent.TryGetValue(player.actorId, out waiter))
                    return false;
                if (!MatchesExpectedPlayerEvent(waiter.ExpectedOwner, waiter.ExpectedName, waiter.ExpectedType, actualOwner, actualName, actualType))
                {
                    DevDiagnostics.Trace("lua.wait.eventMismatch", "player", player.customDisplayName,
                        "actor", String.Format("0x{0:X}", player.actorId),
                        "expectedOwner", String.Format("0x{0:X}", waiter.ExpectedOwner),
                        "expectedName", waiter.ExpectedName,
                        "actualOwner", String.Format("0x{0:X}", actualOwner),
                        "actualName", actualName ?? "",
                        "actualType", actualType,
                        "action", "dispatch unrelated event");
                    waiter = null;
                    return false;
                }
                mSleepingOnPlayerEvent.Remove(player.actorId);
                return true;
            }
        }

        private bool TryTakePlayerEventWaiter(Player player, out PlayerEventWaiter waiter)
        {
            lock (mWaiterLock)
            {
                if (!mSleepingOnPlayerEvent.TryGetValue(player.actorId, out waiter))
                    return false;
                mSleepingOnPlayerEvent.Remove(player.actorId);
                return true;
            }
        }

        public void OnEventUpdate(Player player, List<LuaParam> args)
        {
            PlayerEventWaiter waiter;
            bool hadWaiter = TryTakePlayerEventWaiter(player, out waiter);

            if (hadWaiter)
            {
                try
                {
                    DevDiagnostics.Trace(
                        "lua.resume",
                        "player", player.customDisplayName,
                        "actor", String.Format("0x{0:X}", player.actorId),
                        "source", "event.update",
                        "coroutine", waiter.Coroutine.GetHashCode(),
                        "params", LuaUtils.DumpParams(args));
                    Coroutine coroutine = waiter.Coroutine;
                    if (waiter.ExpectedOwner != 0)
                    {
                        player.currentEventOwner = waiter.ExpectedOwner;
                        player.currentEventName = waiter.ExpectedName;
                        player.currentEventType = waiter.ExpectedType;
                    }
                    DynValue value = coroutine.Resume(LuaUtils.CreateLuaParamObjectList(args));
                    ResolveResume(player, coroutine, value);
                }
                catch (ScriptRuntimeException e)
                {
                    LuaEngine.SendError(player, String.Format("OnEventUpdated: {0}", e.DecoratedMessage));
                }
            }
            else
            {
                DevDiagnostics.Trace(
                    "lua.resumeMissing",
                    "player", player.customDisplayName,
                    "actor", String.Format("0x{0:X}", player.actorId),
                    "source", "event.update",
                    "params", LuaUtils.DumpParams(args));
                player.EndEvent();
            }
        }

        /// <summary> 
        /// // todo: this is dumb, should probably make a function for each action with different default return values
        /// or just make generic function and pass default value as first arg after functionName
        /// </summary>
        public static void CallLuaBattleFunction(Character actor, string functionName, params object[] args)
        {
            // todo: should use "scripts/zones/ZONE_NAME/battlenpcs/NAME.lua" instead of scripts/unique
            string path = "";

            // todo: should we call this for players too?
            if (actor is Player)
            {
                // todo: check this is correct
                path = FILEPATH_PLAYER;
            }
            else if (actor is Npc)
            {
                // todo: this is probably unnecessary as im not sure there were pets for players
                if (!(actor.aiContainer.GetController<PetController>()?.GetPetMaster() is Player))
                    path = String.Format("./scripts/unique/{0}/{1}/{2}.lua", actor.zone.zoneName, actor is BattleNpc ? "Monster" : "PopulaceStandard", ((Npc)actor).GetUniqueId());
            }
            // dont wanna throw an error if file doesnt exist
            path = ResolveExistingPath(path);
            if (File.Exists(path))
            {
                var script = LoadGlobals();
                try
                {
                    script.DoFile(path);
                }
                catch (Exception e)
                {
                    Program.Log.Error($"LuaEngine.CallLuaBattleFunction [{functionName}] {e.Message}");
                }
                DynValue res = new DynValue();

                if (!script.Globals.Get(functionName).IsNil())
                {
                    res = script.Call(script.Globals.Get(functionName), args);
                }
            }
        }

        public static int CallLuaStatusEffectFunction(Character actor, StatusEffect effect, string functionName, params object[] args)
        {
            // todo: this is stupid, load the actual effect name from db table
            string path = $"./scripts/effects/{effect.GetName()}.lua";

            if (File.Exists(path))
            {
                var script = LoadGlobals();

                try
                {
                    script.DoFile(path);
                }
                catch (Exception e)
                {
                    Program.Log.Error($"LuaEngine.CallLuaStatusEffectFunction [{functionName}] {e.Message}");
                }
                DynValue res = new DynValue();

                if (!script.Globals.Get(functionName).IsNil())
                {
                    res = script.Call(script.Globals.Get(functionName), args);
                    if (res != null)
                        return (int)res.Number;
                }
            }
            else
            {
                Program.Log.Error($"LuaEngine.CallLuaStatusEffectFunction [{effect.GetName()}] Unable to find script {path}");
            }
            return -1;
        }

        public static int CallLuaBattleCommandFunction(Character actor, BattleCommand command, string folder, string functionName, params object[] args)
        {
            string path = $"./scripts/commands/{folder}/{command.name}.lua";
            string requestedPath = path;

            if (File.Exists(path))
            {
                DevDiagnostics.Trace(
                    "lua.commandScript.resolve",
                    "actor", actor == null ? "0x0" : String.Format("0x{0:X}", actor.actorId),
                    "actorName", actor == null ? "" : (actor.customDisplayName != null ? actor.customDisplayName : actor.actorName),
                    "commandId", command.id,
                    "commandName", command.name,
                    "folder", folder,
                    "function", functionName,
                    "path", path,
                    "defaultUsed", false,
                    "resolved", true);
                var script = LoadGlobals();

                try
                {
                    script.DoFile(path);
                }
                catch (Exception e)
                {
                    Program.Log.Error($"LuaEngine.CallLuaBattleCommandFunction [{functionName}] {e.Message}");
                }
                DynValue res = new DynValue();
                
                if (!script.Globals.Get(functionName).IsNil())
                {
                    res = script.Call(script.Globals.Get(functionName), args);
                    if (res != null)
                        return (int)res.Number;
                }
            }
            else
            {
                path = $"./scripts/commands/{folder}/default.lua";
                bool defaultExists = File.Exists(path);
                DevDiagnostics.Trace(
                    "lua.commandScript.resolve",
                    "actor", actor == null ? "0x0" : String.Format("0x{0:X}", actor.actorId),
                    "actorName", actor == null ? "" : (actor.customDisplayName != null ? actor.customDisplayName : actor.actorName),
                    "commandId", command.id,
                    "commandName", command.name,
                    "folder", folder,
                    "function", functionName,
                    "path", path,
                    "requestedPath", requestedPath,
                    "defaultUsed", true,
                    "resolved", defaultExists);
                if (!defaultExists)
                    return -1;

                var script = LoadGlobals();

                try
                {
                    script.DoFile(path);
                }
                catch (Exception e)
                {
                    Program.Log.Error($"LuaEngine.CallLuaBattleCommandFunction [{functionName}] {e.Message}");
                }
                DynValue res = new DynValue();
               // DynValue r = script.Globals.Get(functionName);

                if (!script.Globals.Get(functionName).IsNil())
                {
                    res = script.Call(script.Globals.Get(functionName), args);
                    if (res != null)
                        return (int)res.Number;
                }
            }
            return -1;
        }


        public static void LoadBattleCommandScript(BattleCommand command, string folder)
        {
            string path = $"./scripts/commands/{folder}/{command.name}.lua";

            if (File.Exists(path))
            {
                var script = LoadGlobals();

                try
                {
                    script.DoFile(path);
                }
                catch (Exception e)
                {
                    Program.Log.Error($"LuaEngine.CallLuaBattleCommandFunction {e.Message}");
                }
                command.script = script;
            }
            else
            {
                path = $"./scripts/commands/{folder}/default.lua";
                if (!File.Exists(path))
                {
                    DevDiagnostics.Trace(
                        "lua.commandScript.resolve",
                        "commandId", command.id,
                        "commandName", command.name,
                        "folder", folder,
                        "path", path,
                        "defaultUsed", true,
                        "resolved", false);
                    return;
                }

                var script = LoadGlobals();

                try
                {
                    script.DoFile(path);
                }
                catch (Exception e)
                {
                    Program.Log.Error($"LuaEngine.CallLuaBattleCommandFunction {e.Message}");
                }

                command.script = script;
            }
        }

        public static void LoadStatusEffectScript(StatusEffect effect)
        {
            string path = $"./scripts/effects/{effect.GetName()}.lua";

            if (File.Exists(path))
            {
                var script = LoadGlobals();

                try
                {
                    script.DoFile(path);
                }
                catch (Exception e)
                {
                    Program.Log.Error($"LuaEngine.CallLuaBattleCommandFunction {e.Message}");
                }
                effect.script = script;
            }
            else
            {
                path = $"./scripts/effects/default.lua";
                if (!File.Exists(path))
                {
                    DevDiagnostics.Trace(
                        "lua.effectScript.resolve",
                        "effect", effect.GetName(),
                        "path", path,
                        "defaultUsed", true,
                        "resolved", false);
                    return;
                }

                var script = LoadGlobals();

                try
                {
                    script.DoFile(path);
                }
                catch (Exception e)
                {
                    Program.Log.Error($"LuaEngine.CallLuaBattleCommandFunction {e.Message}");
                }

                effect.script = script;
            }
        }


        public static string GetScriptPath(Actor target)
        {
            if (target is Player)
            {
                return String.Format(FILEPATH_PLAYER);
            }
            else if (target is Npc)
            {
                return null;
            }
            else if (target is Command)
            {
                return String.Format(FILEPATH_COMMANDS, target.GetName());
            }
            else if (target is Director)
            {
                return String.Format(FILEPATH_DIRECTORS, ((Director)target).GetScriptPath());
            }
            else if (target is PrivateAreaContent)
            {
                return String.Format(FILEPATH_CONTENT, ((PrivateAreaContent)target).GetPrivateAreaName());
            }
            else if (target is Area)
            {
                return String.Format(FILEPATH_ZONE, ((Area)target).zoneName);
            }
            else if (target is Quest)
            {
                // Quest class names come from the legacy catalog with mixed
                // casing (for example, Man0g1), while the canonical packaged
                // script tree is lowercase. APFS/NTFS tolerated the mismatch;
                // Linux does not.
                string questName = ((Quest)target).actorName.ToLowerInvariant();
                string initial = questName.Substring(0, 3);
                return String.Format(FILEPATH_QUEST, initial, questName);
            }
            else
                return "";
        }

        public static string ResolveExistingPath(string requestedPath)
        {
            if (String.IsNullOrWhiteSpace(requestedPath))
                return requestedPath;

            if (File.Exists(requestedPath) || Directory.Exists(requestedPath))
                return requestedPath;

            string fullPath;
            try
            {
                fullPath = Path.GetFullPath(requestedPath);
            }
            catch
            {
                return requestedPath;
            }

            string root = Path.GetPathRoot(fullPath);
            string current = String.IsNullOrEmpty(root) ? Directory.GetCurrentDirectory() : root;
            string remainder = fullPath.Substring(current.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string[] parts = remainder.Split(new char[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar }, StringSplitOptions.RemoveEmptyEntries);

            foreach (string part in parts)
            {
                string direct = Path.Combine(current, part);
                if (File.Exists(direct) || Directory.Exists(direct))
                {
                    current = direct;
                    continue;
                }

                if (!Directory.Exists(current))
                    return requestedPath;

                string match = null;
                try
                {
                    foreach (string entry in Directory.GetFileSystemEntries(current))
                    {
                        if (String.Equals(Path.GetFileName(entry), part, StringComparison.OrdinalIgnoreCase))
                        {
                            match = entry;
                            break;
                        }
                    }
                }
                catch
                {
                    return requestedPath;
                }

                if (match == null)
                    return requestedPath;

                current = match;
            }

            return current;
        }

        private static string ResolveActorScriptPath(Player player, Actor target, string funcName)
        {
            string requestedPath = GetScriptPath(target);
            string resolvedPath = ResolveExistingPath(requestedPath);

            DevDiagnostics.Trace(
                "lua.script.resolve",
                "player", player == null ? "(none)" : player.customDisplayName,
                "actor", target == null ? "(none)" : target.GetName(),
                "actorType", target == null ? "(none)" : target.GetType().Name,
                "function", funcName,
                "requestedPath", requestedPath,
                "resolvedPath", resolvedPath,
                "resolved", !String.IsNullOrWhiteSpace(resolvedPath) && File.Exists(resolvedPath));

            return resolvedPath;
        }

        private List<LuaParam> CallLuaFunctionNpcForReturn(Player player, Npc target, string funcName, bool optional, params object[] args)
        {
            object[] args2 = new object[args.Length + (player == null ? 1 : 2)];
            Array.Copy(args, 0, args2, (player == null ? 1 : 2), args.Length);
            if (player != null)
            {
                args2[0] = player;
                args2[1] = target;
            }
            else
                args2[0] = target;

            LuaScript parent = null, child = null;
            string parentRequestedPath = "./scripts/base/" + target.classPath + ".lua";
            string parentPath = ResolveExistingPath(parentRequestedPath);
            string childPath = null;
            string childRequestedPath = null;

            if (File.Exists(parentPath))
                parent = LuaEngine.LoadScript(parentPath);

            Area area = target.zone;
            if (area is PrivateArea)
            {
                childRequestedPath = String.Format("./scripts/unique/{0}/PrivateArea/{1}_{2}/{3}/{4}.lua", area.zoneName, ((PrivateArea)area).GetPrivateAreaName(), ((PrivateArea)area).GetPrivateAreaType(), target.className, target.GetUniqueId());
                childPath = ResolveExistingPath(childRequestedPath);
                if (File.Exists(childPath))
                    child = LuaEngine.LoadScript(childPath);
            }
            else
            {
                childRequestedPath = String.Format("./scripts/unique/{0}/{1}/{2}.lua", area.zoneName, target.className, target.GetUniqueId());
                childPath = ResolveExistingPath(childRequestedPath);
                if (File.Exists(childPath))
                    child = LuaEngine.LoadScript(childPath);
            }

            DevDiagnostics.Trace(
                "lua.script.resolve",
                "player", player == null ? "(none)" : player.customDisplayName,
                "actor", target.GetName(),
                "uniqueId", target.GetUniqueId(),
                "className", target.className,
                "classPath", target.classPath,
                "function", funcName,
                "parentRequestedPath", parentRequestedPath,
                "parentPath", parentPath,
                "parentExists", parent != null,
                "childRequestedPath", childRequestedPath,
                "childPath", childPath,
                "childExists", child != null,
                "resolved", parent != null || child != null);

            if (parent == null && child == null)
            {
                LuaEngine.SendError(player, String.Format("ERROR: Could not find script for actor {0}.", target.GetName()));
            }

            //Run Script
            DynValue result;

            if (child != null && child.Globals[funcName] != null)
                result = ((Script)child).Call(child.Globals[funcName], args2);
            else if (parent != null && parent.Globals[funcName] != null)
                result = ((Script)parent).Call(parent.Globals[funcName], args2);
            else
                return null;

            List<LuaParam> lparams = LuaUtils.CreateLuaParamList(result);
            return lparams;
        }

        private void CallLuaFunctionNpc(Player player, Npc target, string funcName, bool optional, params object[] args)
        {
            object[] args2 = new object[args.Length + (player == null ? 1 : 2)];
            Array.Copy(args, 0, args2, (player == null ? 1 : 2), args.Length);
            if (player != null)
            {
                args2[0] = player;
                args2[1] = target;
            }
            else
                args2[0] = target;

            LuaScript parent = null, child = null;
            string parentRequestedPath = "./scripts/base/" + target.classPath + ".lua";
            string parentPath = ResolveExistingPath(parentRequestedPath);
            string childPath = null;
            string childRequestedPath = null;

            if (File.Exists(parentPath))
                parent = LuaEngine.LoadScript(parentPath);

            Area area = target.zone;
            if (area is PrivateArea)
            {
                childRequestedPath = String.Format("./scripts/unique/{0}/PrivateArea/{1}_{2}/{3}/{4}.lua", area.zoneName, ((PrivateArea)area).GetPrivateAreaName(), ((PrivateArea)area).GetPrivateAreaType(), target.className, target.GetUniqueId());
                childPath = ResolveExistingPath(childRequestedPath);
                if (File.Exists(childPath))
                    child = LuaEngine.LoadScript(childPath);
            }
            else
            {
                childRequestedPath = String.Format("./scripts/unique/{0}/{1}/{2}.lua", area.zoneName, target.className, target.GetUniqueId());
                childPath = ResolveExistingPath(childRequestedPath);
                if (File.Exists(childPath))
                    child = LuaEngine.LoadScript(childPath);
            }

            DevDiagnostics.Trace(
                "lua.script.resolve",
                "player", player == null ? "(none)" : player.customDisplayName,
                "actor", target.GetName(),
                "uniqueId", target.GetUniqueId(),
                "className", target.className,
                "classPath", target.classPath,
                "function", funcName,
                "parentRequestedPath", parentRequestedPath,
                "parentPath", parentPath,
                "parentExists", parent != null,
                "childRequestedPath", childRequestedPath,
                "childPath", childPath,
                "childExists", child != null,
                "resolved", parent != null || child != null);

            if (parent == null && child == null)
            {
                LuaEngine.SendError(player, String.Format("Could not find script for actor {0}.", target.GetName()));
                return;
            }

            //Run Script
            Coroutine coroutine = null;

            if (child != null && !child.Globals.Get(funcName).IsNil())
                coroutine = ((Script)child).CreateCoroutine(child.Globals[funcName]).Coroutine;
            else if (parent != null && parent.Globals.Get(funcName) != null && !parent.Globals.Get(funcName).IsNil())
                coroutine = ((Script)parent).CreateCoroutine(parent.Globals[funcName]).Coroutine;

            if (coroutine != null)
            {
                try
                {
                    DynValue value = coroutine.Resume(args2);
                    ResolveResume(player, coroutine, value);
                }
                catch (Exception e)
                {
                    string message = e is ScriptRuntimeException scriptException
                        ? scriptException.DecoratedMessage
                        : e.Message;
                    TraceLuaFailure(player, target, funcName, childPath ?? parentPath, e, message);
                    Program.Log.Error("Lua NPC function failed: player={0} actor={1} unique={2} class={3} func={4} parent={5} child={6}: {7}",
                        player != null ? player.customDisplayName : "(none)",
                        target.GetName(),
                        target.GetUniqueId(),
                        target.classPath,
                        funcName,
                        parentPath,
                        childPath,
                        message);
                    SendError(player, message);
                }
            }
            else if (!optional)
            {
                if (IsOptionalMonsterEvent(target, funcName))
                {
                    TraceOptionalMissingFunction(player, target, funcName, parentPath, childPath);
                    if (player != null && funcName == "onEventStarted")
                        player.EndEvent();
                    return;
                }

                LuaEngine.SendError(player, String.Format("Could not find function '{0}' for actor {1}.", funcName, target.GetName()));
            }
        }

        private static bool IsOptionalMonsterEvent(Npc target, string funcName)
        {
            return funcName == "onEventStarted" && target is BattleNpc;
        }

        private static void TraceOptionalMissingFunction(Player player, Npc target, string funcName, string parentPath, string childPath)
        {
            DevDiagnostics.Trace(
                "lua.function.missing.optional",
                "player", player == null ? "(none)" : player.customDisplayName,
                "actor", target.GetName(),
                "actorId", String.Format("0x{0:X}", target.actorId),
                "uniqueId", target.GetUniqueId(),
                "className", target.className,
                "classPath", target.classPath,
                "function", funcName,
                "parentPath", parentPath,
                "childPath", childPath,
                "reason", "battle NPC event hook is optional");
        }

        public List<LuaParam> CallLuaFunctionForReturn(Player player, Actor target, string funcName, bool optional, params object[] args)
        {
            //Need a seperate case for NPCs cause that child/parent thing.
            if (target is Npc)
                return CallLuaFunctionNpcForReturn(player, (Npc)target, funcName, optional, args);

            object[] args2 = ComposeActorFunctionArguments(player, target, args);

            string luaPath = ResolveActorScriptPath(player, target, funcName);
            LuaScript script = LoadScript(luaPath);
            if (script != null)
            {
                if (!script.Globals.Get(funcName).IsNil())
                {
                    try
                    {
                        DynValue result = ((Script)script).Call(
                            script.Globals[funcName],
                            args2);
                        return LuaUtils.CreateLuaParamList(result);
                    }
                    catch (Exception e)
                    {
                        string message = e is ScriptRuntimeException scriptException
                            ? scriptException.DecoratedMessage
                            : e.Message;
                        TraceLuaFailure(player, target, funcName, luaPath, e, message);
                        Program.Log.Error(
                            "Lua function failed: player={0} actor={1} func={2} path={3}: {4}",
                            player == null ? "(none)" : player.customDisplayName,
                            target.GetName(),
                            funcName,
                            luaPath,
                            message);
                        SendError(player, message);
                    }
                }
                else if (!optional)
                {
                    string message = String.Format(
                        "Could not find function '{0}' for actor {1}.",
                        funcName,
                        target.GetName());
                    Program.Log.Error("{0} Script path: {1}.", message, luaPath);
                    SendError(player, message);
                }
            }
            else if (!optional)
            {
                string message = String.Format(
                    "Could not find script for actor {0}.",
                    target.GetName());
                Program.Log.Error("{0} Requested path: {1}.", message, luaPath);
                SendError(player, message);
            }
            return null;
        }

        public List<LuaParam> CallLuaFunctionForReturn(string path, string funcName, bool optional, params object[] args)
        {
            string luaPath = ResolveExistingPath(path);
            LuaScript script = LoadScript(luaPath);
            if (script != null)
            {
                if (!script.Globals.Get(funcName).IsNil())
                {
                    //Run Script
                    DynValue result = ((Script)script).Call(
                        script.Globals[funcName],
                        args);
                    List<LuaParam> lparams = LuaUtils.CreateLuaParamList(result);
                    return lparams;
                }
            }
            return null;
        }

        public void CallLuaFunction(Player player, Actor target, string funcName, bool optional, params object[] args)
        {
            //Need a seperate case for NPCs cause that child/parent thing.
            if (target is Npc)
            {
                CallLuaFunctionNpc(player, (Npc)target, funcName, optional, args);
                return;
            }

            object[] args2 = ComposeActorFunctionArguments(player, target, args);

            string luaPath = ResolveActorScriptPath(player, target, funcName);
            LuaScript script = LoadScript(luaPath);
            if (script != null)
            {
                if (!script.Globals.Get(funcName).IsNil())
                {
                    try
                    {
                        Coroutine coroutine = ((Script)script)
                            .CreateCoroutine(script.Globals[funcName])
                            .Coroutine;
                        DynValue value = coroutine.Resume(args2);
                        ResolveResume(player, coroutine, value);
                    }
                    catch(Exception e)
                    {
                        string message = e is ScriptRuntimeException scriptException
                            ? scriptException.DecoratedMessage
                            : e.Message;
                        TraceLuaFailure(player, target, funcName, luaPath, e, message);
                        Program.Log.Error("Lua function failed: player={0} actor={1} func={2} path={3}: {4}",
                            player != null ? player.customDisplayName : "(none)",
                            target.GetName(),
                            funcName,
                            luaPath,
                            message);
                        SendError(player, message);
                    }
                }
                else if (!optional)
                {
                    string message = String.Format(
                        "Could not find function '{0}' for actor {1}.",
                        funcName,
                        target.GetName());
                    Program.Log.Error("{0} Script path: {1}.", message, luaPath);
                    SendError(player, message);
                }
            }
            else if (!(target is Area) && !optional)
            {
                string message = String.Format(
                    "Could not find script for actor {0}.",
                    target.GetName());
                Program.Log.Error("{0} Requested path: {1}.", message, luaPath);
                SendError(player, message);
            }
        }

        private static void TraceLuaFailure(
            Player player,
            Actor target,
            string function,
            string path,
            Exception exception,
            string message)
        {
            DevDiagnostics.Trace(
                "lua.call.failed",
                "player", player == null ? "(none)" : player.customDisplayName,
                "actor", target == null ? "(none)" : target.GetName(),
                "actorId", target == null ? "" : String.Format("0x{0:X}", target.actorId),
                "actorType", target == null ? "" : target.GetType().Name,
                "function", function ?? "",
                "path", path ?? "",
                "exceptionType", exception == null ? "" : exception.GetType().FullName,
                "message", message ?? "");
        }

        internal static object[] ComposeActorFunctionArguments(
            Player player,
            Actor target,
            object[] args)
        {
            if (target == null)
                throw new ArgumentNullException(nameof(target));
            if (args == null)
                throw new ArgumentNullException(nameof(args));

            int prefixLength = player == null ? 1 : 2;
            object[] invocationArguments = new object[args.Length + prefixLength];
            if (player != null)
            {
                invocationArguments[0] = player;
                invocationArguments[1] = target;
            }
            else
            {
                invocationArguments[0] = target;
            }

            Array.Copy(args, 0, invocationArguments, prefixLength, args.Length);
            return invocationArguments;
        }

        public void EventStarted(Player player, Actor target, EventStartPacket eventStart)
        {
            // Base args: eventName + client luaParams (legacy Meteor shape:
            // only the eventName string is pushed; scripts route on it).
            List<LuaParam> lparams = new List<LuaParam>();
            lparams.AddRange(eventStart.luaParams);
            lparams.Insert(0, new LuaParam(2, eventStart.eventName));
            PlayerEventWaiter waiter;
            bool hadWaiter = TryTakePlayerEventWaiter(
                player,
                eventStart.ownerActorID,
                eventStart.eventName,
                eventStart.eventType,
                out waiter);
            Coroutine coroutine = hadWaiter ? waiter.Coroutine : null;
            if (hadWaiter)
            {
                try
                {
                    DevDiagnostics.Trace(
                        "lua.resume",
                        "player", player.customDisplayName,
                        "actor", String.Format("0x{0:X}", player.actorId),
                        "source", "event.start",
                        "eventName", eventStart.eventName,
                        "coroutine", coroutine.GetHashCode());
                    DynValue value = coroutine.Resume();
                    ResolveResume(null, coroutine, value);
                }
                catch (ScriptRuntimeException e)
                {
                    LuaEngine.SendError(player, String.Format("OnEventStarted: {0}", e.DecoratedMessage));
                }
            }
            else
            {
                DevDiagnostics.Trace(
                    "lua.dispatch",
                    "player", player.customDisplayName,
                    "actor", String.Format("0x{0:X}", player.actorId),
                    "target", target.GetName(),
                    "targetActor", String.Format("0x{0:X}", target.actorId),
                    "eventName", eventStart.eventName,
                    "isDirector", target is Director);

                if (target is Director)
                    ((Director)target).OnEventStart(player, LuaUtils.CreateLuaParamObjectList(lparams));
                else
                    CallLuaFunction(player, target, "onEventStarted", false, LuaUtils.CreateLuaParamObjectList(lparams));
            }
        }

        public DynValue ResolveResume(Player player, Coroutine coroutine, DynValue value)
        {
            if (value == null || value.IsVoid())
                return value;

            if (player != null && value.String != null && value.String.Equals("_WAIT_EVENT"))
            {
                DevDiagnostics.Trace(
                    "lua.wait",
                    "player", player.customDisplayName,
                    "actor", String.Format("0x{0:X}", player.actorId),
                    "waitType", "_WAIT_EVENT",
                    "coroutine", coroutine.GetHashCode());
                GetInstance().AddWaitEventCoroutine(player, coroutine);
            }
            else if (value.Tuple != null && value.Tuple.Length >= 1 && value.Tuple[0].String != null)
            {
                switch (value.Tuple[0].String)
                {
                    case "_WAIT_TIME":
                        DevDiagnostics.Trace(
                            "lua.wait",
                            "player", player == null ? "(none)" : player.customDisplayName,
                            "waitType", "_WAIT_TIME",
                            "seconds", value.Tuple.Length > 1 ? value.Tuple[1].Number : 0,
                            "coroutine", coroutine.GetHashCode());
                        GetInstance().AddWaitCoroutine(coroutine, (float)value.Tuple[1].Number);
                        break;
                    case "_WAIT_SIGNAL":
                        DevDiagnostics.Trace(
                            "lua.wait",
                            "player", player == null ? "(none)" : player.customDisplayName,
                            "waitType", "_WAIT_SIGNAL",
                            "signal", value.Tuple.Length > 1 ? value.Tuple[1].String : "",
                            "coroutine", coroutine.GetHashCode());
                        GetInstance().AddWaitSignalCoroutine(coroutine, (string)value.Tuple[1].String);
                        break;
                    case "_WAIT_EVENT":
                        Player waitingPlayer = (Player)value.Tuple[1].UserData.Object;
                        uint expectedOwner = value.Tuple.Length > 2 && value.Tuple[2].Type == DataType.Number ? (uint)value.Tuple[2].Number : 0;
                        string expectedName = value.Tuple.Length > 3 && value.Tuple[3].Type == DataType.String ? value.Tuple[3].String : "";
                        byte expectedType = value.Tuple.Length > 4 && value.Tuple[4].Type == DataType.Number ? (byte)value.Tuple[4].Number : (byte)0;
                        DevDiagnostics.Trace(
                            "lua.wait",
                            "player", waitingPlayer.customDisplayName,
                            "actor", String.Format("0x{0:X}", waitingPlayer.actorId),
                            "waitType", "_WAIT_EVENT",
                            "expectedOwner", String.Format("0x{0:X}", expectedOwner),
                            "expectedName", expectedName,
                            "expectedType", expectedType,
                            "coroutine", coroutine.GetHashCode());
                        GetInstance().AddWaitEventCoroutine(waitingPlayer, coroutine, expectedOwner, expectedName, expectedType);
                        break;
                    default:
                        return value;
                }
            }

            return value;
        }

        #region RunGMCommand
        public static void RunGMCommand(Player player, String cmd, string[] param, bool help = false)
        {
            bool playerNull = player == null;

            if (playerNull)
            {
                if (param.Length >= 2 && param[1].Contains("\""))
                    player = Server.GetWorldManager().GetPCInWorld(param[1]);
                else if (param.Length > 2)
                    player = Server.GetWorldManager().GetPCInWorld(param[1] + param[2]);
            }

            if (playerNull && param.Length >= 3)
                player = Server.GetWorldManager().GetPCInWorld(param[1] + " " + param[2]);
            
            // load from scripts/commands/gm/ directory
            var path = String.Format("./scripts/commands/gm/{0}.lua", cmd.ToLower());

            // check if the file exists
            if (File.Exists(path))
            {
                // load global functions
                LuaScript script = LoadGlobals();

                // see if this script has any syntax errors
                try
                {
                    script.DoFile(path);
                }
                catch (Exception e)
                {
                    Program.Log.Error("LuaEngine.RunGMCommand: {0}.", e.Message);
                    return;
                }

                // can we run this script
                if (!script.Globals.Get("onTrigger").IsNil())
                {
                    // can i run this command
                    var permissions = 0;

                    // parameter types (string, integer, double, float)
                    var parameters = "";
                    var description = "!" + cmd + ": ";

                    // get the properties table
                    var res = script.Globals.Get("properties");

                    // make sure properties table exists
                    if (!res.IsNil())
                    {
                        try
                        {
                            // returns table if one is found
                            var table = res.Table;

                            // find each key/value pair
                            foreach (var pair in table.Pairs)
                            {
                                if (pair.Key.String == "permissions")
                                {
                                    permissions = (int)pair.Value.Number;
                                }
                                else if (pair.Key.String == "parameters")
                                {
                                    parameters = pair.Value.String;
                                }
                                else if (pair.Key.String == "description")
                                {
                                    description = pair.Value.String;
                                }
                            }
                        }
                        catch (Exception e) { LuaScript.Log.Error("LuaEngine.RunGMCommand: " + e.Message); return; }
                    }

                    // if this isnt a console command, make sure player exists
                    if (player != null)
                    {
                        if (permissions > 0 && !player.isGM)
                        {
                            Program.Log.Info("LuaEngine.RunGMCommand: {0}'s GM level is too low to use command {1}.", player.actorName, cmd);
                            player.SendMessage(SendMessagePacket.MESSAGE_TYPE_SYSTEM_ERROR, "[Commands]", "This command requires GM access.");
                            return;
                        }
                        // i hate to do this, but cant think of a better way to keep !help
                        else if (help)
                        {
                            player.SendMessage(SendMessagePacket.MESSAGE_TYPE_SYSTEM_ERROR, String.Format("[Commands] [{0}]", cmd), description);
                            return;
                        }
                    }
                    else if (help)
                    {
                        LuaScript.Log.Info("[Commands] [{0}]: {1}", cmd, description);
                        return;
                    }

                    // we'll push our lua params here
                    List<object> LuaParam = new List<object>();

                    var i = playerNull ? 2 : 0;
                    for (; i < parameters.Length; ++i)
                    {
                        try
                        {
                            // convert chat parameters to command parameters
                            switch (parameters[i])
                            {
                                case 'i':
                                    LuaParam.Add(Convert.ChangeType(param[i + 1], typeof(int)));
                                    continue;
                                case 'd':
                                    LuaParam.Add(Convert.ChangeType(param[i + 1], typeof(double)));
                                    continue;
                                case 'f':
                                    LuaParam.Add(Convert.ChangeType(param[i + 1], typeof(float)));
                                    continue;
                                case 's':
                                    LuaParam.Add(param[i + 1]);
                                    continue;
                                default:
                                    LuaScript.Log.Info("LuaEngine.RunGMCommand: {0} unknown parameter {1}.", path, parameters[i]);
                                    LuaParam.Add(param[i + 1]);
                                    continue;
                            }
                        }
                        catch (Exception e)
                        {
                            if (e is IndexOutOfRangeException) break;
                            LuaParam.Add(param[i + 1]);
                        }
                    }

                    // the script can double check the player exists, we'll push them anyways
                    LuaParam.Insert(0, player);
                    // push the arg count too
                    LuaParam.Insert(1, i - (playerNull ? 2 : 0));

                    // run the script                    
                    //script.Call(script.Globals["onTrigger"], LuaParam.ToArray());

                    // gm commands dont need to be coroutines?
                    try
                    {
                        Coroutine coroutine = script.CreateCoroutine(script.Globals["onTrigger"]).Coroutine;
                        DynValue value = coroutine.Resume(LuaParam.ToArray());
                        GetInstance().ResolveResume(player, coroutine, value);
                    }
                    catch (Exception e)
                    {
                        Program.Log.Error("LuaEngine.RunGMCommand: {0} - {1}", path, e.Message);
                    }
                    return;
                }
            }
            LuaScript.Log.Error("LuaEngine.RunGMCommand: Unable to find script {0}", path);
            return;
        }
        #endregion

        public static LuaScript LoadScript(string path)
        {
            if (!File.Exists(path))
                return null;

            LuaScript script = LoadGlobals();

            try
            {
                ((Script)script).DoFile(path);
            }
            catch (InterpreterException e)
            {
                Program.Log.Error("{0}.", e.DecoratedMessage);
                return null;
            }
            catch (Exception e)
            {
                Program.Log.Error("Could not load Lua script {0}: {1}", path, e.Message);
                return null;
            }
            return script;
        }

        public static LuaScript LoadGlobals(LuaScript script = null)
        {
            script = script ?? new LuaScript();

            // register and load all global functions here
            ((FileSystemScriptLoader)script.Options.ScriptLoader).ModulePaths = FileSystemScriptLoader.UnpackStringPaths("./scripts/?;./scripts/?.lua");
            script.Globals["GetWorldManager"] = (Func<WorldManager>)Server.GetWorldManager;
            script.Globals["GetStaticActor"] = (Func<string, Actor>)Server.GetStaticActors;
            script.Globals["GetStaticActorById"] = (Func<uint, Actor>)Server.GetStaticActors;
            script.Globals["GetWorldMaster"] = (Func<Actor>)Server.GetWorldManager().GetActor;
            script.Globals["GetItemGamedata"] = (Func<uint, ItemData>)Server.GetItemGamedata;
            script.Globals["GetGuildleveGamedata"] = (Func<uint, GuildleveData>)Server.GetGuildleveGamedata;
            script.Globals["GetLuaInstance"] = (Func<LuaEngine>)LuaEngine.GetInstance;

            script.Options.DebugPrint = s => { Program.Log.Debug(s); };
            return script;
        }

        public static void SendError(Player player, string message)
        {
            message = "[LuaError] " + message;
            if (player == null)
                return;
            player.SendMessage(SendMessagePacket.MESSAGE_TYPE_SYSTEM_ERROR, "", message);
            player.EndEvent();
        }

    }
}
