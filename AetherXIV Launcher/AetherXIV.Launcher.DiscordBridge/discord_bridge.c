/*
 * AetherXIV Discord Bridge
 * Copyright (C) 2026 Demi Dev Unit
 *
 * This file is part of AetherXIV.
 * See THIRD_PARTY_NOTICES.md for historical and third-party attribution.
 *
 * AetherXIV is free software: you may redistribute it and/or modify it
 * under the terms of the GNU Affero General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 *
 * SPDX-License-Identifier: AGPL-3.0-or-later
 *
 * Serves the Windows named pipe \\.\pipe\discord-ipc-0 inside a Wine prefix
 * and relays every byte to Discord's host Unix domain socket
 * (<host tmp dir>/discord-ipc-0). This lets the in-game Umbra Discord Rich
 * Presence plugin reach the host Discord client while the FFXIV 1.x client
 * runs under Wine on macOS and Linux.
 *
 * Why raw syscalls: Wine cannot hand a Windows program a Unix socket. Its
 * ws2_32 has no AF_UNIX support on the Wine 11 runtime AetherXIV ships (and
 * macOS Wine has none at all), so the Unix-side socket is opened with raw
 * x86-64 `syscall` instructions. Those bypass Wine's API interception and are
 * serviced directly by the host kernel. The technique is the one pioneered by
 * the wine-discord-ipc-bridge family (and shipped by XIV on Mac); this
 * implementation is original AetherXIV code.
 *
 * The bridge marks itself as a Wine "system process" (the Wine-specific
 * NtSetInformationProcess class 1000) so it survives the launcher helper that
 * spawned it and lives as long as the prefix.
 *
 * Build (Linux/SteamOS numbers, no define):
 *   x86_64-w64-mingw32-gcc -O2 -Wall -masm=intel -static \
 *     -o AetherXIV.DiscordBridge.exe discord_bridge.c
 *
 * Build (macOS numbers):
 *   ... -DAETHERXIV_BRIDGE_MACOS -o AetherXIV.DiscordBridge.exe discord_bridge.c
 */

#include <windows.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <stdarg.h>
#include <stdint.h>

#define BRIDGE_BUFFER_SIZE 2048
#define BRIDGE_PIPE_NAME L"\\\\.\\pipe\\discord-ipc-0"
#define BRIDGE_MAX_PIPE_NUMBER 9
#define BRIDGE_MAX_SOCKET_ATTEMPTS 20
#define BRIDGE_MAX_SOCKET_PATH 128
#define BRIDGE_SOCKET_PATH_CAPACITY 108 /* struct sockaddr_un sun_path on Linux and macOS */

/* ------------------------------------------------------------------ */
/* Host syscall numbers (x86-64). macOS syscalls carry the 0x2000000  */
/* class bit, which the raw syscall thunk adds at runtime.            */
/* ------------------------------------------------------------------ */

#if defined(AETHERXIV_BRIDGE_MACOS)
#define BRIDGE_SYS_CLASS_BIT 0x2000000
#define BRIDGE_SYS_READ 0x03
#define BRIDGE_SYS_WRITE 0x04
#define BRIDGE_SYS_CLOSE 0x06
#define BRIDGE_SYS_SOCKET 0x61
#define BRIDGE_SYS_CONNECT 0x62
#define BRIDGE_SYS_GETPID 0x14
#else
#define BRIDGE_SYS_CLASS_BIT 0
#define BRIDGE_SYS_READ 0
#define BRIDGE_SYS_WRITE 1
#define BRIDGE_SYS_CLOSE 3
#define BRIDGE_SYS_SOCKET 41
#define BRIDGE_SYS_CONNECT 42
#define BRIDGE_SYS_GETPID 39
#endif

/*
 * Direct host syscall with three arguments. The Linux ABI (and the macOS
 * variant of it) places the first three arguments in rdi, rsi, rdx, and the
 * syscall number in rax; r10 would carry a fourth argument, which none of the
 * syscalls used here need. The kernel reports failure by setting the carry
 * flag and returning the negated errno, which this thunk turns into a
 * positive errno, matching the Unix convention.
 */
static intptr_t raw_syscall(intptr_t number, intptr_t a1, intptr_t a2, intptr_t a3)
{
    intptr_t result;
    register intptr_t sysno asm("rax") = number;
    register intptr_t arg1 asm("rdi") = a1;
    register intptr_t arg2 asm("rsi") = a2;
    register intptr_t arg3 asm("rdx") = a3;
    __asm__ volatile(
        "add rax, %[class_bit]\n\t"
        "syscall\n\t"
        "jnc 1f\n\t"
        "neg rax\n\t"
        "1:\n\t"
        : "=a"(result)
        : "a"(sysno), "r"(arg1), "r"(arg2), "r"(arg3),
          [class_bit] "i"(BRIDGE_SYS_CLASS_BIT)
        : "rcx", "r11", "memory");
    return result;
}

/* ------------------------------------------------------------------ */
/* Diagnostics: a small log file next to Wine's temp directory.       */
/* ------------------------------------------------------------------ */

static FILE* g_log = NULL;

static void log_open(void)
{
    char temp[MAX_PATH];
    if (!GetTempPathA((DWORD)sizeof(temp), temp))
        return;
    char path[MAX_PATH + 64];
    snprintf(path, sizeof(path), "%sAetherXIV.DiscordBridge.log", temp);
    g_log = fopen(path, "a");
}

static void log_close(void)
{
    if (g_log)
    {
        fclose(g_log);
        g_log = NULL;
    }
}

static void log_printf(const char* fmt, ...)
{
    if (!g_log)
        return;
    va_list args;
    va_start(args, fmt);
    fprintf(g_log, "[aetherxiv-discord-bridge] ");
    vfprintf(g_log, fmt, args);
    fprintf(g_log, "\n");
    fflush(g_log);
    va_end(args);
}

/* ------------------------------------------------------------------ */
/* Wine "system process" so the bridge outlives its launcher helper.  */
/* ------------------------------------------------------------------ */

static void make_wine_system_process(void)
{
    HMODULE ntdll = GetModuleHandleW(L"ntdll.dll");
    if (!ntdll)
    {
        log_printf("wine_system_process_skipped ntdll_missing");
        return;
    }
    FARPROC proc = GetProcAddress(ntdll, "NtSetInformationProcess");
    if (!proc)
    {
        log_printf("wine_system_process_skipped routine_missing");
        return;
    }

    /* Wine-specific process information class 1000: keep this process alive
     * as long as the prefix instead of cleaning it up with its parent. */
    const int ProcessWineMakeProcessSystem = 1000;
    HANDLE info = NULL;
    typedef LONG(__stdcall* NtSetInformationProcessFn)(HANDLE, int, PVOID, ULONG);
    LONG status = ((NtSetInformationProcessFn)proc)(
        GetCurrentProcess(), ProcessWineMakeProcessSystem, &info, sizeof(HANDLE));
    if (status < 0)
        log_printf("wine_system_process_failed status=0x%lx", (unsigned long)status);
    else
        log_printf("wine_system_process_ok");
}

/* ------------------------------------------------------------------ */
/* Host Unix socket helpers.                                          */
/* ------------------------------------------------------------------ */

struct bridge_sockaddr_un
{
    unsigned short sun_family;
    char sun_path[BRIDGE_SOCKET_PATH_CAPACITY];
};

static const char* find_socket_directory(void)
{
    /* The launcher passes the host's Discord IPC directory explicitly. The
     * fallback chain mirrors Discord's own resolution for manual runs. */
    const char* dir = getenv("AETHER_DISCORD_IPC_DIR");
    if (dir && dir[0])
        return dir;
    dir = getenv("XDG_RUNTIME_DIR");
    if (dir && dir[0])
        return dir;
    dir = getenv("TMPDIR");
    if (dir && dir[0])
        return dir;
    dir = getenv("TMP");
    if (dir && dir[0])
        return dir;
    dir = getenv("TEMP");
    if (dir && dir[0])
        return dir;
    return "/tmp";
}

static int connect_once(const char* path)
{
    int fd = (int)raw_syscall(BRIDGE_SYS_SOCKET, 1 /* AF_UNIX */, 1 /* SOCK_STREAM */, 0);
    if (fd < 0)
    {
        log_printf("unix_socket_failed errno=%d", fd);
        return -1;
    }

    size_t path_length = strlen(path);
    if (path_length >= BRIDGE_SOCKET_PATH_CAPACITY)
    {
        log_printf("unix_path_too_long path=%s", path);
        raw_syscall(BRIDGE_SYS_CLOSE, fd, 0, 0);
        return -1;
    }

    struct bridge_sockaddr_un addr;
    memset(&addr, 0, sizeof(addr));
    addr.sun_family = 1; /* AF_UNIX */
    memcpy(addr.sun_path, path, path_length + 1);

    intptr_t rc = raw_syscall(BRIDGE_SYS_CONNECT, fd, (intptr_t)&addr, (intptr_t)sizeof(addr));
    if (rc < 0)
    {
        log_printf("unix_connect_failed path=%s errno=%d", path, (int)rc);
        raw_syscall(BRIDGE_SYS_CLOSE, fd, 0, 0);
        return -1;
    }
    return fd;
}

/*
 * Discord may still be starting (or not running at all). Keep the pipe open
 * for the client and retry the host socket for a while; if it never appears
 * the connection is dropped and the bridge waits for the next pipe client,
 * which the plugin's reconnect-with-backoff provides later.
 */
static int connect_host_socket(void)
{
    const char* directory = find_socket_directory();
    char path[BRIDGE_MAX_SOCKET_PATH];
    for (int attempt = 0; attempt < BRIDGE_MAX_SOCKET_ATTEMPTS; attempt++)
    {
        for (int pipe_number = 0; pipe_number <= BRIDGE_MAX_PIPE_NUMBER; pipe_number++)
        {
            const char* separator = "";
            size_t length = strlen(directory);
            if (length > 0 && directory[length - 1] != '/' && directory[length - 1] != '\\')
                separator = "/";
            snprintf(path, sizeof(path), "%s%sdiscord-ipc-%d", directory, separator, pipe_number);

            int fd = connect_once(path);
            if (fd >= 0)
            {
                log_printf("unix_connected path=%s fd=%d", path, fd);
                return fd;
            }
        }
        Sleep(1000);
    }
    log_printf("unix_socket_unavailable directory=%s", directory);
    return -1;
}

/* ------------------------------------------------------------------ */
/* Windows named pipe side.                                           */
/* ------------------------------------------------------------------ */

static HANDLE g_pipe = INVALID_HANDLE_VALUE;
static int g_sock_fd = -1;

static BOOL create_pipe(void)
{
    g_pipe = CreateNamedPipeW(
        BRIDGE_PIPE_NAME,
        PIPE_ACCESS_DUPLEX,
        PIPE_TYPE_BYTE | PIPE_READMODE_BYTE | PIPE_WAIT,
        1, /* one instance */
        BRIDGE_BUFFER_SIZE,
        BRIDGE_BUFFER_SIZE,
        0,
        NULL);
    if (g_pipe != INVALID_HANDLE_VALUE)
        return TRUE;

    DWORD error = GetLastError();
    if (error == ERROR_ACCESS_DENIED || error == ERROR_PIPE_BUSY)
        log_printf("pipe_owned_by_existing_bridge gle=%lu", error);
    else
        log_printf("create_pipe_failed gle=%lu", error);
    return FALSE;
}

static BOOL wait_for_client(void)
{
    if (ConnectNamedPipe(g_pipe, NULL))
        return TRUE;
    DWORD error = GetLastError();
    if (error == ERROR_PIPE_CONNECTED)
        return TRUE;
    log_printf("connect_pipe_failed gle=%lu", error);
    return FALSE;
}

/*
 * Each relay thread closes the endpoint it does not own when it finishes, so
 * a broken pipe client (game quit) or a dropped Discord connection (Discord
 * restarted) unblocks the peer and the bridge can loop back to serve a fresh
 * connection.
 */
static DWORD WINAPI pipe_to_socket_thread(LPVOID param)
{
    (void)param;
    char buffer[BRIDGE_BUFFER_SIZE];
    DWORD bytes_read = 0;
    BOOL ok = ReadFile(g_pipe, buffer, sizeof(buffer), &bytes_read, NULL) && bytes_read > 0;
    while (ok)
    {
        intptr_t total = 0;
        while (total < (intptr_t)bytes_read)
        {
            intptr_t written = raw_syscall(
                BRIDGE_SYS_WRITE, g_sock_fd, (intptr_t)(buffer + total), (intptr_t)(bytes_read - total));
            if (written < 0)
            {
                log_printf("unix_write_failed errno=%d", (int)written);
                ok = FALSE;
                break;
            }
            total += written;
        }
        if (!ok)
            break;
        ok = ReadFile(g_pipe, buffer, sizeof(buffer), &bytes_read, NULL) && bytes_read > 0;
    }
    if (g_sock_fd >= 0)
    {
        raw_syscall(BRIDGE_SYS_CLOSE, g_sock_fd, 0, 0);
        g_sock_fd = -1;
    }
    return 0;
}

static DWORD WINAPI socket_to_pipe_thread(LPVOID param)
{
    (void)param;
    char buffer[BRIDGE_BUFFER_SIZE];
    intptr_t bytes_read;
    while ((bytes_read = raw_syscall(BRIDGE_SYS_READ, g_sock_fd, (intptr_t)buffer,
                                     (intptr_t)sizeof(buffer))) > 0)
    {
        DWORD total = 0;
        while (total < (DWORD)bytes_read)
        {
            DWORD written = 0;
            if (!WriteFile(g_pipe, buffer + total, (DWORD)bytes_read - total, &written, NULL)
                || written == 0)
            {
                log_printf("pipe_write_failed gle=%lu", GetLastError());
                break;
            }
            total += written;
        }
    }
    if (g_pipe != INVALID_HANDLE_VALUE)
    {
        CloseHandle(g_pipe);
        g_pipe = INVALID_HANDLE_VALUE;
    }
    return 0;
}

static void relay_connection(void)
{
    HANDLE threads[2];
    threads[0] = CreateThread(NULL, 0, pipe_to_socket_thread, NULL, 0, NULL);
    threads[1] = CreateThread(NULL, 0, socket_to_pipe_thread, NULL, 0, NULL);
    if (!threads[0] || !threads[1])
    {
        log_printf("relay_thread_create_failed");
        if (threads[0])
            CloseHandle(threads[0]);
        if (threads[1])
            CloseHandle(threads[1]);
        return;
    }

    log_printf("relay_started");
    WaitForSingleObject(threads[0], INFINITE);
    WaitForSingleObject(threads[1], INFINITE);
    CloseHandle(threads[0]);
    CloseHandle(threads[1]);
    log_printf("relay_finished");
}

/* ------------------------------------------------------------------ */
/* Entry point.                                                       */
/* ------------------------------------------------------------------ */

int main(int argc, char** argv)
{
    log_open();

    if (argc > 1 && strcmp(argv[1], "--probe") == 0)
    {
        intptr_t pid = raw_syscall(BRIDGE_SYS_GETPID, 0, 0, 0);
        printf("AETHERXIV_DISCORD_BRIDGE_OK x86-64 raw-syscall bridge pid=%d\n", (int)pid);
        log_close();
        return 0;
    }

    make_wine_system_process();
    log_printf("bridge_started pid=%d", (int)raw_syscall(BRIDGE_SYS_GETPID, 0, 0, 0));

    for (;;)
    {
        if (!create_pipe())
            break; /* another bridge already owns the pipe; nothing to do */

        if (!wait_for_client())
        {
            CloseHandle(g_pipe);
            g_pipe = INVALID_HANDLE_VALUE;
            continue;
        }

        log_printf("pipe_client_connected");
        g_sock_fd = connect_host_socket();
        if (g_sock_fd < 0)
        {
            CloseHandle(g_pipe);
            g_pipe = INVALID_HANDLE_VALUE;
            continue; /* wait for the plugin's next reconnect attempt */
        }

        relay_connection();

        if (g_sock_fd >= 0)
        {
            raw_syscall(BRIDGE_SYS_CLOSE, g_sock_fd, 0, 0);
            g_sock_fd = -1;
        }
        if (g_pipe != INVALID_HANDLE_VALUE)
        {
            CloseHandle(g_pipe);
            g_pipe = INVALID_HANDLE_VALUE;
        }
        /* Loop back and recreate the pipe for the next client connection. */
    }

    log_close();
    return 0;
}
