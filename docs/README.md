# AetherXIV documentation

These guides cover the AetherXIV 2.1.2 release, build 22044, and Umbra 2.1.2
with plugin API 2.1. Choose a platform guide first, then follow the guide for
the application or plugin you want to use.

## Install a release

| Your computer | Installation guide |
|---|---|
| Windows 11 x64 | [Windows](setup/WINDOWS.md) |
| macOS 14 or later on Apple silicon | [macOS](setup/MACOS.md) |
| Ubuntu 22.04/24.04 x64 and other desktop Linux distributions | [Linux](setup/LINUX.md) |
| Steam Deck in Desktop Mode | [SteamOS](setup/STEAMOS.md) |

- [Runtime dependencies](BUILD_AND_RUNTIME_DEPENDENCIES.md): what is included,
  what to install, and which requirements apply to server operators or players.
- [Download selection and integrity](DOWNLOADS.md): Full, Core, and Launcher
  packages, checksums, updating, and repository access.
- [First launch](GETTING_STARTED.md): local server setup or connecting to an existing server.

## Core and Launcher

- [Core guide](AETHERXIV_CORE_GUIDE.md): service controls, maintenance,
  soft-deleted character recovery, backups, realm status, and hosted endpoints.
- [Launcher guide](LAUNCHER_GUIDE.md): server profiles, client validation,
  patching, runtime checks, graphics targets, and Umbra controls.
- [Database setup and recovery](DATABASE_SETUP_AND_MIGRATION.md).
- [Hosted server checklist](HOSTED_SERVER.md).
- [Debugging and bug reports](DEBUGGING_AND_BUG_REPORTING.md).
- [Docker availability](DOCKER_SERVER.md).

## Umbra and plugin development

- [Using Umbra](umbra/USING_UMBRA.md): enabling the framework, installing plugins,
  custom repositories, safe mode, and recovery.
- [SDK overview and first plugin](UMBRA_SDK.md): build against the shipped public
  API without a private source checkout.
- [Plugin API reference](umbra/PLUGIN_DEVELOPMENT.md): manifests, capabilities,
  lifecycle, UI, commands, services, packaging, and repository indexes.
- [UI images and animation](umbra/UI_MEDIA_API.md).
- [Diagnostics and plugin messaging](umbra/DIAGNOSTICS.md).
- [Resource loading and appearance](umbra/RESOURCE_AND_APPEARANCE.md).
- [Map and travel service boundaries](umbra/MAP_TRAVEL.md).
- [Factory usage](umbra/FACTORY.md): optional, separately installed appearance mods.

## Release information

- [Full 2.1.2 release notes](AETHERXIV_2.1.2_RELEASE_NOTES.md).
- [Release downloads](https://github.com/jackandcarter/AetherXIV/releases).

This repository distributes documentation and compiled releases. The Core,
Launcher, Umbra, and Wine implementation repositories remain separate and
private. Plugin examples in these guides illustrate the public API; they are
not the framework implementation. Game client files, patches, and extracted
Square Enix assets are not distributed here.
