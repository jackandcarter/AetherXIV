# Map and travel service boundaries

The public plugin API defines map-pin observations and authenticated travel
requests. These contracts do not grant arbitrary client-side teleportation.
Map Travel is a separate development plugin and is not bundled with the
application downloads.

Declare `client.map.read` for `IUmbraMapService`, `travel.preview` for preview
requests, and both `travel.preview` and `travel.warp` for a resolved travel
request. Check service and adapter availability before offering actions.

A map pin includes zone/map/floor identity, world X/Z, a session ID and a
selection revision. It does not invent an elevation. A preview request must
be resolved by the server and returns landing candidates plus an expiring opaque
token. A warp request takes that token and a candidate ID rather than an
arbitrary client elevation. Plugin authors do not need a navigation-mesh library
to consume the contract.

The authenticated travel transport and native map-selection integration are
not connected as a generally available travel feature in this release. Present
the adapter's unavailable reason; do not substitute a guessed height, movement
packet, native function call, or locally forged authorization token.
