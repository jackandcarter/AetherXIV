# Build AetherXIV on SteamOS

Use `./tools/SteamOS/build-aetherxiv.sh Release --scope full` or `--scope core`.
SteamOS is immutable: `install-build-dependencies.sh` provisions an
`aetherxiv-build` distrobox rather than modifying the gaming host.

SteamOS uses the Linux ABI and build implementation but writes an independent
SteamOS release directory.

## Requirements

- Current SteamOS in Desktop Mode or another writable development environment
- [.NET SDK 10](https://dotnet.microsoft.com/download/dotnet/10.0), exact SDK
  `10.0.203`
- [Python 3](https://www.python.org/downloads/)
- Bash and standard Linux utilities
- MinGW-w64 providing `i686-w64-mingw32-g++`
- Sufficient persistent storage for SDKs, NuGet caches, and release output

Because the SteamOS base image is read-only and system updates can replace local
changes, prefer a persistent development container or user-owned tool location.

## Build

From the repository root. The SteamOS entry point sets the platform name to
`SteamOS` and delegates to the Linux build recipe, preventing the two package
formats from drifting. The package build produces the compatibility runtime
automatically when `AETHERXIV_WINE_RUNTIME_ROOT` is unset (recipe:
`tools/runtime/build-linux.sh`); to reuse an existing runtime package instead:

```bash
runtime_root="/tmp/aetherxiv-runtime-linux-x64-wow64"
./tools/runtime/build-linux.sh "$runtime_root"
AETHERXIV_WINE_RUNTIME_ROOT="$runtime_root" ./tools/SteamOS/build-aetherxiv.sh Release
```

## Output

The complete release is written to:

```text
bin/build/Release/SteamOS/
```

It includes Core, Launcher, all services, database tools, the Windows x64
managed helper, native x86 injector, and locally built, integrity-pinned Umbra
base framework. Path-dependent
Relocatable `.desktop` templates are packaged with `Terminal=false`; edit `Exec`
to the installed absolute path before adding one in Desktop Mode. The bundled Discord Rich
Presence plugin compiles against `Aether.Umbra.PluginApi` from the
repository-local NuGet feed (`.umbra-nuget/`), packed automatically when
missing; CI populates it before each platform build.

A core-only build is written separately to `bin/build/Release/SteamOS-Core`.
The standalone Launcher download contains the verified `launcher/` directory,
its Launcher desktop entry, Umbra, and the bundled compatibility runtime, but
no Core, server, or database payload.

## Output reset and verification

The build uses an isolated SteamOS staging directory, verifies it, then replaces
the prior SteamOS package atomically. A failed build leaves the last verified
package and other platform outputs intact. Run the full test suite when
preparing a release:

```bash
./tools/Development/verify-aetherxiv.sh
```

If maintaining the toolchain directly on SteamOS becomes unreliable, build the
SteamOS target from a compatible x64 Linux development host and test the final
package on the target SteamOS version.
