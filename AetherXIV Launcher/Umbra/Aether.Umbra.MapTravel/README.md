# Map Travel

Development plugin for pin-based travel in AetherXIV Umbra.

Uses `IUmbraMapService` and `IUmbraTravelService`; no raw memory or SharpNav access
is required by the plugin. Enabled plugins run without opening a window at startup.
Open it from Installed → Map Travel → Open, or through `/maptravel` when native
command dispatch is available.

**Not yet connected to live map selection or server travel.** The window reports
unavailable bindings until native map selection and server travel are connected.


This is an installable third-party plugin, not a system plugin or bundled entry.
Use `package.py --base-url <hosting-base-url> --output <directory>` to build a
standard ZIP and Umbra custom repository catalog. The existing repository manager
handles fetching, verification, installation and enablement. Developer discovery
is optional; it does not test repository installation.

Requires Umbra API 2.1 and framework 2.1.0 or later. Native map selection and
server travel are not connected yet; availability is reported by the framework.
