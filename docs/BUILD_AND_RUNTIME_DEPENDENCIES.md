# Runtime and plugin development dependencies

This matrix applies to the compiled AetherXIV 2.1.2 downloads. Core and Launcher
GUIs are self-contained. Players connecting to another server do not need a
database server, compiler, or SDK.

## Requirements by task

| Task | Required | Included in the download |
|---|---|---|
| Use Launcher with a remote server | Supported OS, graphics drivers, your own 1.23b client, server account | Launcher, Windows helpers, Umbra |
| Use Launcher on macOS/Linux/SteamOS | Platform prerequisites below | Verified AetherXIV compatibility runtime |
| Run Core and the server hosts | MariaDB Server, MariaDB/MySQL command-line tools, .NET 10 ASP.NET Core Runtime, writable data/log/backup folders | Core GUI, server hosts, database baseline and migrations |
| Develop a managed Umbra plugin | .NET SDK 10.0.203 and the public PluginApi DLL from a matching Launcher | The API DLL ships with Umbra; see the [plugin quickstart](UMBRA_SDK.md) |

Use the [Full, Core, or Launcher package](DOWNLOADS.md) appropriate to your task.
The game client, client patches, MariaDB Server, and host ASP.NET Core Runtime
are not included. The Windows x86 .NET runtime hosted by Umbra is included in
the verified framework; keep its receipt and payload together.

## Windows

Windows 11 x64 is the supported desktop target. The legacy client remains a
32-bit application. Windows launches it natively; Wine is not used.

Core operators need MariaDB Server, its command-line tools, and the Windows x64
.NET 10 ASP.NET Core Runtime. Install the **ASP.NET Core Runtime**, which also
provides the base runtime needed by Map, World, and Lobby.

The original game may need legacy DirectX components from its installation
media. These are game prerequisites, separate from the self-contained Launcher
and its statically linked native helpers.

## macOS

Apple silicon with macOS 14 or later is supported. Core operators need native
ARM64 MariaDB and the macOS ARM64 .NET 10 ASP.NET Core Runtime.

Launcher includes an Intel compatibility runtime and requires Rosetta 2.
When Rosetta is absent, runtime validation can trigger Apple's installation
prompt and wait for it to finish. Apple controls that installation and its terms.

The default legacy DirectX 9 path uses WineD3D/OpenGL. The validated Linux DXVK
target is not offered on macOS. GStreamer is optional for launch; some movies
or media may not play without it. Launcher reports the warning and does not
silently install an unsigned upstream package.

Grant file access when macOS requests access to your client or application data.
Keep the extracted applications with the rest of the release folder.

## Linux

Ubuntu 22.04 and 24.04 x64 are the primary desktop targets. Other x64 desktop
distributions are best-effort. Core operators need x64 MariaDB and the x64
.NET 10 ASP.NET Core Runtime.

Both GUIs require X11 or XWayland and Avalonia's native libraries. On
Debian/Ubuntu, common packages are `libx11-6`, `libice6`, `libsm6`, and
`libfontconfig1`. The bundled Wine runtime also needs its host libraries and
working graphics drivers. **Verify Dependencies** reports exact missing library
names; the correct package name depends on your distribution.

On supported Debian/Ubuntu, Arch, and Fedora families, Launcher can offer an
administrator-approved dependency installation and repeat verification afterward.
It does not install dependencies without that confirmation.

The optional **DXVK / Vulkan (validated)** target appears only after the bundled
runtime and a real x86 D3D9 device probe pass. The host supplies the x86_64 Vulkan
loader and GPU drivers; the bundled WoW64 runtime supplies the x86 Windows D3D9
payload. Do not install a different Wine provider to work around a failed check.

## SteamOS

Use Desktop Mode and persistent writable storage. SteamOS uses the Linux x64
release ABI and the included compatibility runtime.

The immutable base image and OS updates can replace system changes. Launcher
therefore directs SteamOS users to a persistent environment for missing host
dependencies. Do not disable read-only protection merely to satisfy a check.
Running both server and client can be resource intensive; use a remote server
when a local database and server stack are unnecessary.

## Umbra plugin authors

Install .NET SDK **10.0.203** for your development computer and compile managed
plugins for `net10.0-windows` with Windows targeting enabled. Building on macOS
or Linux produces a managed plugin for the Windows x86 game process, not a
native plugin for the host OS.

Use the public `Aether.Umbra.PluginApi.dll` shipped with Umbra 2.1.2. You do not
need the Framework source, native bootstrap source, a C++ toolchain, or a private
GitHub account to follow the [quickstart](UMBRA_SDK.md). A downloadable SDK/NuGet
bundle is not attached to this application release; the quickstart uses the
shipped API assembly directly.

## Network defaults

| Service | Default TCP port | Typical exposure |
|---|---|---|
| Launcher Services | 8080 | Local, or behind an HTTPS reverse proxy |
| Lobby | 54994 | Client-facing for a hosted server |
| World | 54992 | Client-facing for a hosted server |
| Map | 1989 | Internal service route |
| MariaDB | 3306 | Local or restricted database network |

See the [hosted server guide](HOSTED_SERVER.md) for bind versus advertise
addresses. A local all-in-one installation uses loopback and needs no router
port forwarding.

## Official dependency downloads

- [.NET 10 SDK and runtimes](https://dotnet.microsoft.com/download/dotnet/10.0).
- [Microsoft .NET installation guides](https://learn.microsoft.com/dotnet/core/install/).
- [MariaDB Community Server](https://mariadb.org/download/).
- [Avalonia Linux dependencies](https://docs.avaloniaui.net/docs/deployment/linux).
- [Apple Rosetta installation](https://support.apple.com/en-us/102527).

Use Launcher and Core validation to distinguish a missing host dependency from
a corrupt release payload. Restore the matching release when an integrity check
fails; replacing individual Umbra or compatibility-runtime files invalidates
their inventories.
