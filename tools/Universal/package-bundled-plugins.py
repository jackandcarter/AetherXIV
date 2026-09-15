#!/usr/bin/env python3
"""Builds the built-in Umbra plugin packages and their foundation catalog.

The output directory is a self-contained "bundled plugins" payload that ships
inside the launcher next to the bundled framework:

  repository.json    - foundation catalog. Every plugin it lists is marked
                       ``built_in: true`` and its ``download_url`` names the
                       package file relative to this directory, so the launcher
                       and the in-game framework can install it with no network
                       and no update service. When the official update service
                       goes live, its repository.json will list the same plugin
                       (same id/version/sha256) with a remote download_url, and
                       that becomes the update path.
  <id>-<version>.zip - the plugin package. This is the exact artifact the
                       update service will serve, so SHA-256 verification is
                       identical for bundled and downloaded installs.

Each project added to the built-in catalog in the future follows the same
shape: build it, zip its package directory, and append its catalog entry with
``built_in: true``.
"""

import argparse
import hashlib
import json
import os
import shutil
import subprocess
import sys
import tempfile
import zipfile
from datetime import datetime, timezone

REPOSITORY_ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
REPOSITORY_NAME = "AetherXIV Built-in"
AUTHOR = "Demi Dev Unit"
DESCRIPTION = "Shows Final Fantasy XIV 1.X in your Discord profile as Rich Presence."

# The framework provides Aether.Umbra.PluginApi in the default load context, so
# plugin packages must not carry their own copy (or its deps/pdb sidecars).
EXCLUDED_BUILD_PREFIXES = ("Aether.Umbra.PluginApi.",)


def ensure_plugin_api_feed(dotnet):
    """Make Aether.Umbra.PluginApi available to restore for the plugin build.

    The package is not published to nuget.org; it is packed into the
    repository-local feed referenced by the plugin's nuget.config. Fresh
    checkouts (CI runners and new machines) have no global NuGet cache entry,
    so the feed must be populated before building any bundled plugin.
    """
    helper = os.path.join(
        REPOSITORY_ROOT,
        "tools",
        "Universal",
        "package-umbra-pluginapi.py",
    )
    subprocess.run([sys.executable, helper, "--dotnet", dotnet], check=True)


def build_plugin(project, output_build, dotnet):
    subprocess.run(
        [
            dotnet,
            "build",
            project,
            "-c",
            "Release",
            "--nologo",
            "-o",
            output_build,
            "/p:NuGetAudit=false",
        ],
        check=True,
    )


def stage_package(build_dir, package_dir):
    os.makedirs(package_dir, exist_ok=True)
    for name in sorted(os.listdir(build_dir)):
        source = os.path.join(build_dir, name)
        if not os.path.isfile(source):
            continue
        if name.endswith(".pdb") or name.startswith(EXCLUDED_BUILD_PREFIXES):
            continue
        shutil.copy2(source, os.path.join(package_dir, name))


def load_manifest(package_dir):
    with open(os.path.join(package_dir, "umbra-plugin.json"), encoding="utf-8") as manifest_file:
        return json.load(manifest_file)


def write_repository(output_dir, zip_name, size_bytes, sha256, manifest):
    entry = {
        "id": manifest["id"],
        "name": manifest["name"],
        "version": manifest["version"],
        "api_version": manifest["api_version"],
        "author": AUTHOR,
        "description": DESCRIPTION,
        "download_url": zip_name,
        "size_bytes": size_bytes,
        "sha256": sha256,
        "minimum_framework_version": manifest.get("minimum_framework_version", "2.0.0"),
        "is_active": True,
        "built_in": True,
    }
    repository = {
        "schema_version": 1,
        "sequence": 1,
        "generated_at": datetime.now(timezone.utc).strftime("%Y-%m-%dT%H:%M:%SZ"),
        "repository_name": REPOSITORY_NAME,
        "plugins": [entry],
        "resources": [],
    }
    repository_path = os.path.join(output_dir, "repository.json")
    with open(repository_path, "w", encoding="utf-8") as repository_file:
        json.dump(repository, repository_file, indent=2)
        repository_file.write("\n")
    return repository_path


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument(
        "--output",
        required=True,
        help="Directory that receives repository.json and the plugin packages.",
    )
    parser.add_argument(
        "--project",
        help="Plugin project to build (defaults to the bundled Discord Rich Presence plugin).",
    )
    parser.add_argument(
        "--dotnet",
        default=shutil.which("dotnet") or "/usr/local/share/dotnet/dotnet",
        help="Path to the dotnet CLI.",
    )
    args = parser.parse_args()

    output_dir = os.path.abspath(args.output)
    os.makedirs(output_dir, exist_ok=True)

    if args.project:
        project = os.path.abspath(args.project)
    else:
        project = os.path.join(
            REPOSITORY_ROOT,
            "AetherXIV Launcher",
            "Umbra",
            "Aether.Umbra.DiscordRichPresence",
            "Aether.Umbra.DiscordRichPresence.csproj",
        )
    if not os.path.isfile(project):
        print(f"Plugin project not found: {project}", file=sys.stderr)
        return 2

    ensure_plugin_api_feed(args.dotnet)

    with tempfile.TemporaryDirectory(prefix="aetherxiv-bundled-plugins.") as work_root:
        build_dir = os.path.join(work_root, "build")
        package_dir = os.path.join(work_root, "package")
        build_plugin(project, build_dir, args.dotnet)
        stage_package(build_dir, package_dir)
        manifest = load_manifest(package_dir)

        zip_name = f"{manifest['id']}-{manifest['version']}.zip"
        zip_path = os.path.join(output_dir, zip_name)
        with zipfile.ZipFile(zip_path, "w", zipfile.ZIP_DEFLATED) as archive:
            for name in sorted(os.listdir(package_dir)):
                archive.write(os.path.join(package_dir, name), arcname=name)

        with open(zip_path, "rb") as archive_file:
            sha256 = hashlib.sha256(archive_file.read()).hexdigest()
        size_bytes = os.path.getsize(zip_path)
        repository_path = write_repository(output_dir, zip_name, size_bytes, sha256, manifest)

    print(f"bundled_plugin_package={zip_path} size_bytes={size_bytes} sha256={sha256[:16]}...")
    print(f"bundled_repository={repository_path}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
