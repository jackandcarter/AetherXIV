# AetherXIV 2.1 Release Notes

AetherXIV 2.1, build 22042, updates the Core, Launcher, Umbra, and bundled compatibility runtime.

## Downloads

Choose the package for Windows, macOS, Linux, or SteamOS. Core packages contain the server stack and database tools without the Launcher or Wine. Full packages also include the Launcher and Umbra, with a bundled compatibility runtime on macOS, Linux, and SteamOS. Windows runs natively.

## Launcher and compatibility

- Linux and SteamOS include DXVK 3.1.1. **DXVK / Vulkan (validated)** becomes available after a graphics capability check succeeds. The Launcher remembers the selected renderer and periodically retries failed capability checks.
- Linux runtime builds include Vulkan and PulseAudio support, with improved checks for missing graphics and audio dependencies.
- Client patching handles file deletions correctly and supports recovery after interruption. Temporary Windows file locks are retried up to five times with one-second delays.
- The Launcher manages its bundled runtime and game prefix. **Reset Prefix** provides a recovery option.
- Optional Discord Rich Presence is controlled through the Launcher and its Umbra plugin.

## Core and Umbra

- Improvements to opening quests, zone transitions, NPC presentation, equipment handling, and crafting content.
- Expanded diagnostic logging for investigating connection, quest, and transition problems.
- Database setup, migration, backup, and recovery tools are included with Core packages.
- Umbra provides an in-game Plugin Manager, plugin isolation, safe mode, and a versioned plugin SDK.

## Known limitations

Gameplay restoration remains incomplete. Some quests, battle behavior, and city travel features still require in-game validation. Umbra's remote framework update service remains disabled; the bundled framework is used.

The reported Linux startup crash involving XAudio2 reverb and in-game audio still require validation on affected systems. A successful build does not establish that these issues are resolved.

## Getting started

See the [platform setup guides](README.md), [Launcher guide](LAUNCHER_GUIDE.md), [Core guide](AETHERXIV_CORE_GUIDE.md), and [database migration guide](DATABASE_SETUP_AND_MIGRATION.md). Plugin authors can use the [Umbra SDK guide](UMBRA_SDK.md).
