#!/usr/bin/env python3
"""Validate a packaged Umbra base framework and its complete file receipt."""

from __future__ import annotations

import argparse
import hashlib
import json
from pathlib import Path


RECEIPT_NAME = "umbra-framework.json"
KNOWN_GAME_SHA256 = "9341f2b4567440b310a4d494f5cc5599ca334ba51c8042247317ff466492f2e9"


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def validate(framework_root: Path) -> None:
    root = framework_root.resolve(strict=True)
    receipt_path = root / RECEIPT_NAME
    receipt = json.loads(receipt_path.read_text(encoding="utf-8"))

    expected_identity = {
        "Name": "Aether Umbra",
        "ApiVersion": "2.0",
        "PlatformRid": "win-x86",
        "InstallPath": ".",
        "BootstrapPath": "Aether.Umbra.Bootstrap.x86.dll",
        "FrameworkPath": "Managed/Aether.Umbra.Framework.dll",
        "IsBundled": True,
    }
    for field, expected in expected_identity.items():
        if receipt.get(field) != expected:
            raise ValueError(
                f"{receipt_path}: {field} must be {expected!r}, got {receipt.get(field)!r}"
            )

    version = (root / "version.txt").read_text(encoding="utf-8").strip()
    if receipt.get("Version") != version:
        raise ValueError(
            f"{receipt_path}: receipt version {receipt.get('Version')!r} "
            f"does not match version.txt {version!r}"
        )

    supported_hashes = {
        str(value).lower() for value in receipt.get("SupportedGameSha256", [])
    }
    if KNOWN_GAME_SHA256 not in supported_hashes:
        raise ValueError(f"{receipt_path}: supported FFXIV 1.23b hash is missing")

    entries = receipt.get("Files")
    if not isinstance(entries, list) or not entries:
        raise ValueError(f"{receipt_path}: Files must contain the complete bundle")

    expected_files: dict[str, tuple[int, str]] = {}
    for entry in entries:
        relative = str(entry.get("path", "")).replace("\\", "/")
        relative_path = Path(relative)
        if (
            not relative
            or relative_path.is_absolute()
            or ".." in relative_path.parts
            or relative == RECEIPT_NAME
        ):
            raise ValueError(f"{receipt_path}: unsafe receipt path {relative!r}")
        if relative.casefold() in expected_files:
            raise ValueError(f"{receipt_path}: duplicate receipt path {relative!r}")
        expected_files[relative.casefold()] = (
            int(entry.get("size_bytes", -1)),
            str(entry.get("sha256", "")).lower(),
        )

    actual_files: dict[str, Path] = {}
    for path in root.rglob("*"):
        if not path.is_file() or path == receipt_path:
            continue
        relative = path.relative_to(root).as_posix()
        actual_files[relative.casefold()] = path

    missing = sorted(set(expected_files) - set(actual_files))
    unsigned = sorted(set(actual_files) - set(expected_files))
    if missing or unsigned:
        raise ValueError(
            f"{receipt_path}: bundle inventory mismatch; missing={missing}, unsigned={unsigned}"
        )

    for relative, path in actual_files.items():
        expected_size, expected_hash = expected_files[relative]
        actual_size = path.stat().st_size
        if actual_size != expected_size:
            raise ValueError(
                f"{path}: size mismatch, expected {expected_size}, got {actual_size}"
            )
        actual_hash = sha256(path)
        if actual_hash != expected_hash:
            raise ValueError(
                f"{path}: SHA-256 mismatch, expected {expected_hash}, got {actual_hash}"
            )

    print(f"verified Umbra base framework {version}: {len(actual_files)} files")


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("framework_root", type=Path)
    args = parser.parse_args()
    validate(args.framework_root)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
