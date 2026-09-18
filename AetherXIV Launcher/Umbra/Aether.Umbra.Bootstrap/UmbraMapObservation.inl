// Read-only diagnostic observation at the verified map update call site.
// Included inside dllmain.cpp's anonymous namespace. No visibility or world/
// screen transform is inferred from a captured control. Never used to move actors.
namespace MapObservation
{
    constexpr DWORD Bytes = 0xa70;
    struct Snapshot { DWORD size, control, ticks, sequence; BYTE bytes[Bytes]; };
    SRWLOCK gate = SRWLOCK_INIT;
    Snapshot snapshot{};
    using Update = void (__thiscall*)(void*);
    Update original = nullptr;
    bool installed = false;
    volatile LONG requested = 0, requestedAt = 0;

    void __fastcall Capture(void* self, void*)
    {
        original(self);
        if (!InterlockedCompareExchange(&requested, 0, 0) ||
            GetTickCount() - static_cast<DWORD>(InterlockedCompareExchange(&requestedAt, 0, 0)) > 5000) return;
        Snapshot next{};
        SIZE_T count = 0;
        if (!ReadProcessMemory(GetCurrentProcess(), self, next.bytes, Bytes, &count) || count != Bytes)
            return;
        BYTE* base = reinterpret_cast<BYTE*>(GetModuleHandleW(nullptr));
        const auto word = [&](DWORD offset) { return *reinterpret_cast<const DWORD*>(next.bytes + offset); };
        if (word(0) != reinterpret_cast<DWORD>(base + 0xbc358c)
            || word(4) != reinterpret_cast<DWORD>(base + 0xf3f970)
            || word(0xb4) != reinterpret_cast<DWORD>(base + 0xbc3464)
            || word(0x194) != reinterpret_cast<DWORD>(base + 0xbc3450)) return;
        next.size = sizeof(next);
        next.control = reinterpret_cast<DWORD>(self);
        next.ticks = GetTickCount();
        // Diagnostics must not stall the game thread behind a bridge reader.
        if (!TryAcquireSRWLockExclusive(&gate)) return;
        next.sequence = snapshot.sequence + 1;
        snapshot = next;
        ReleaseSRWLockExclusive(&gate);
    }

    bool Read(Snapshot* output)
    {
        if (!output || !installed) return false;
        // Reached only through the authenticated developer endpoint. Capture is
        // demand-driven for five seconds, independent of native settings UI state.
        InterlockedExchange(&requestedAt, static_cast<LONG>(GetTickCount()));
        InterlockedExchange(&requested, 1);
        AcquireSRWLockShared(&gate);
        *output = snapshot;
        ReleaseSRWLockShared(&gate);
        return output->sequence != 0;
    }

    bool Start()
    {
        if (installed) return true;
        // Main-menu startup already validates this build and pins the module.
        if (!LuaMainMenu::installed) return false;
        BYTE* base = reinterpret_cast<BYTE*>(GetModuleHandleW(nullptr));
        // Complete CALL instruction in MapScreenControl update (0x679940).
        // Its original target is the thiscall, zero-argument routine 0x6795d0.
        const BYTE expected[] = {0x8b,0xce,0xe8,0x25,0xfa,0xff,0xff,0x39,0xbe,0x48,0x04,0x00,0x00};
        if (memcmp(base + 0x279ba4, expected, sizeof(expected)) != 0) return false;
        original = reinterpret_cast<Update>(base + 0x2795d0);
        installed = WriteRelativeCall(base + 0x279ba6, reinterpret_cast<void*>(&Capture));
        return installed;
    }
}
