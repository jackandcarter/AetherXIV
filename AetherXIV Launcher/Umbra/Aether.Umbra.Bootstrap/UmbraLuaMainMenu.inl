// Framework-owned MainMenuWidget binding for the verified 1.23b client.
// Included inside dllmain.cpp's anonymous namespace. No client assets are edited.
namespace LuaMainMenu
{
    using State = void;
    using Callback = int (__cdecl*)(State*);
    using Call = void (__cdecl*)(State*, void*, int);
    struct Api
    {
        const char* (__cdecl* getupvalue)(State*, int, int);
        const char* (__cdecl* setupvalue)(State*, int, int);
        int (__cdecl* error)(State*);
        int (__cdecl* gettop)(State*);
        void (__cdecl* settop)(State*, int);
        int (__cdecl* checkstack)(State*, int);
        void (__cdecl* pushvalue)(State*, int);
        int (__cdecl* type)(State*, int);
        const void* (__cdecl* topointer)(State*, int);
        const char* (__cdecl* tolstring)(State*, int, unsigned int*);
        double (__cdecl* tonumber)(State*, int);
        void (__cdecl* pushlightuserdata)(State*, void*);
        void* (__cdecl* touserdata)(State*, int);
        void (__cdecl* pushstring)(State*, const char*);
        void (__cdecl* pushnumber)(State*, double);
        void (__cdecl* pushcclosure)(State*, Callback, int);
        void (__cdecl* getfield)(State*, int, const char*);
        void (__cdecl* setfield)(State*, int, const char*);
        void (__cdecl* call)(State*, int, int);
        int (__cdecl* pcall)(State*, int, int, int);
        int (__cdecl* cpcall)(State*, Callback, void*);
    } api{};
    constexpr int Globals = -10002;
    constexpr int Original = Globals - 1;
    constexpr int Row = 19;
    Call originalCall = nullptr;
    BYTE* target = nullptr;
    BYTE saved[5]{};
    volatile LONG rootHits = 0, bound = 0, rowHits = 0, selectionHits = 0;
    volatile LONG errors = 0, openPending = 0, observedCount = -1;
    bool installed = false;
    volatile LONG bindStage = 0, classType = -1, initType = -1, lastState = 0, lastClass = 0, lastInit = 0;

    bool Readable(const void* ptr, SIZE_T size)
    {
        MEMORY_BASIC_INFORMATION info{};
        if (!ptr || !VirtualQuery(ptr, &info, sizeof(info)) || info.State != MEM_COMMIT
            || (info.Protect & (PAGE_NOACCESS | PAGE_GUARD))) return false;
        ULONG_PTR begin = reinterpret_cast<ULONG_PTR>(ptr);
        ULONG_PTR end = reinterpret_cast<ULONG_PTR>(info.BaseAddress) + info.RegionSize;
        return begin <= end && size <= end - begin;
    }
    DWORD Word(const BYTE* ptr, unsigned int offset)
    {
        return *reinterpret_cast<const DWORD*>(ptr + offset);
    }
    DWORD HashBytes(DWORD hash, const BYTE* bytes, unsigned int count)
    {
        for (unsigned int i = 0; i < count; ++i) hash = (hash ^ bytes[i]) * 16777619u;
        return hash;
    }
    // Fingerprint every opcode and constant, not just source lines (which collide).
    bool Matches(const void* closure, int first, int last, int params,
                 int instructions, int constants, DWORD expected)
    {
        auto c = static_cast<const BYTE*>(closure);
        if (!Readable(c, 0x14) || c[6] != 0) return false;
        auto p = *reinterpret_cast<const BYTE* const*>(c + 0x10);
        if (!Readable(p, 0x4c) || Word(p, 0x3c) != static_cast<DWORD>(first)
            || Word(p, 0x40) != static_cast<DWORD>(last) || p[0x49] != params
            || Word(p, 0x2c) != static_cast<DWORD>(instructions)
            || Word(p, 0x28) != static_cast<DWORD>(constants)) return false;
        auto code = *reinterpret_cast<const BYTE* const*>(p + 0x0c);
        auto k = *reinterpret_cast<const BYTE* const*>(p + 0x08);
        if (!Readable(code, instructions * 4) || !Readable(k, constants * 16)) return false;
        DWORD hash = HashBytes(2166136261u, code, instructions * 4);
        for (int i = 0; i < constants; ++i)
        {
            const BYTE* v = k + i * 16;
            DWORD tag = Word(v, 8);
            if (tag != 0 && tag != 1 && tag != 3 && tag != 4) return false;
            BYTE t = static_cast<BYTE>(tag);
            hash = HashBytes(hash, &t, 1);
            if (tag == 3) hash = HashBytes(hash, v, 8);
            if (tag == 1) { BYTE b = Word(v, 0) != 0; hash = HashBytes(hash, &b, 1); }
            if (tag == 4)
            {
                auto s = *reinterpret_cast<const BYTE* const*>(v);
                if (!Readable(s, 16)) return false;
                DWORD length = Word(s, 12);
                if (length > 4096 || !Readable(s + 16, length + 1)) return false;
                hash = HashBytes(hash, s + 16, length + 1);
            }
        }
        return hash == expected;
    }
    void Pop(State* L) { api.settop(L, -2); }
    bool Method(State* L, const char* name)
    {
        api.getfield(L, 1, name);
        if (api.type(L, -1) != 6) { Pop(L); InterlockedIncrement(&errors); return false; }
        api.pushvalue(L, 1);
        return true;
    }
    bool Finish(State* L, int args, int results = 0)
    {
        if (api.pcall(L, args, results, 0) == 0) return true;
        Pop(L);
        InterlockedIncrement(&errors);
        return false;
    }
    const char* RowLabel(int row)
    { return row == Row ? "Umbra Plugin Manager" : "Umbra Settings"; }
    bool Property(State* L, int row, const char* key, const char* value)
    {
        if (!Method(L, "setListProperty")) return false;
        api.pushstring(L, "MainMenu"); api.pushnumber(L, row);
        api.pushstring(L, key); api.pushstring(L, value);
        return Finish(L, 5);
    }
    bool HasRow(State* L, int row)
    {
        if (!Method(L, "getListProperty")) return false;
        api.pushstring(L, "MainMenu"); api.pushnumber(L, row); api.pushstring(L, "MainName");
        if (!Finish(L, 4, 1)) return false;
        const char* label = api.type(L, -1) == 4 ? api.tolstring(L, -1, nullptr) : nullptr;
        bool matches = label && lstrcmpA(label, RowLabel(row)) == 0;
        Pop(L);
        return matches;
    }
    struct RowAttempt { int inserted; };
    int __cdecl AddRow(State* L)
    {
        if (!api.checkstack(L, 12) || !Method(L, "getListPropertyCount")) return 0;
        api.pushstring(L, "MainMenu");
        if (!Finish(L, 2, 1)) return 0;
        int count = api.type(L, -1) == 3 ? static_cast<int>(api.tonumber(L, -1)) : -1;
        Pop(L);
        InterlockedExchange(&observedCount, count);
        if (count == Row + 2)
        {
            if (!HasRow(L, Row) || !HasRow(L, Row + 1)) return 0;
            api.pushnumber(L, 1); return 1;
        }
        if (count != Row) { InterlockedIncrement(&errors); return 0; }
        auto attempt = static_cast<RowAttempt*>(api.touserdata(L, 2));
        for (int row = Row; row < Row + 2; ++row)
        {
            if (!Method(L, "insertListProperty")) return 0;
            api.pushstring(L, "MainMenu"); api.pushnumber(L, row);
            if (!Finish(L, 3)) return 0;
            ++attempt->inserted;
            if (!Property(L, row, "MainName", RowLabel(row))
                || !Property(L, row, "MainEnable", "True")
                || !Property(L, row, "visibility", "Visible")) return 0;
        }
        if (!Method(L, "updateListProperty")) return 0;
        api.pushstring(L, "MainMenu");
        if (!Finish(L, 2)) return 0;
        if (!HasRow(L, Row) || !HasRow(L, Row + 1))
        { InterlockedIncrement(&errors); return 0; }
        InterlockedIncrement(&rowHits);
        api.pushnumber(L, 1); return 1;
    }
    int __cdecl RemoveRow(State* L)
    {
        auto attempt = static_cast<RowAttempt*>(api.touserdata(L, 2));
        while (attempt->inserted > 0)
        {
            if (!Method(L, "deleteListProperty")) return 0;
            api.pushstring(L, "MainMenu"); api.pushnumber(L, Row + attempt->inserted - 1);
            if (!Finish(L, 3)) return 0;
            --attempt->inserted;
        }
        if (Method(L, "updateListProperty"))
        { api.pushstring(L, "MainMenu"); Finish(L, 2); }
        return 0;
    }
    int Forward(State* L)
    {
        int args = api.gettop(L);
        if (!api.checkstack(L, args + 2))
        { api.pushstring(L, "Umbra menu: insufficient Lua stack"); return api.error(L); }
        api.pushvalue(L, Original);
        for (int i = 1; i <= args; ++i) api.pushvalue(L, i);
        // Let original errors propagate to the game's existing protected caller.
        api.call(L, args, -1);
        return api.gettop(L) - args;
    }
    int __cdecl Init(State* L)
    {
        int results = Forward(L);
        int top = api.gettop(L);
        RowAttempt attempt{};
        api.pushcclosure(L, AddRow, 0); api.pushvalue(L, 1);
        api.pushlightuserdata(L, &attempt);
        bool success = Finish(L, 2, 1) && api.type(L, -1) == 3 && api.tonumber(L, -1) == 1;
        api.settop(L, top);
        if (!success && attempt.inserted)
        {
            api.pushcclosure(L, RemoveRow, 0); api.pushvalue(L, 1);
            api.pushlightuserdata(L, &attempt);
            Finish(L, 2);
            api.settop(L, top);
        }
        return results;
    }
    int __cdecl Activate(State* L)
    {
        int row = static_cast<int>(api.tonumber(L, 2));
        if ((row != Row && row != Row + 1) || !HasRow(L, row)) return 0;
        if (Method(L, "hide")) Finish(L, 1);
        InterlockedIncrement(&selectionHits);
        InterlockedExchange(&openPending, row == Row ? 1 : 2);
        return 0;
    }
    int __cdecl Selection(State* L)
    {
        const char* control = api.type(L, 3) == 4 ? api.tolstring(L, 3, nullptr) : nullptr;
        double row = api.type(L, 4) == 3 ? api.tonumber(L, 4) : -1;
        bool ours = control && lstrcmpA(control, "ListBox_MainMenu") == 0
            && (row == Row || row == Row + 1);
        int results = Forward(L);
        if (ours)
        {
            int top = api.gettop(L);
            api.pushcclosure(L, Activate, 0); api.pushvalue(L, 1); api.pushnumber(L, row);
            Finish(L, 2);
            api.settop(L, top);
        }
        return results;
    }
    bool PushWrappedMethod(State* L, int index, int first, int last, int params,
                           int instructions, int constants, DWORD fingerprint)
    {
        auto closure = static_cast<const BYTE*>(api.topointer(L, index));
        auto base = reinterpret_cast<const BYTE*>(GetModuleHandleW(nullptr));
        // Verified engine method wrapper: userdata upvalue 1 owns dispatch state;
        // upvalue 2 roots the exact Lua method. Keep the wrapper intact.
        if (!Readable(closure, 56) || closure[6] != 1 || closure[7] != 2
            || *reinterpret_cast<const BYTE* const*>(closure + 16) != base + 0x907f20
            || Word(closure, 32) != 7 || Word(closure, 48) != 6) return false;
        if (!api.getupvalue(L, index, 2)) return false;
        return Matches(api.topointer(L, -1), first, last, params, instructions, constants, fingerprint);
    }
    int __cdecl Bind(State* L)
    {
        InterlockedExchange(&lastState, reinterpret_cast<LONG>(L));
        InterlockedExchange(&bindStage, 1);
        if (!api.checkstack(L, 12)) return 0;
        api.getfield(L, Globals, "MainMenuWidget");
        int kind = api.type(L, -1);
        InterlockedExchange(&classType, kind);
        InterlockedExchange(&lastClass, reinterpret_cast<LONG>(api.topointer(L, -1)));
        if (kind != 5) return 0;
        InterlockedExchange(&bindStage, 2);
        int cls = api.gettop(L);
        api.getfield(L, cls, "init");
        InterlockedExchange(&initType, api.type(L, -1));
        InterlockedExchange(&lastInit, reinterpret_cast<LONG>(api.topointer(L, -1)));
        if (!PushWrappedMethod(L, cls + 1, 63, 169, 1, 329, 99, 0x72f4b5c0)) return 0;
        InterlockedExchange(&bindStage, 3);
        api.getfield(L, cls, "processUICommandSelectionChanged");
        if (!PushWrappedMethod(L, cls + 3, 182, 289, 5, 240, 55, 0x66008f27)) return 0;
        // Allocate both callbacks before mutation. Lua's setupvalue maintains GC
        // write barriers; the engine dispatcher and its userdata remain unchanged.
        api.pushvalue(L, cls + 4);
        api.pushcclosure(L, Selection, 1);
        api.pushvalue(L, cls + 2);
        api.pushcclosure(L, Init, 1);
        api.setupvalue(L, cls + 1, 2);
        api.setupvalue(L, cls + 3, 2);
        InterlockedIncrement(&bound);
        InterlockedExchange(&bindStage, 4);
        return 0;
    }
    // Map lifecycle diagnostics share the existing Lua dispatcher. No retained
    // Lua object pointers, second detour, or change to the map's input behavior.
    namespace MapLifecycle
    {
        volatile LONG bound = 0, opened = 0, closed = 0, active = 0, generation = 0;
        int __cdecl Init(State* L)
        {
            int results = Forward(L);
            InterlockedIncrement(&opened);
            InterlockedIncrement(&generation);
            InterlockedExchange(&active, 1);
            return results;
        }
        int __cdecl Closing(State* L)
        {
            // Invalidate before forwarding, including when the original errors.
            InterlockedExchange(&active, 0);
            InterlockedIncrement(&closed);
            InterlockedIncrement(&generation);
            return Forward(L);
        }
        int __cdecl Bind(State* L)
        {
            if (!api.checkstack(L, 12)) return 0;
            api.getfield(L, Globals, "MapNavigationWidget");
            if (api.type(L, -1) != 5) return 0;
            int cls = api.gettop(L);
            api.getfield(L, cls, "init");
            if (!PushWrappedMethod(L, cls + 1, 94, 203, 4, 143, 43, 0xa3ba29b2)) return 0;
            api.getfield(L, cls, "processClosing");
            if (!PushWrappedMethod(L, cls + 3, 342, 348, 1, 9, 3, 0x475fe917)) return 0;
            api.pushvalue(L, cls + 4);
            api.pushcclosure(L, Closing, 1);
            api.pushvalue(L, cls + 2);
            api.pushcclosure(L, Init, 1);
            api.setupvalue(L, cls + 1, 2);
            api.setupvalue(L, cls + 3, 2);
            InterlockedIncrement(&bound);
            return 0;
        }
    }
    void __cdecl Dispatch(State* L, void* function, int results)
    {
        const BYTE* value = static_cast<const BYTE*>(function);
        bool menu = Readable(value, 16) && Word(value, 8) == 6
            && Matches(*reinterpret_cast<void* const*>(value), 0, 0, 0, 74, 27, 0xb3334122);
        bool map = Readable(value, 16) && Word(value, 8) == 6
            && Matches(*reinterpret_cast<void* const*>(value), 0, 0, 0, 140, 49, 0x5a666978);
        originalCall(L, function, results);
        if (menu)
        {
            InterlockedIncrement(&rootHits);
            int top = api.gettop(L);
            if (api.cpcall(L, Bind, nullptr) != 0) InterlockedIncrement(&errors);
            api.settop(L, top);
        }
        if (map)
        {
            InterlockedExchange(&MapLifecycle::active, 0);
            InterlockedIncrement(&MapLifecycle::generation);
            int top = api.gettop(L);
            if (api.cpcall(L, MapLifecycle::Bind, nullptr) != 0) InterlockedIncrement(&errors);
            api.settop(L, top);
        }
    }
    bool Start()
    {
        if (installed) return true;
        BYTE* base = reinterpret_cast<BYTE*>(GetModuleHandleW(nullptr));
        auto dos = reinterpret_cast<IMAGE_DOS_HEADER*>(base);
        if (!base || dos->e_magic != IMAGE_DOS_SIGNATURE) return false;
        auto nt = reinterpret_cast<IMAGE_NT_HEADERS*>(base + dos->e_lfanew);
        if (nt->Signature != IMAGE_NT_SIGNATURE || nt->FileHeader.TimeDateStamp != 0x504f671f
            || nt->OptionalHeader.SizeOfImage != 0x00f99000) return false;
        // Entire instruction boundaries; the trampoline contains no relative operand.
        const BYTE prologue[] = {0x56, 0x8b, 0x74, 0x24, 0x08, 0x66, 0x83, 0x46, 0x34, 0x01};
        target = base + 0x9cfc30;
        if (memcmp(target, prologue, sizeof(prologue)) != 0) return false;
        // API entry signatures are generated/verified against the same local client.
#include "UmbraLuaApiBindings.inl"
        BYTE* trampoline = static_cast<BYTE*>(VirtualAlloc(nullptr, 16,
            MEM_COMMIT | MEM_RESERVE, PAGE_EXECUTE_READWRITE));
        if (!trampoline) return false;
        memcpy(saved, target, sizeof(saved)); memcpy(trampoline, saved, sizeof(saved));
        trampoline[5] = 0xe9;
        *reinterpret_cast<DWORD*>(trampoline + 6) = static_cast<DWORD>(target + 5 - (trampoline + 10));
        FlushInstructionCache(GetCurrentProcess(), trampoline, 10);
        HMODULE pinnedModule = nullptr;
        if (!GetModuleHandleExW(GET_MODULE_HANDLE_EX_FLAG_FROM_ADDRESS | GET_MODULE_HANDLE_EX_FLAG_PIN,
            reinterpret_cast<LPCWSTR>(&Dispatch), &pinnedModule))
        { VirtualFree(trampoline, 0, MEM_RELEASE); return false; }
        originalCall = reinterpret_cast<Call>(trampoline);
        BYTE patch[5] = {0xe9};
        *reinterpret_cast<DWORD*>(patch + 1) = static_cast<DWORD>(reinterpret_cast<BYTE*>(&Dispatch) - (target + 5));
        if (!WriteProtectedBytes(target, patch, sizeof(patch)))
        { originalCall = nullptr; VirtualFree(trampoline, 0, MEM_RELEASE); return false; }
        installed = true;
        return true;
    }
    // Installed before the game thread resumes. Keep callbacks/trampoline alive
    // until process exit: Lua closures retain pointers into this module.
    void Poll()
    {
        LONG request = InterlockedExchange(&openPending, 0);
        if (request)
        {
            PluginInstallerOpen = true;
            PluginManagerSettingsRequestPending = request == 2;
            PluginManagerUpdatesRequestPending = false;
        }
        static LONG last[7] = {-1, -1, -1, -1, -1, -2, -1};
        LONG now[] = {rootHits, bound, rowHits, selectionHits, errors, observedCount, bindStage};
        if (memcmp(last, now, sizeof(now)) == 0) return;
        memcpy(last, now, sizeof(now));
        HANDLE log = OpenBootstrapLog();
        AppendLogUInt(log, L"umbra_lua_menu_root_hits", now[0]);
        AppendLogUInt(log, L"umbra_lua_menu_bound", now[1]);
        AppendLogUInt(log, L"umbra_lua_menu_rows_verified", now[2]);
        AppendLogUInt(log, L"umbra_lua_menu_selected", now[3]);
        AppendLogUInt(log, L"umbra_lua_menu_errors", now[4]);
        AppendLogUInt(log, L"umbra_lua_menu_original_count", now[5]);
        AppendLogUInt(log, L"umbra_lua_menu_bind_stage", bindStage);
        AppendLogUInt(log, L"umbra_lua_menu_class_type", classType);
        AppendLogUInt(log, L"umbra_lua_menu_init_type", initType);
        AppendLogHex(log, L"umbra_lua_menu_state", lastState);
        AppendLogHex(log, L"umbra_lua_menu_class", lastClass);
        AppendLogHex(log, L"umbra_lua_menu_init", lastInit);
        if (log != INVALID_HANDLE_VALUE) CloseHandle(log);
    }
}
