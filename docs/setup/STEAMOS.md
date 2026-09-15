# AetherXIV Setup on SteamOS

SteamOS uses the Linux release ABI and is supported in Steam Deck Desktop Mode.
The current SteamOS release is the primary target; modified or older images are
best-effort.

## Before you begin

You need the complete `SteamOS` release, a user-owned Final Fantasy XIV 1.23b
client, MariaDB, the .NET 10 ASP.NET Core Runtime, and the Linux desktop
libraries listed in the [dependency matrix](../BUILD_AND_RUNTIME_DEPENDENCIES.md).
The AetherXIV Compatibility Runtime is included with the release; a separate
Wine installation is neither required nor selected by the Launcher.

SteamOS has a read-only base image. System updates can replace packages or
changes made outside persistent storage. Plan where MariaDB data, runtimes,
prefixes, and the AetherXIV release will live before configuring the server.

## Install in Desktop Mode

1. Enter Desktop Mode.
2. Extract the entire SteamOS release to persistent storage.
3. Keep `core`, `launcher`, `servers`, and `Database` together.
4. Make `core/app/AetherXIV.Core.App` and
   `launcher/app/AetherXIV.Launcher.App` executable, then open those apps
   directly from their folders. The release intentionally does not ship
   path-dependent `.desktop` shortcuts.

## Local setup

1. Start MariaDB and open **AetherXIV Core**.
2. Verify dependencies and complete database setup.
3. Start the stack and wait for all services.
4. Open **AetherXIV Launcher**, save **Localhost**, and locate the 1.23b client.
5. On **Runtime**, confirm that **Runtime source** reports the bundled
   AetherXIV Compatibility Runtime, then select **Validate Runtime**. It checks
   the shipped receipt, host libraries, managed prefix, and helpers without
   modifying the read-only SteamOS base image. If validation names a missing
   host library, install that library in a persistent SteamOS/Arch environment
   and validate again.
6. Enable Umbra if desired, then log in.

Running the server and client together is convenient for development but may
be resource intensive on a Steam Deck. A remote AetherXIV server can be selected
from the Launcher's **Server** tab instead.

Do not add a system Wine provider or disable SteamOS read-only protection to
work around a validation failure. The Launcher deliberately has no custom
runtime setting; restore the complete matching release if its bundled runtime
is missing or fails integrity validation.

## After SteamOS updates

Recheck the .NET runtime, MariaDB service, graphics support, and executable
bits. Launcher-managed data and prefixes should remain in
writable user storage.

See the [Linux setup guide](LINUX.md) for the shared runtime process and the
[debugging guide](../DEBUGGING_AND_BUG_REPORTING.md) for log locations.
