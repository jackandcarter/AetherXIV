#!/usr/bin/env python3
"""Authenticated local companion for the read-only Umbra development bridge."""

from __future__ import annotations

import argparse
import json
import os
import sys
import time
import urllib.error
import urllib.parse
import urllib.request
from pathlib import Path
from typing import Any


def candidate_control_paths() -> list[Path]:
    candidates: list[Path] = []
    explicit = os.environ.get("AETHER_UMBRA_DEV_BRIDGE_CONTROL")
    if explicit:
        candidates.append(Path(explicit).expanduser())

    home = Path.home()
    candidates.extend(
        [
            home
            / "Library"
            / "Application Support"
            / "Demi Dev Unit"
            / "AetherXIV Launcher"
            / "Umbra"
            / "Cache"
            / "DevBridge"
            / "control.json",
            home
            / ".config"
            / "Demi Dev Unit"
            / "AetherXIV Launcher"
            / "Umbra"
            / "Cache"
            / "DevBridge"
            / "control.json",
        ]
    )
    local_app_data = os.environ.get("LOCALAPPDATA")
    if local_app_data:
        candidates.append(
            Path(local_app_data)
            / "Demi Dev Unit"
            / "AetherXIV Launcher"
            / "Umbra"
            / "Cache"
            / "DevBridge"
            / "control.json"
        )
    return candidates


def resolve_control_path(explicit: str | None) -> Path:
    if explicit:
        path = Path(explicit).expanduser().resolve()
        if not path.is_file():
            raise RuntimeError(f"Umbra control file does not exist: {path}")
        return path

    for path in candidate_control_paths():
        if path.is_file():
            return path.resolve()
    raise RuntimeError(
        "Could not find Umbra's control.json. Pass --control with its full path."
    )


class Bridge:
    def __init__(self, control_path: Path) -> None:
        self.control_path = control_path

    def _control(self) -> dict[str, Any]:
        value = json.loads(self.control_path.read_text(encoding="utf-8"))
        token = value.get("token")
        if not isinstance(token, str) or len(token) != 64:
            raise RuntimeError(
                "The bridge control file has no valid token. Launch the updated "
                "managed Umbra framework once so it can repair the control state."
            )
        return value

    def request(
        self,
        method: str,
        path: str,
        payload: dict[str, Any] | None = None,
    ) -> dict[str, Any]:
        control = self._control()
        port = int(control.get("port", 8797))
        token = str(control["token"])
        body = None if payload is None else json.dumps(payload).encode("utf-8")
        request = urllib.request.Request(
            f"http://127.0.0.1:{port}{path}",
            data=body,
            method=method,
            headers={
                "Authorization": f"Bearer {token}",
                "Accept": "application/json",
                "Content-Type": "application/json",
            },
        )
        try:
            with urllib.request.urlopen(request, timeout=35) as response:
                return json.loads(response.read().decode("utf-8"))
        except urllib.error.HTTPError as error:
            detail = error.read().decode("utf-8", errors="replace")
            raise RuntimeError(f"Bridge returned HTTP {error.code}: {detail}") from error
        except urllib.error.URLError as error:
            raise RuntimeError(
                f"Umbra bridge is unavailable on 127.0.0.1:{port}: {error.reason}"
            ) from error


def print_json(value: object) -> None:
    print(json.dumps(value, indent=2, sort_keys=True))


def add_capture_metadata(parser: argparse.ArgumentParser) -> None:
    parser.add_argument("--name", default="gameplay-test")
    parser.add_argument("--correlation-id")
    parser.add_argument("--server-run-id")
    parser.add_argument("--test-case")
    parser.add_argument("--note")


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--control", help="Full path to Umbra DevBridge/control.json")
    commands = parser.add_subparsers(dest="command", required=True)

    for name in ("status", "snapshot", "capabilities", "modules", "watches"):
        commands.add_parser(name)

    events = commands.add_parser("events")
    events.add_argument("--after", type=int)
    events.add_argument("--limit", type=int, default=100)
    events.add_argument("--wait-ms", type=int, default=0)

    watch = commands.add_parser("watch")
    watch.add_argument("--after", type=int, default=0)
    watch.add_argument("--limit", type=int, default=200)
    watch.add_argument("--wait-ms", type=int, default=30_000)

    logs = commands.add_parser("logs")
    logs.add_argument("--limit", type=int, default=120)

    capture_start = commands.add_parser("capture-start")
    add_capture_metadata(capture_start)

    mark = commands.add_parser("mark")
    mark.add_argument("label")
    mark.add_argument("--note")

    commands.add_parser("capture-pause")
    commands.add_parser("capture-stop")
    commands.add_parser("capture-export")

    watch_start = commands.add_parser("memory-watch-start")
    watch_start.add_argument("name")
    watch_start.add_argument("module")
    watch_start.add_argument("offset")
    watch_start.add_argument("--size", type=int, default=4)
    watch_start.add_argument("--interval-ms", type=int, default=500)

    watch_stop = commands.add_parser("memory-watch-stop")
    watch_stop.add_argument("id")

    args = parser.parse_args()
    try:
        bridge = Bridge(resolve_control_path(args.control))
        if args.command in {"status", "snapshot", "capabilities", "modules", "watches"}:
            print_json(bridge.request("GET", f"/{args.command}"))
        elif args.command == "events":
            query = {"limit": max(1, args.limit)}
            if args.after is not None:
                query["after"] = max(0, args.after)
                query["wait_ms"] = min(30_000, max(0, args.wait_ms))
            print_json(
                bridge.request("GET", f"/events?{urllib.parse.urlencode(query)}")
            )
        elif args.command == "watch":
            sequence = max(0, args.after)
            while True:
                query = urllib.parse.urlencode(
                    {
                        "after": sequence,
                        "limit": max(1, args.limit),
                        "wait_ms": min(30_000, max(0, args.wait_ms)),
                    }
                )
                page = bridge.request("GET", f"/events?{query}")
                for item in page.get("events", []):
                    print(json.dumps(item, sort_keys=True), flush=True)
                    sequence = max(sequence, int(item.get("sequence", sequence)))
        elif args.command == "logs":
            print_json(
                bridge.request("GET", f"/logs?limit={max(1, args.limit)}")
            )
        elif args.command == "capture-start":
            print_json(
                bridge.request(
                    "POST",
                    "/capture/start",
                    {
                        "name": args.name,
                        "correlation_id": args.correlation_id,
                        "server_run_id": args.server_run_id,
                        "test_case": args.test_case,
                        "note": args.note,
                    },
                )
            )
        elif args.command == "mark":
            print_json(
                bridge.request(
                    "POST",
                    "/capture/mark",
                    {"label": args.label, "note": args.note},
                )
            )
        elif args.command == "memory-watch-start":
            print_json(
                bridge.request(
                    "POST",
                    "/watch/start",
                    {
                        "name": args.name,
                        "module": args.module,
                        "offset": args.offset,
                        "size": args.size,
                        "interval_ms": args.interval_ms,
                    },
                )
            )
        elif args.command == "memory-watch-stop":
            print_json(
                bridge.request("POST", "/watch/stop", {"id": args.id})
            )
        else:
            path = {
                "capture-pause": "/capture/pause",
                "capture-stop": "/capture/stop",
                "capture-export": "/capture/export",
            }[args.command]
            print_json(bridge.request("POST", path, {}))
        return 0
    except KeyboardInterrupt:
        return 130
    except (OSError, ValueError, RuntimeError, json.JSONDecodeError) as error:
        print(f"error: {error}", file=sys.stderr)
        return 1


if __name__ == "__main__":
    raise SystemExit(main())
