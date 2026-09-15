#!/usr/bin/env python3
"""Packs Aether.Umbra.PluginApi into the local NuGet feed used by the bundled-plugin build.

The bundled Discord Rich Presence plugin compiles against the
``Aether.Umbra.PluginApi`` package while the framework provides that contract at
runtime, so the package is intentionally not published to nuget.org. Every
build that packages the bundled plugin therefore needs the package available to
NuGet restore. This script packs it into the repository-local feed referenced
by ``AetherXIV Launcher/Umbra/Aether.Umbra.DiscordRichPresence/nuget.config``:

    {repo}/.umbra-nuget/Aether.Umbra.PluginApi.{version}.nupkg

The CI workflow calls this before each platform build, and
``tools/Universal/package-bundled-plugins.py`` calls it automatically whenever
the package is missing, so fresh checkouts on runners or local machines build
without relying on a pre-warmed global NuGet cache.

When the plugin's PackageReference to Aether.Umbra.PluginApi is bumped, bump
``--package-version`` here (and in the plugin project) to match.
"""

import argparse
import os
import shutil
import subprocess
import sys

REPOSITORY_ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
PLUGIN_API_PROJECT = os.path.join(
    REPOSITORY_ROOT,
    "AetherXIV Launcher",
    "Umbra",
    "Aether.Umbra.PluginApi",
    "Aether.Umbra.PluginApi.csproj",
)
DEFAULT_FEED = os.path.join(REPOSITORY_ROOT, ".umbra-nuget")


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument(
        "--feed",
        default=DEFAULT_FEED,
        help="Local NuGet feed directory (default: <repo>/.umbra-nuget).",
    )
    parser.add_argument(
        "--package-version",
        default="2.0.0",
        help="PluginApi package version; must match the plugin's PackageReference.",
    )
    parser.add_argument(
        "--dotnet",
        default=shutil.which("dotnet") or "/usr/local/share/dotnet/dotnet",
        help="Path to the dotnet CLI.",
    )
    parser.add_argument(
        "--force",
        action="store_true",
        help="Repack even when the package is already present.",
    )
    args = parser.parse_args()

    feed = os.path.abspath(args.feed)
    os.makedirs(feed, exist_ok=True)
    package = os.path.join(feed, f"Aether.Umbra.PluginApi.{args.package_version}.nupkg")
    if os.path.isfile(package) and not args.force:
        print(f"pluginapi_feed_cached={package}")
        return 0

    subprocess.run(
        [
            args.dotnet,
            "pack",
            PLUGIN_API_PROJECT,
            "--configuration",
            "Release",
            "--output",
            feed,
            "/p:PackageVersion=" + args.package_version,
            "/p:NuGetAudit=false",
        ],
        check=True,
    )
    if not os.path.isfile(package):
        print(f"dotnet pack did not produce {package}", file=sys.stderr)
        return 1

    print(f"pluginapi_feed_packed={package}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
