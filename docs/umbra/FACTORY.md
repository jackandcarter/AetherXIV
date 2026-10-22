# Factory appearance mods

Factory is an optional Umbra plugin for installing `.factory` appearance mod
packs. It is separately installable and is not bundled with the Core or
Launcher release. Obtain a compatible plugin package from a trusted source
and install it through the in-game Plugin Manager.

## Install and manage mods

Enable Factory in **Installed**, then choose **Open**. Its left pane lists
installed mods, with search and a footer **+** menu. Choose Install to open the
file picker and select a `.factory` pack. Removal requires holding Shift while
activating Delete.

Select a mod to change its enabled state, priority and pack-defined options.
The UI reads these options from the pack rather than hard-coding a gear item
or skin. Higher-priority mappings and layered content affect how overlapping
mods resolve; inspect the selected pack's changed items and conflicts.

## Apply changes

Use **Apply** after changing enabled mods or options. This publishes the current
resource mappings for subsequent loads; it does not force an immediate actor
redraw. A normal equipment change or character reload may be needed to expose
newly loaded resources. Forced redraw is unavailable in the current native API.

To restore normal resources, disable the mod and Apply, then allow a normal
reload. Factory and Umbra use copies and caches outside the client installation.
Keep originals and authoring masters separate; do not overwrite client assets.

## Compatibility

Resource mappings are shared by logical path on your client. Other actors using
the same asset can therefore receive the same replacement. Per-character
collections are not implemented.

Packs must match the supported 1.23b resource format and model/UV layout. A
modern XIV mod is not a drop-in legacy mod. The active native adapter supports
PC texture replacements and bounded stock Highlander body edits, not arbitrary
new skeletons, topology or material/shader replacement.

Tattoo or body-hair layers need a compatible base texture and UV layout. A layer
authored for a stock body may not fit a different body option; follow the pack
author's declared compatibility. Do not redistribute extracted game assets in
public packs without the appropriate rights.

See [resource loading and appearance](RESOURCE_AND_APPEARANCE.md) for API scope,
cache behavior and ownership, and use Umbra's log when an import or apply fails.
