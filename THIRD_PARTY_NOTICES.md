# Third-Party Notices

## Project Meteor Server

AetherXIV incorporates and modifies source code and content originally
developed by Project Meteor Server, including portions of the lobby, world,
and map server implementations, database material, and Lua scripts.

Project Meteor Server was published under the GNU Affero General Public
License, version 3 or later.

AetherXIV includes substantial modifications, re-architecture, replacement
components, and extensive original implementation by the AetherXIV
contributors. Files retaining Project Meteor implementation remain attributed
to the Project Meteor contributors.

AetherXIV is an independent project. Project Meteor and its contributors do
not maintain, endorse, support, or bear responsibility for AetherXIV or its
modifications.

Original project:
https://bitbucket.org/Ioncannon/project-meteor-server/

License:
GNU Affero General Public License, version 3 or later

## Dear ImGui

The Umbra framework includes Dear ImGui, Copyright (c) 2014-2026 Omar Cornut
and Dear ImGui contributors, under the MIT License.

The complete license is included in the signed Umbra framework distribution.

## Inter

The signed Umbra framework includes the Inter font family, Copyright (c) 2016 The Inter
Project Authors, under the SIL Open Font License, Version 1.1.

The complete license is included beside the bundled framework font assets.

## AetherXIV Compatibility Runtime (Wine)

The macOS, Linux, and SteamOS Launcher packages include an AetherXIV-built Wine
runtime for the 32-bit FFXIV 1.x client and the in-process Umbra .NET 10 host.
The macOS runtime starts from the checksum-pinned Wine Stable 11.0_1 macOS
binary distribution published by Gcenx, rebuilds only Wine's `wow64cpu.dll`
from the corresponding upstream Wine 11.0 source, and applies the minimal
Rosetta 2 WoW64 transition workaround derived from the open-source Wine
components published by CodeWeavers for CrossOver 26.0.0. The Linux and
SteamOS recipes use those CodeWeavers Wine components directly. AetherXIV does
not redistribute the CrossOver application or use the CrossOver End User
License Agreement.

Upstream Wine 11.0 source archive:
https://dl.winehq.org/wine/source/11.0/wine-11.0.tar.xz

Source SHA-256:
`c07a6857933c1fc60dff5448d79f39c92481c1e9db5aa628db9d0358446e0701`

Wine Stable 11.0_1 macOS binary distribution:
https://github.com/Gcenx/macOS_Wine_builds/releases/download/11.0_1/wine-stable-11.0_1-osx64.tar.xz

Binary SHA-256:
`b50dc50ec7f41d58b115a6b685d4d1315ba3c797bd3aa0f49213f2703cb82388`

CodeWeavers workaround and Linux/SteamOS source archive:
https://media.codeweavers.com/pub/crossover/source/crossover-sources-26.0.0.tar.gz

Source SHA-256:
`544d6ef462e5089017340ccf66df0a8cdc117aa9895d91d7c5d10edaa5fcbc56`

Wine is distributed under the GNU Lesser General Public License, version 2.1
or later. Each compatibility-runtime package contains the complete Wine license,
the exact AetherXIV build patch, its source offer, build provenance, and a
SHA-256 inventory.

The macOS runtime also bundles the x86_64 open-source host libraries needed by
that Wine build, including FreeType, GnuTLS, gettext, p11-kit, libidn2,
libunistring, libtasn1, Nettle, GMP, and libpng. Their provenance and applicable
notices are preserved with the Wine Stable distribution and this notice.

## Wine Discord IPC Bridge Technique

The launcher's `AetherXIV.DiscordBridge.exe` is original AetherXIV code, but it
implements the byte-forwarding bridge technique first popularized by the
wine-discord-ipc-bridge project (which makes Windows games running under Wine
reach Discord's host Unix socket through raw host syscalls) and by
`macOS-wine-bridge` (which XIV on Mac ships as `discord_bridge.exe`). No source
code from those projects is included; AetherXIV's bridge is written from
scratch against the same public Discord IPC protocol.

Reference projects:
https://github.com/0e4ef622/wine-discord-ipc-bridge
https://codeberg.org/jackson/macOS-wine-bridge

## SharpNav and Recast Navigation

AetherXIV distributes SharpNav material, Copyright (c) 2013-2016 Robert
Rouhani and other contributors. SharpNav includes altered source code from
Recast Navigation, Copyright (c) 2009 Mikko Mononen. This material is
distributed under the MIT License.

The complete license is provided at:
`Data/navmesh/SHARPNAV_LICENSE`

## Package Dependencies

AetherXIV builds and release packages may include additional open-source
dependencies restored through package managers. Those components remain
subject to their respective copyright notices and license terms.
