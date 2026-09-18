# AetherXIV 2.1

AetherXIV is a cross-platform server, launcher, and Umbra Plugin framework stack for
a user-owned Final Fantasy XIV 1.23b client. 

The 2.1 release candidate combines the Lobby, World, Map, and Launcher Services; AetherXIV Core UI App; AetherXIV
Launcher; Direct Core database tooling; and Umbra Frameworks in one workspace.

- Main branch: Current Stable Release
- 2.0 branch: Archival
- 2.1 branch: Current Development

## Supported targets

- macOS 14 or later on Apple silicon
- Windows 11 x64
- Ubuntu 22.04/24.04 x64
- SteamOS Desktop Mode

Windows uses the native client launch path. macOS, Linux, and SteamOS full
packages include the project compatibility runtime. AetherXIV never distributes
the game client, patches, or Square Enix assets.

## Start here

- [2.1 documentation index](docs/README.md)
- [2.1 release notes](docs/AETHERXIV_2.1_RELEASE_NOTES.md)
- [AetherXIV Core guide](docs/AETHERXIV_CORE_GUIDE.md)
- [Launcher guide](docs/LAUNCHER_GUIDE.md)
- [Database setup and migration](docs/DATABASE_SETUP_AND_MIGRATION.md)
- [Umbra SDK and plugin development](docs/UMBRA_SDK.md)

## Build from source

Every platform supports two explicit build scopes:

- `core` packages the server stack, Core application, configurations, Direct
  Core database package, migrations, and startup tooling.
- `full` adds Launcher, Umbra, bundled framework/plugin assets, client helpers,
  and the non-Windows compatibility runtime.

## Release downloads

Each 2.1 platform build publishes three archives:

- **Full** — Core stack and UI, database package, Launcher, Umbra, client
  helpers, and the bundled compatibility runtime on macOS/Linux/SteamOS.
- **Core** — the server stack, Core UI, database package, migrations, and
  startup tooling; it does not contain Launcher, Umbra, or a compatibility
  runtime.
- **Launcher** — AetherXIV Launcher, Umbra, helpers, and on macOS/Linux/SteamOS
  the bundled Aether.3 compatibility runtime; it does not contain Core, server
  hosts, or the database package.

Windows Launcher archives remain native and therefore contain no Wine runtime.

Use the platform guides for exact prerequisites, opt-in dependency installation,
and package verification:

- [macOS](docs/build/MACOS.md)
- [Windows](docs/build/WINDOWS.md)
- [Linux](docs/build/LINUX.md)
- [SteamOS](docs/build/STEAMOS.md)

The repository pins .NET SDK `10.0.203` in `global.json`. Run the managed
verification suite from the repository root:

```sh
./tools/Development/verify-aetherxiv.sh
```

## Client ownership and licensing

Each operator must supply a legally obtained Final Fantasy XIV 1.23b client and
any required patch library. AetherXIV is licensed under the
[GNU Affero General Public License, version 3 or later](LICENSE).

- [Development and modification notice](MODIFICATIONS.md)
- [Third-party notices](THIRD_PARTY_NOTICES.md)
- [Contribution policy](CONTRIBUTING.md)
- [Name and branding policy](TRADEMARKS.md)


If you are interested in becoming part of the team feel free to reach out on Discord!
