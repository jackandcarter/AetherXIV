# Using Umbra

Umbra 2.1.2 ships inside the matching Launcher download. Players do not need an
SDK, a private project checkout, or a separate .NET installation to use the
bundled framework.

## Enable the framework

On Launcher's **Umbra** tab, enable Umbra and launch the supported 1.23b client.
**Safe Mode** starts the framework without third-party plugins. The future
official framework-update service is separate from the in-game Plugin Manager;
the bundled verified framework works without that service.

## Install and manage plugins

Open the in-game Umbra Plugin Manager. Its repository, discovery, installed,
and update controls manage plugins. Launcher does not install individual plugins.

1. Add a trusted custom HTTPS repository in **Repositories**. A GitHub repository
   homepage can be used when it hosts `umbra-repository.json` on its default branch.
2. Choose **View plugins** or open **Discover**, select the compatible package,
   and install it.
3. In **Installed**, enable the plugin and use its **Open** or settings action.
4. Use **Updates** for a newer compatible package from the same source.

Custom repositories are checksum-verified and remain labelled unreviewed.
Only install plugins from authors you trust: managed plugins execute inside
the game process and are not an operating-system sandbox.

The bundled catalog includes Discord Rich Presence. Factory is optional and
separately installable; it is not included in the application release. The SDK
sample, Map Travel, and diagnostic packages are separate developer/optional
plugins. Do not assume that a package is available from the offline official
service just because an API exists.

## Developer locations

Plugin authors can enable developer-plugin loading, add a local output folder,
and rescan/reload it. Under Wine, host folders may be visible through `Z:`.
Developer files remain owned by the author rather than being managed as an
installed package. Start with the [SDK quickstart](../UMBRA_SDK.md).

## Recover from a failure

Try **Safe Mode** to isolate third-party plugins. Disable the suspect plugin
before loading others again. Three consecutive callback failures can quarantine
a plugin. Uninstall uses recoverable plugin trash; it does not delete the game.

An individual plugin update keeps a previous package for rollback. The framework
and native bootstrap instead form one integrity-checked bundle: restore the
matching Launcher release rather than copying individual framework DLLs.

For reports, include the exact client build, Umbra version, plugin versions,
platform/runtime, and relevant [logs](../DEBUGGING_AND_BUG_REPORTING.md). An
unavailable adapter should display its reason rather than guessed client values.
