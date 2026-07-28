#!/usr/bin/env python3
"""
AetherXIV
Copyright (C) 2026 Demi Dev Unit

This file is part of AetherXIV.
See THIRD_PARTY_NOTICES.md for historical and third-party attribution.

SPDX-License-Identifier: AGPL-3.0-or-later
"""

from __future__ import annotations

import argparse
from pathlib import Path


WORKSPACE = Path(__file__).resolve().parents[2]

ORIGINAL_SOURCE_ROOTS = (
    Path("src/AetherXIV.ClientData"),
    Path("src/AetherXIV.Core"),
    Path("src/AetherXIV.Data"),
    Path("src/AetherXIV.Launcher.Contracts"),
    Path("src/AetherXIV.Launcher.Host"),
    Path("src/AetherXIV.Operator"),
    Path("src/AetherXIV.Protocol"),
    Path("src/AetherXIV.Server.Hosting"),
    Path("src/AetherXIV.UI.App"),
    Path("tests"),
    Path("AetherXIV Launcher"),
)

EXCLUDED_PARTS = {"bin", "obj", "vendor", "assets", "Image"}
EXCLUDED_FILES = {
    Path(
        "AetherXIV Launcher/AetherXIV.Launcher.Core/"
        "AetherXIVCoreCommon/Blowfish.cs"
    ),
}
SUPPORTED_EXTENSIONS = {".cs", ".cpp", ".h", ".axaml"}


def is_app_source(relative_path: Path) -> bool:
    return (
        relative_path.parts[:2]
        == ("AetherXIV Launcher", "AetherXIV.Launcher.App")
        or relative_path.parts[:2] == ("src", "AetherXIV.UI.App")
    )


def c_style_header(include_third_party_reference: bool) -> str:
    attribution = (
        " * See THIRD_PARTY_NOTICES.md for historical and third-party attribution.\n"
        if include_third_party_reference
        else ""
    )
    return (
        "/*\n"
        " * AetherXIV\n"
        " * Copyright (C) 2026 Demi Dev Unit\n"
        " *\n"
        " * This file is part of AetherXIV.\n"
        f"{attribution}"
        " *\n"
        " * AetherXIV is free software: you may redistribute it and/or modify it\n"
        " * under the terms of the GNU Affero General Public License as published by\n"
        " * the Free Software Foundation, either version 3 of the License, or\n"
        " * (at your option) any later version.\n"
        " *\n"
        " * SPDX-License-Identifier: AGPL-3.0-or-later\n"
        " */\n\n"
    )


def xml_header(include_third_party_reference: bool) -> str:
    attribution = (
        "\n  See THIRD_PARTY_NOTICES.md for historical and third-party attribution."
        if include_third_party_reference
        else ""
    )
    return (
        "<!--\n"
        "  AetherXIV\n"
        "  Copyright (C) 2026 Demi Dev Unit\n"
        "\n"
        "  This file is part of AetherXIV."
        f"{attribution}\n"
        "\n"
        "  SPDX-License-Identifier: AGPL-3.0-or-later\n"
        "-->\n\n"
    )


def candidates() -> list[Path]:
    files: set[Path] = set()
    for relative_root in ORIGINAL_SOURCE_ROOTS:
        root = WORKSPACE / relative_root
        if not root.exists():
            continue
        for path in root.rglob("*"):
            if not path.is_file() or path.suffix.lower() not in SUPPORTED_EXTENSIONS:
                continue
            relative_path = path.relative_to(WORKSPACE)
            if relative_path in EXCLUDED_FILES:
                continue
            if any(part in EXCLUDED_PARTS for part in relative_path.parts):
                continue
            if path.name.endswith((".Designer.cs", ".g.cs")):
                continue
            files.add(path)
    return sorted(files)


def expected_header(path: Path) -> str:
    relative_path = path.relative_to(WORKSPACE)
    include_third_party_reference = not is_app_source(relative_path)
    if path.suffix.lower() == ".axaml":
        return xml_header(include_third_party_reference)
    return c_style_header(include_third_party_reference)


def has_aetherxiv_header(text: str) -> bool:
    return (
        "Copyright (C) 2026 Demi Dev Unit" in text[:1600]
        and "SPDX-License-Identifier: AGPL-3.0-or-later" in text[:1600]
    )


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument(
        "--check",
        action="store_true",
        help="report original AetherXIV source files missing the standard header",
    )
    args = parser.parse_args()

    missing: list[Path] = []
    updated: list[Path] = []
    for path in candidates():
        text = path.read_text(encoding="utf-8-sig")
        if has_aetherxiv_header(text):
            continue
        if args.check:
            missing.append(path.relative_to(WORKSPACE))
            continue
        path.write_text(expected_header(path) + text, encoding="utf-8")
        updated.append(path.relative_to(WORKSPACE))

    if missing:
        for path in missing:
            print(path)
        print(f"{len(missing)} original source file(s) are missing license headers.")
        return 1

    if updated:
        print(f"Added AetherXIV headers to {len(updated)} original source file(s).")
    else:
        print("All classified original source files have AetherXIV headers.")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
