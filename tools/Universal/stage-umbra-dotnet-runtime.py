#!/usr/bin/env python3
"""Stage the private framework-dependent .NET runtime used by Umbra."""

from __future__ import annotations

import argparse
import json
import os
import shutil
import subprocess
from pathlib import Path


RUNTIME_PACKAGE = "microsoft.netcore.app.runtime.win-x86"
HOST_PACKAGE = "microsoft.netcore.app.host.win-x86"


def global_packages_root(dotnet: str) -> Path:
    configured = os.environ.get("NUGET_PACKAGES")
    if configured:
        return Path(configured).expanduser().resolve()

    result = subprocess.run(
        [dotnet, "nuget", "locals", "global-packages", "--list"],
        check=True,
        capture_output=True,
        text=True,
    )
    _, separator, value = result.stdout.partition(":")
    if not separator or not value.strip():
        raise RuntimeError("dotnet did not report the NuGet global-packages directory")
    return Path(value.strip()).expanduser().resolve()


def requested_runtime_version(runtime_config: Path) -> str:
    document = json.loads(runtime_config.read_text(encoding="utf-8"))
    options = document.get("runtimeOptions", {})
    if options.get("includedFrameworks"):
        raise ValueError(
            f"{runtime_config}: Umbra must be framework-dependent; "
            "self-contained component hosting is not supported"
        )

    framework = options.get("framework")
    if not isinstance(framework, dict):
        frameworks = options.get("frameworks", [])
        framework = next(
            (
                candidate
                for candidate in frameworks
                if candidate.get("name") == "Microsoft.NETCore.App"
            ),
            None,
        )
    if not isinstance(framework, dict) or framework.get("name") != "Microsoft.NETCore.App":
        raise ValueError(f"{runtime_config}: Microsoft.NETCore.App framework is missing")

    version = str(framework.get("version", "")).strip()
    if not version.startswith("10."):
        raise ValueError(f"{runtime_config}: expected a .NET 10 runtime, got {version!r}")
    return version


def resolve_runtime_package_version(packages: Path, requested: str) -> str:
    package_root = packages / RUNTIME_PACKAGE
    requested_package = package_root / requested
    if requested_package.is_dir():
        return requested
    raise FileNotFoundError(
        f"required {RUNTIME_PACKAGE} package {requested} was not restored under {package_root}"
    )


def copy_tree_contents(source: Path, destination: Path) -> None:
    if not source.is_dir():
        raise FileNotFoundError(source)
    destination.mkdir(parents=True, exist_ok=True)
    for item in source.iterdir():
        target = destination / item.name
        if item.is_dir():
            shutil.copytree(item, target, dirs_exist_ok=True)
        else:
            shutil.copy2(item, target)


def stage(managed: Path, runtime_root: Path, dotnet: str) -> str:
    managed = managed.resolve(strict=True)
    runtime_config = managed / "Aether.Umbra.Framework.runtimeconfig.json"
    requested_version = requested_runtime_version(runtime_config)
    packages = global_packages_root(dotnet)
    version = resolve_runtime_package_version(packages, requested_version)
    runtime_package = packages / RUNTIME_PACKAGE / version
    host_package = packages / HOST_PACKAGE / version

    shared = runtime_root / "shared" / "Microsoft.NETCore.App" / version
    fxr = runtime_root / "host" / "fxr" / version
    if runtime_root.exists():
        shutil.rmtree(runtime_root)

    copy_tree_contents(runtime_package / "runtimes" / "win-x86" / "lib" / "net10.0", shared)
    copy_tree_contents(runtime_package / "runtimes" / "win-x86" / "native", shared)
    fxr.mkdir(parents=True, exist_ok=True)
    shutil.copy2(shared / "hostfxr.dll", fxr / "hostfxr.dll")
    shutil.copy2(shared / "hostfxr.dll", runtime_root / "hostfxr.dll")

    host_native = host_package / "runtimes" / "win-x86" / "native"
    shutil.copy2(host_native / "nethost.dll", runtime_root / "nethost.dll")
    for name in ("LICENSE.TXT", "THIRD-PARTY-NOTICES.TXT"):
        source = runtime_package / name
        if source.is_file():
            shutil.copy2(source, runtime_root / name)

    required = (
        fxr / "hostfxr.dll",
        shared / "hostpolicy.dll",
        shared / "coreclr.dll",
        shared / "System.Private.CoreLib.dll",
    )
    missing = [str(path) for path in required if not path.is_file()]
    if missing:
        raise FileNotFoundError(f"private Umbra runtime is incomplete: {missing}")

    marker = runtime_root / "umbra-runtime.json"
    marker.write_text(
        json.dumps(
            {
                "schema_version": 1,
                "framework": "Microsoft.NETCore.App",
                "version": version,
                "runtime_identifier": "win-x86",
                "hosting_mode": "framework-dependent-component",
            },
            indent=2,
        )
        + "\n",
        encoding="utf-8",
    )
    return version


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--managed", required=True, type=Path)
    parser.add_argument("--runtime", required=True, type=Path)
    parser.add_argument("--dotnet", default="dotnet")
    args = parser.parse_args()
    version = stage(args.managed, args.runtime.resolve(), args.dotnet)
    print(f"staged private Umbra .NET runtime {version} (win-x86)")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
