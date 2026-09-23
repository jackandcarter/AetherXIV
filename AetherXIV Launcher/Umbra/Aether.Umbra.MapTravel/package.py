#!/usr/bin/env python3
"""Build Map Travel and author a standard Umbra custom repository (no installer)."""
import argparse
import hashlib
import json
from pathlib import Path
import subprocess
from urllib.parse import urlsplit
from zipfile import ZipFile, ZIP_DEFLATED


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--base-url", required=True, help="URL that will serve the output directory")
    parser.add_argument("--output", type=Path, required=True)
    args = parser.parse_args()
    url = urlsplit(args.base_url)
    if (url.scheme != "https" and not (url.scheme == "http" and url.hostname in
            {"localhost", "127.0.0.1", "::1"})) or not url.hostname or url.query or url.fragment or url.username or url.password:
        parser.error("base-url must be HTTPS or loopback HTTP, without credentials, query or fragment")
    root = Path(__file__).resolve().parent
    output = args.output.resolve()
    output.mkdir(parents=True, exist_ok=True)
    build = output / "build"
    subprocess.run(["dotnet", "build", str(root / "Aether.Umbra.MapTravel.csproj"),
                    "-c", "Release", "-o", str(build), "--nologo"], check=True)
    manifest = json.loads((root / "umbra-plugin.json").read_text())
    # Enablement belongs to the existing plugin manager after installation.
    manifest["enabled"] = False
    package = output / ("map-travel-" + manifest["version"] + ".zip")
    with ZipFile(package, "w", compression=ZIP_DEFLATED) as archive:
        archive.writestr("umbra-plugin.json", json.dumps(manifest, indent=2) + "\n")
        archive.write(build / manifest["entry"], manifest["entry"])
        archive.write(root / "README.md", "README.md")
    data = package.read_bytes()
    entry = {key: manifest[key] for key in ("id", "name", "version", "api_version",
        "minimum_framework_version", "target_framework", "architecture", "language", "entry")}
    entry.update(download_url=args.base_url.rstrip("/") + "/" + package.name,
        size_bytes=len(data), sha256=hashlib.sha256(data).hexdigest(), built_in=False,
        author="Demi Dev Unit", punchline="Map landing preview development plugin.",
        description="Experimental Map Travel UI. Requires Umbra API 2.1 and framework 2.1.0; native map and server travel bindings are not connected.")
    catalog = output / "umbra-repository.json"
    catalog.write_text(json.dumps(dict(schema_version=1,
        repository_name="Map Travel Development", plugins=[entry]), indent=2) + "\n")
    print(catalog)
    print(package)


if __name__ == "__main__":
    main()
