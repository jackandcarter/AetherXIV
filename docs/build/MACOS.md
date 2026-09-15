# Build AetherXIV on macOS

Use `./tools/MacOS/build-aetherxiv.sh Release --scope full` for a complete
package or `--scope core` for the Core stack. `--install-dependencies` is an
explicit Homebrew provisioning action; it is never implicit in a build.

This build produces the complete Apple-silicon macOS release, including both
GUI applications, all server services, database tooling, Windows client helpers,
the native x86 injector, and the locally built Umbra base framework.

## Requirements

- Apple silicon with macOS 14 or later
- [.NET SDK 10](https://dotnet.microsoft.com/download/dotnet/10.0), exact SDK
  `10.0.203` as pinned by `global.json`
- [Python 3](https://www.python.org/downloads/)
- [Homebrew](https://brew.sh/)
- Intel Homebrew [MinGW-w64](https://formulae.brew.sh/formula/mingw-w64),
  providing x86_64-hosted compilers for both i686 and x86_64 Windows targets
- Bison 3 for the targeted Wine module build
- Apple Command Line Tools for `clang`, `codesign`, and standard build tools
- Rosetta 2 when building or testing on Apple silicon
- Internet access for the initial NuGet restore, unless packages are cached

The current script does not require the full Xcode application.

Example dependency installation after Homebrew is installed:

```bash
arch -x86_64 /usr/local/bin/brew install \
  bison mingw-w64 python
```

On Apple silicon, install the Wine build dependencies with the Intel Homebrew
installation under `/usr/local`. The runtime builder deliberately rejects the
ARM-hosted MinGW tools under `/opt/homebrew`, because Wine's macOS host build
runs as x86_64 through Rosetta. If both Homebrew installations are present, the
builder selects `/usr/local/opt/mingw-w64/bin`; it can also be supplied
explicitly with `AETHERXIV_MINGW_BIN`.

Install .NET from Microsoft's official download and confirm the pinned SDK:

```bash
dotnet --list-sdks
python3 --version
i686-w64-mingw32-g++ --version
```

## Compatibility runtime

The release does not discover or download a different Wine provider. The
package build produces the pinned AetherXIV variant of Wine Stable 11.0_1
automatically when `AETHERXIV_WINE_RUNTIME_ROOT` is unset (the recipe lives at
`tools/runtime/build-macos.sh`). To build one explicitly and reuse it across
builds instead:

```bash
runtime_root="/tmp/aetherxiv-runtime-macos-x64-wow64"
./tools/runtime/build-macos.sh "$runtime_root"
AETHERXIV_WINE_RUNTIME_ROOT="$runtime_root" ./tools/MacOS/build-aetherxiv.sh Release
```

The runtime recipe verifies the exact upstream Wine 11.0 source and Wine Stable
11.0_1 macOS binary SHA-256 values. It preserves the official binary package's
graphics, audio, media, and host-library layout, applies the recorded Rosetta 2
WoW64 transition workaround needed by Umbra's Windows x86 .NET 10 runtime,
rebuilds and strips only `wow64cpu.dll`, signs the nested Mach-O payload locally,
proves a new WoW64 prefix can initialize, and writes a complete checksum
inventory. The client remains on WineD3D/OpenGL; no custom FAudio scheduling,
buffer, or game-priority modifications are applied.

## Build

From the repository root (omitting `AETHERXIV_WINE_RUNTIME_ROOT` builds the
compatibility runtime automatically):

```bash
AETHERXIV_WINE_RUNTIME_ROOT="$runtime_root" ./tools/MacOS/build-aetherxiv.sh Release
```

Use `Debug` instead of `Release` for a symbol-bearing development package:

```bash
AETHERXIV_WINE_RUNTIME_ROOT="$runtime_root" ./tools/MacOS/build-aetherxiv.sh Debug
```

To rebuild only the Launcher, its Windows helper, Umbra, and the bundled
compatibility runtime while leaving all Core and server outputs untouched:

```bash
AETHERXIV_WINE_RUNTIME_ROOT="$runtime_root" ./tools/MacOS/build-aetherxiv.sh Release --launcher-only
```

The launcher-only mode replaces only `AetherXIV Launcher.app` inside the normal
macOS output directory. It does not publish the Core app, server hosts, database
package, scripts, actor data, or any other server-side payload.

The default runtime identifier is `osx-arm64`. Advanced builders may override
the documented `AETHERXIV_*` environment variables used at the top of the build
script, but every changed target must be verified independently.

## Output

The complete release is written to:

```text
bin/build/Release/MacOS/
├── AetherXIV Core.app
├── AetherXIV Launcher.app
├── Database/
└── build-manifest.txt
```

The server payload is embedded inside `AetherXIV Core.app`. The Launcher bundle
contains `Contents/Resources/CompatibilityRuntime`, the self-contained Windows
x64 managed helper, the native x86 Umbra injector, and the integrity-pinned
Umbra base framework. The 32-bit game and Umbra do not require a user-installed
Wine provider or a separately installed 32-bit .NET runtime.

A core-only build is written separately and never alters the full package:

```text
bin/build/Release/MacOS-Core/
├── AetherXIV Core.app
└── Database/
```

The standalone Launcher download is assembled from the verified Launcher app
inside the full build; it contains no Core app, server hosts, or database
package.

The bundled Discord Rich Presence plugin compiles against `Aether.Umbra.PluginApi`
from the repository-local NuGet feed (`.umbra-nuget/`). The packaging step packs
it automatically when missing, and CI populates it before each platform build, so
a pre-warmed global NuGet cache is never required.

## Output reset warning

The build uses an isolated macOS staging directory, verifies it, then replaces
the prior macOS package atomically. A failed build removes its staging/work
directory and leaves the last verified package intact. Other platform outputs
are not rewritten. Package promotion is refused while AetherXIV Core, Launcher,
the game client, or a server process still has the replacement target open.
Replacing a live package would detach those processes from their relative
server data and Lua trees even though the new package is complete. Stop the
running stack before rebuilding. Never store hand-written files, logs,
captures, or backups under `bin`.

## Verification

The build automatically validates the final release layout. Run the full source
test suites separately when preparing a release:

```bash
./tools/Development/verify-aetherxiv.sh
```

The runtime packager applies ad-hoc signatures for local verification. A public
release must then sign every nested Mach-O runtime library and executable with
the release identity before signing the outer Launcher app, followed by
notarization and stapling. Archive creation and distribution remain separate
release steps.

## Common failures

- **SDK not found:** install SDK `10.0.203` and ensure `dotnet` resolves to it.
- **Intel MinGW compiler missing:** install MinGW-w64 with the Intel Homebrew
  installation under `/usr/local`, or set `AETHERXIV_MINGW_BIN` to the directory
  containing x86_64-hosted `i686-w64-mingw32-gcc` and
  `x86_64-w64-mingw32-gcc`.
- **Lua manifest verification failed:** regenerate the Lua inventory only after
  reviewing the authoritative `Data/scripts` changes.
- **NuGet restore failed:** confirm network access or the configured offline
  package cache.
- **Umbra base build failed:** confirm the x86 MinGW toolchain can compile both
  the injector and bootstrap, then review the reported managed or receipt error.
- **Compatibility runtime missing:** build it first and pass its package root in
  `AETHERXIV_WINE_RUNTIME_ROOT`. The release build intentionally has no system
  Wine, CrossOver, or custom-command fallback.
