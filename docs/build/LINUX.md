# Build AetherXIV on Linux

For 2.1, run `./tools/Linux/build-aetherxiv.sh Release --scope full` for the
complete Launcher/Umbra/Aether.3 package or `--scope core` for the server,
database, Operator, and Core-app package. Run
`./tools/Linux/install-build-dependencies.sh` first, or add
`--install-dependencies` to explicitly permit provisioning.

Ubuntu 22.04 and 24.04 x64 are the primary Linux build baselines. Other x64
distributions are best-effort.

## Requirements

- [.NET SDK 10](https://dotnet.microsoft.com/download/dotnet/10.0), exact SDK
  `10.0.203`
- [Python 3](https://www.python.org/downloads/)
- Bash, GNU core utilities, `find`, and SHA-256 tooling
- MinGW-w64 providing the i686 and x86_64 Windows compilers
- A C toolchain, Bison, Flex, X11/OpenGL development headers, FreeType, and
  GnuTLS for the authoritative AetherXIV compatibility runtime
- Internet access for the initial NuGet restore

Use your distribution's packages from an official repository. On Ubuntu, locate
current package names through [Ubuntu Packages](https://packages.ubuntu.com/).
The common tool packages include Python 3 and MinGW-w64; install the .NET SDK
using [Microsoft's Linux instructions](https://learn.microsoft.com/dotnet/core/install/linux).

Confirm the required commands:

```bash
dotnet --list-sdks
python3 --version
i686-w64-mingw32-g++ --version
```

## Compatibility runtime

The package build produces the pinned CodeWeavers-source Wine runtime
automatically when `AETHERXIV_WINE_RUNTIME_ROOT` is unset (the recipe lives at
`tools/runtime/build-linux.sh`). To build one explicitly and reuse it across
builds instead:

```bash
runtime_root="/tmp/aetherxiv-runtime-linux-x64-wow64"
./tools/runtime/build-linux.sh "$runtime_root"
AETHERXIV_WINE_RUNTIME_ROOT="$runtime_root" ./tools/Linux/build-aetherxiv.sh Release
```

The recipe verifies the source SHA-256, applies the public AetherXIV patch,
including the XAudio scheduling and underrun-buffer correction, builds the new
i386/x86_64 WoW64 layout, records corresponding source and licenses, checks
host linkage, and writes the runtime checksum inventory.

## Build

From the repository root (omitting `AETHERXIV_WINE_RUNTIME_ROOT` builds the
compatibility runtime automatically):

```bash
AETHERXIV_WINE_RUNTIME_ROOT="$runtime_root" ./tools/Linux/build-aetherxiv.sh Release
```

For a development package:

```bash
AETHERXIV_WINE_RUNTIME_ROOT="$runtime_root" ./tools/Linux/build-aetherxiv.sh Debug
```

## Output

The full release is written to `bin/build/Release/Linux`:

```text
Linux/
├── core/app/AetherXIV.Core.App
├── launcher/app/AetherXIV.Launcher.App
├── servers/
└── Database/
```

The package includes relocatable `.desktop` templates with `Terminal=false`.
Copy the desired template to your desktop environment only after editing `Exec`
to the installed absolute package path. The
Launcher contains `launcher/app/CompatibilityRuntime`, a self-contained Windows
x64 managed helper, the native x86 injector, and the locally built,
integrity-pinned Umbra base framework used by the 32-bit client. A user-installed
Wine provider or separate 32-bit .NET runtime is not used.

The bundled Discord Rich Presence plugin compiles against `Aether.Umbra.PluginApi`
from the repository-local NuGet feed (`.umbra-nuget/`); it is packed
automatically when missing, and CI populates it before each platform build.

A core-only build is written separately to `bin/build/Release/Linux-Core`.
The standalone Launcher download is assembled from the verified `launcher/`
directory and its Launcher desktop entry; it contains Umbra and the bundled
compatibility runtime, but no Core, server, or database payload.

## Output reset warning

The build uses an isolated Linux staging directory, verifies it, then replaces
the prior Linux package atomically. A failed build leaves the last verified
package and other platform outputs intact. Do not store personal files under
`bin`.

## Verification

The package layout is verified automatically. Run the complete solution and
Launcher tests with:

```bash
./tools/Development/verify-aetherxiv.sh
```

## Common failures

- Microsoft does not publish every SDK patch through every distribution feed;
  confirm that SDK `10.0.203` is actually selected.
- Some distributions name or split MinGW packages differently. The final test
  is whether `i686-w64-mingw32-g++` resolves on `PATH`.
- Runtime GUI libraries are not required merely to compile, but install the
  [Avalonia Linux dependencies](https://docs.avaloniaui.net/docs/deployment/linux)
  before testing the built applications.
- A missing or invalid compatibility-runtime bundle blocks launch. Rebuild or
  reinstall the matching AetherXIV package; there is intentionally no PATH Wine
  or custom-command fallback.
