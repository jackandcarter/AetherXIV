# AetherXIV

AetherXIV restores the Final Fantasy XIV 1.23b experience through Core, Launcher,
and the optional Umbra plugin framework.

**Current release: 2.1.2 · Build 22044.**
See the [full release notes](docs/AETHERXIV_2.1.2_RELEASE_NOTES.md) for restored
quests, battle and navigation improvements, server maintenance, and known limits.

## Downloads

Open [Releases](https://github.com/jackandcarter/AetherXIV/releases) for the
platform binaries.

The release provides three downloads for each platform:

| Platform | Archive name prefix | Format |
|---|---|---|
| Windows 11 x64 | `AetherXIV-2.1.2.22044-Windows-x64` | `.zip` |
| macOS 14+ on Apple silicon | `AetherXIV-2.1.2.22044-MacOS-arm64` | `.zip` |
| Linux x64 | `AetherXIV-2.1.2.22044-Linux-x64` | `.tar.gz` |
| SteamOS x64, Desktop Mode | `AetherXIV-2.1.2.22044-SteamOS-x64` | `.tar.gz` |

Choose the archive without a suffix for the complete local server and Launcher.
`-Core` contains the server tools; `-Launcher` is for connecting to an existing
server. Keep the extracted folder structure intact.

Ubuntu 22.04 and 24.04 are the primary Linux targets. Other desktop distributions
are best-effort. Platform build checks do not replace testing on your own machine.

## Getting started

1. Back up your database before updating. Stop the old server and close Launcher.
2. Extract the chosen archive into a new, writable folder. Do not launch from the archive.
3. For a local server, install MariaDB and the .NET 10 ASP.NET Core Runtime.
   Open Core, verify dependencies and database settings, then start the stack.
4. Open Launcher, select your server, and locate your own Final Fantasy XIV
   1.23b client. The game client and its assets are not included.
5. Validate the client and runtime before logging in. macOS requires Rosetta 2;
   Linux and SteamOS require the desktop and graphics libraries reported by validation.

Core and Launcher GUIs are self-contained. Linux and SteamOS users can open
`core/app/AetherXIV.Core.App` and `launcher/app/AetherXIV.Launcher.App`; restore
executable permissions if your extraction tool removes them. macOS provides
`AetherXIV Core.app` and `AetherXIV Launcher.app`.

Launcher packages include the verified Umbra framework and, on macOS/Linux,
the matching AetherXIV compatibility runtime. Windows runs the client natively.
Factory is separately installable and is not bundled.

## Documentation

Start with the [documentation index](docs/README.md) or choose your platform:

| Windows | macOS | Linux | SteamOS |
|---|---|---|---|
| [Install and dependencies](docs/setup/WINDOWS.md) | [Install and dependencies](docs/setup/MACOS.md) | [Install and dependencies](docs/setup/LINUX.md) | [Install and dependencies](docs/setup/STEAMOS.md) |

- [Core guide](docs/AETHERXIV_CORE_GUIDE.md): services, maintenance, character recovery, backups, and realm status.
- [Launcher guide](docs/LAUNCHER_GUIDE.md): server profiles, client and runtime validation, graphics, and Umbra.
- [Database setup and migration](docs/DATABASE_SETUP_AND_MIGRATION.md), [hosted servers](docs/HOSTED_SERVER.md), and [runtime requirements](docs/BUILD_AND_RUNTIME_DEPENDENCIES.md).
- [Using Umbra](docs/umbra/USING_UMBRA.md) and [Factory](docs/umbra/FACTORY.md).
- [Plugin SDK quickstart](docs/UMBRA_SDK.md) and [API reference](docs/umbra/PLUGIN_DEVELOPMENT.md), including UI, diagnostics, resource loading, and appearance services.
- [Troubleshooting and bug reports](docs/DEBUGGING_AND_BUG_REPORTING.md).

Plugin authors can use the public API DLL shipped with Launcher. This repository
provides SDK documentation and examples; it does not add a separate SDK download.

## Integrity and support

`SHA256SUMS.txt` covers all 12 application archives and the dependency lock.
Compare your downloaded archive against that file before extracting it.
`resolved-dependencies.lock.json` records the exact producer versions and hashes.

For issues, include the platform, build number, steps to reproduce, and relevant
Core or Launcher logs. Remove passwords, account details and other private data
before sharing logs. Restoration remains ongoing; see the release notes for
features that are incomplete or disabled by default.

This repository holds release documentation and binary downloads. Core, Launcher,
Umbra, and compatibility-runtime implementation source stays in their respective
development repositories. GitHub's automatic **Source code** archives are snapshots
of this documentation repository; they do not contain those implementations.

The game client and extracted game assets are not distributed here. See the
included [license](LICENSE), [third-party notices](THIRD_PARTY_NOTICES.md), and
[trademark notice](TRADEMARKS.md).


Don't forget to join our Discord! https://discord.gg/sGBVQVVAMG
