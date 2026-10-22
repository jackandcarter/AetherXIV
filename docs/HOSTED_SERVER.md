# Hosting a server

Use a **Core** archive to run the server without Launcher, or a **Full** archive
for a local server and client. Install the [server dependencies](BUILD_AND_RUNTIME_DEPENDENCIES.md),
complete [database setup](DATABASE_SETUP_AND_MIGRATION.md), and keep independent
backups before accepting player data.

## Bind and advertise addresses

The bind endpoint is where a process listens. The advertise address is what
other services or game clients are told to connect to. A remote client cannot
reach your server through `127.0.0.1`; on that client, it means its own computer.

For a server using `game.example.com` and an HTTPS Launcher Services proxy:

| Core setting | Example |
|---|---|
| Map endpoint and advertise | `127.0.0.1:1989` |
| World endpoint | `0.0.0.0:54992` |
| World advertise | `game.example.com:54992` |
| Lobby endpoint | `0.0.0.0:54994` |
| Lobby advertise | `game.example.com:54994` |
| Launcher service endpoint | `127.0.0.1:8080` |
| Launcher service advertise | `launcher.example.com:443` |
| World Map Route | `127.0.0.1:1989` |

Replace the example hostnames with your own reachable DNS names. Configure the
reverse proxy to preserve `/launcher` and forward to `http://127.0.0.1:8080`.
The Launcher profile then uses `https://launcher.example.com/launcher`, server
host `game.example.com`, Lobby port 54994, and World port 54992. Leave Patch Base
URL empty unless you host an actual HTTPS patch repository.

Expose TCP 443, 54994, and 54992 for this layout. Keep the database, Map backend,
and proxy-only HTTP listener private. The game ports are direct TCP endpoints;
an ordinary HTTP reverse proxy does not replace them.

Core can reject a public advertise address paired with a loopback-only
World/Lobby bind. **Use Public World/Lobby Binds** changes those listeners while
preserving their ports. Stop and restart the stack after endpoint changes and
check the actual listener addresses in the logs.

## Maintenance and status

The Services tab provides character maintenance, backups, online counts, and
realm status. Read the [Core guide](AETHERXIV_CORE_GUIDE.md) before restoring or
permanently deleting characters. Online maintenance depends on matching runtime
heartbeats and rechecks; a missing remote heartbeat is not proof that every
character is offline.

Realm type, region, and markers are operator labels. Health-warning reasons
and telemetry availability are shown separately. Missing counts appear unknown
rather than implying zero players.

## Updates and reports

Back up the database, stop the stack, extract a new release into a separate
folder, and review Core's migration or repair prompts. Keep old binaries out
of the new release. Redact credentials and player data before posting
[logs or bug reports](DEBUGGING_AND_BUG_REPORTING.md).
