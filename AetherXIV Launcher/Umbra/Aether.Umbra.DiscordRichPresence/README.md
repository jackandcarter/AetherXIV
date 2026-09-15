# Discord Rich Presence

Shows the FFXIV 1.x client in your Discord profile while the game is running.
With the plugin enabled, Discord displays "Playing Final Fantasy XIV 1.X" (or
whatever the registered application is named) on your profile.

This is the Umbra equivalent of the Discord Rich Presence plugin that Dalamud
ships on modern XIV, and it uses the same RPC client library
([Lachee/discord-rpc-csharp](https://github.com/Lachee/discord-rpc-csharp),
the `DiscordRichPresence` NuGet package).

## How it works

Discord's rich presence is not a web API. Every running Discord desktop client
exposes a local IPC endpoint — `\\.\pipe\discord-ipc-0` on Windows — and your
app connects to it directly. The plugin:

1. Connects to the Discord IPC named pipe.
2. Handshakes with the registered application id.
3. Sends a `SET_ACTIVITY` frame with the application id, artwork key, and the
   process id of the game.
4. Keeps the connection alive (responding to Discord's ping frames) and
   reconnects with backoff if Discord starts later or restarts mid-session.
5. Clears the presence and closes the pipe on shutdown.

No OAuth, no scopes, no voice, no guild features — the basic `SET_ACTIVITY`
path works for any registered application without developer-portal approval.

## The "Playing …" name

The line Discord shows ("Playing **Final Fantasy XIV 1.X**") is **not** set by
this plugin — it is the application name in the Discord Developer Portal
(discord.com/developers/applications). To get the exact status the launcher
advertises, the registered application must be named `Final Fantasy XIV 1.X`.
The application id (`1537597448803975208`) is embedded as the default in the
plugin config and stays valid even if the display name is changed.

Optional artwork: upload a square PNG (512×512 ideal, 128×128 minimum) under
**Rich Presence → Art Assets** and name it `logo`. That key is what the config
sends as `large_image_key`. Without an uploaded asset the status still appears;
the image is just missing.

## Configuration

The plugin writes `discord-rich-presence.json` to its Umbra config directory on
first start:

```json
{
  "client_id": "1537597448803975208",
  "details": null,
  "state": null,
  "large_image_key": "logo",
  "large_image_text": "Final Fantasy XIV 1.X"
}
```

`details` and `state` become the small lines under the game name once a future
framework world-state service can feed them (character, zone, level). Until
then they default to `null` for a clean status.

## Platform notes

- **Windows (native client):** the plugin connects straight to
  `\\.\pipe\discord-ipc-0`. Nothing else is needed.
- **macOS / Linux (Wine):** the client runs under Wine, and Wine cannot hand a
  Windows program a Unix socket. The launcher solves this with a small
  byte-forwarding bridge, `AetherXIV.DiscordBridge.exe`, built from
  `AetherXIV.Launcher.DiscordBridge/discord_bridge.c` and shipped in the
  launcher's `Helpers/win-x64` payload. The launch helper starts it inside the
  prefix before the game; it listens on `\\.\pipe\discord-ipc-0` and relays
  every byte to the host's `discord-ipc-N` Unix socket through raw host
  syscalls (the same technique XIV on Mac ships). The bridge marks itself as a
  Wine system process, so it stays up as long as the prefix and survives the
  launcher closing.

  The bridge is only started when the **Discord Rich Presence** toggle in the
  launcher's Umbra tab is on (it is off by default), and only on Wine
  platforms — native Windows never starts it. If the toggle is off, or Discord
  is not running, the plugin retries its pipe connection with backoff and logs
  the failed connects; enabling the toggle before the next launch starts the
  bridge.

  The bridge's host socket directory comes from the launcher through the
  `AETHER_DISCORD_IPC_DIR` environment variable (resolved from the host's
  `XDG_RUNTIME_DIR`/`TMPDIR` chain), with the same fallbacks Discord itself
  uses for manual runs.

## Build

```shell
dotnet build Aether.Umbra.DiscordRichPresence.csproj -c Release
```

The output of `bin/Release/net10.0-windows` is the plugin package: the entry
assembly, its runtime dependencies (`DiscordRPC.dll` and `Newtonsoft.Json.dll`),
and `umbra-plugin.json`. Umbra discovers the manifest from the plugin directory
and loads the entry assembly (and its dependencies, resolved from the same
folder) in its own collectible context. `Aether.Umbra.PluginApi` must **not** be
included in the package — the framework provides it in the default load
context.

## Distribution

This is a **built-in** plugin: the release build (`tools/Universal/
package-bundled-plugins.py`, wired into the macOS/Linux/Windows package
scripts) compiles it, zips the package, and writes the foundation catalog at
`Umbra/BundledPlugins/repository.json` with `"built_in": true`. The launcher
toggle installs from that bundled package, so the feature works with no update
service, no network, and for self-hosted servers. The framework seeds the
bundled catalog as a supported repository, so the plugin also appears in the
in-game plugin manager with the AetherXIV badge.

When the official update service goes live, its `repository.json` lists the
same plugin (same id/version/SHA-256) with a remote `download_url`. The
launcher prefers the remote entry for updates; while the service is offline the
bundled catalog is simply the fallback and nothing errors.
