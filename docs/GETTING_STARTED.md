# First launch

Start with your [platform installation guide](README.md#install-a-release) and
the [dependency matrix](BUILD_AND_RUNTIME_DEPENDENCIES.md). Extract a release
before opening either application, and keep its directory structure intact.

## Connect to an existing server

Use the **Launcher** download for your platform. You need your own Final Fantasy
XIV 1.23b client and the account and endpoint details supplied by the server
operator. You do not need a local MariaDB server or AetherXIV Core.

1. Open Launcher and select the appropriate preset on **Server**. Use
   **Custom Server Setup** for operator-provided endpoints, then save settings.
2. On **Client**, choose **Use Existing Client...**, locate `ffxivboot.exe` or
   `ffxivgame.exe`, and select **Validate Client**.
3. If patching is required, supply your own compatible patch library and follow
   the [patching instructions](LAUNCHER_GUIDE.md#patch-library).
4. On macOS/Linux/SteamOS, validate the bundled compatibility runtime and
   resolve the exact missing dependencies shown by Launcher.
5. Enable Umbra if desired. Use **Safe Mode** when testing without third-party plugins.
6. On **Home**, enter your server account details and select **Log In & Play**.

## Run a local server and client

Use the **Full** download. Install MariaDB Server and the .NET 10 ASP.NET Core
Runtime for your operating system and CPU architecture.

1. Start MariaDB, open Core, and review **Config**. The default local database
   is `ffxiv_server` at `127.0.0.1:3306`.
2. Select **Verify Dependencies** and resolve reported failures.
3. Select **Start Stack**. Provide MariaDB administrator credentials if requested
   for database setup. Review any backup or repair prompt before accepting it.
4. Wait for Map, World, Lobby, and Launcher Services to report **Running**.
5. Open Launcher and save the **Localhost** server preset.
6. Validate the client and runtime as described above, then log in.

Core creates and verifies its application database account during supported
setup. Change the development database password before hosting for other users.
See [database setup and recovery](DATABASE_SETUP_AND_MIGRATION.md).

## Run only the server

Use the **Core** download. It contains the server hosts, Core GUI, database
package, migrations, and runtime data. It does not include Launcher or the
game compatibility runtime. Clients on other computers use a Launcher download
and your [hosted server endpoints](HOSTED_SERVER.md).

## Update an existing installation

Stop the stack and close Launcher. Make an independent database backup, retain
the old release, and extract the new release into a new folder. Start the new
Core and let it classify the database before approving any migration or repair.
Do not merge old binaries into the new release or copy private configuration
files into this repository.
