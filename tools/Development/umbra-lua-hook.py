#!/usr/bin/env python3
"""Install and stream the client's matched Lua tutorial-mode calls.

The hook detours ffxivgame.exe+0x9cfa60 (luaD_precall), which runs on every
Lua call. The detour itself records only the two verified tutorial closures
(DesktopWidget:isTutorialMode / orderTutorialMode) into a fixed native ring.
Umbra drains that ring and emits one ``lua.call`` bridge event per matched
call. This tool consumes that event stream with a long poll, so the output
contains the actual native-call timestamp/thread/source metadata rather than
managed counter-poll timestamps.

Usage:
  python3 tools/Development/umbra-lua-hook.py [--control PATH] [--out FILE]

Run this BEFORE reaching the linkpearl step; keep it running through the
Confirm click. It installs the hook, streams matched ``lua.call`` events as
JSONL, and clears the hook on exit (Ctrl-C). The bridge is expected to become
unreachable if the client crashes; records already received are retained.
"""
from __future__ import annotations

import argparse
import json
import sys
import time
import urllib.error
import urllib.parse
import urllib.request

DEFAULT_CONTROL = "/Users/imac/Library/Application Support/Demi Dev Unit/AetherXIV Launcher/Umbra/Cache/DevBridge/control.json"
BASE = "http://127.0.0.1:8797"


def load_token(control_path: str):
    with open(control_path, encoding="utf-8") as fh:
        data = json.load(fh)
    return data.get("token"), data.get("pid"), data.get("port", 8797)


class Bridge:
    def __init__(self, control_path: str):
        self.control_path = control_path
        self.token = None
        self.pid = None
        self.port = 8797
        self.refresh()

    def refresh(self):
        try:
            token, pid, port = load_token(self.control_path)
            if token:
                self.token = token
                self.pid = pid
                self.port = int(port)
        except Exception:
            pass

    def _request(self, method: str, path: str, body: dict | None = None, timeout: float = 35.0):
        if not self.token:
            return 0, {"error": "no-token"}
        data = json.dumps(body).encode("utf-8") if body is not None else None
        req = urllib.request.Request(
            f"http://127.0.0.1:{self.port}{path}",
            data=data,
            method=method,
            headers={
                "Authorization": f"Bearer {self.token}",
                "Content-Type": "application/json",
                "Accept": "application/json",
            },
        )
        try:
            with urllib.request.urlopen(req, timeout=timeout) as resp:
                return resp.status, json.load(resp)
        except urllib.error.HTTPError as error:
            try:
                return error.code, json.load(error)
            except Exception:
                return error.code, {"error": str(error)}
        except Exception as error:
            return 0, {"error": str(error)}

    def events(self, after: int, limit: int = 256, wait_ms: int = 0):
        query = urllib.parse.urlencode({
            "after": max(0, after),
            "limit": max(1, min(4096, limit)),
            "wait_ms": max(0, min(30_000, wait_ms)),
        })
        return self._request("GET", f"/events?{query}", timeout=35.0)

    def install(self, name):
        return self._request("POST", "/lua-hook/install", {"name": name})

    def clear(self):
        return self._request("POST", "/lua-hook/clear", {})


def now_ms() -> int:
    return int(time.time() * 1000)


def consume_event_page(page: dict, cursor: int, limit: int = 256):
    """Return the next event cursor and only the diagnostic records we emit.

    The bridge event cursor covers every bridge event, not just ``lua.call``.
    Advancing over non-Lua events is intentional; otherwise a busy bridge could
    repeatedly replay the same unrelated request events. When a full page is
    returned the individual event sequences, rather than ``latest_sequence``,
    define the safe cursor so the next request cannot skip an unseen event.
    """
    if not isinstance(page, dict):
        return cursor, []

    items = page.get("events", [])
    if not isinstance(items, list):
        return cursor, []

    next_cursor = cursor
    selected = []
    for item in items:
        if not isinstance(item, dict):
            continue
        sequence = item.get("sequence")
        if isinstance(sequence, int):
            next_cursor = max(next_cursor, sequence)
        if item.get("category") in ("lua.call", "lua.call.records_overrun"):
            selected.append(item)

    latest = page.get("latest_sequence")
    if isinstance(latest, int) and len(items) < max(1, limit):
        next_cursor = max(next_cursor, latest)
    return next_cursor, selected


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("--control", default=DEFAULT_CONTROL)
    ap.add_argument("--out", default=None)
    # Kept for compatibility with the earlier command. It now controls the
    # minimum long-poll interval rather than causing repeated status requests.
    ap.add_argument("--interval-ms", type=int, default=200)
    ap.add_argument("--wait-ms", type=int, default=None,
                    help="bridge event long-poll timeout (default: max(1000, interval-ms))")
    ap.add_argument("--name", choices=("linkpearl-tutorial-mode",), default="linkpearl-tutorial-mode")
    args = ap.parse_args()

    bridge = Bridge(args.control)
    out_fh = open(args.out, "a", encoding="utf-8") if args.out else None

    def emit(value, console=True):
        line = value if isinstance(value, str) else json.dumps(value, separators=(",", ":"))
        if out_fh:
            out_fh.write(line + "\n")
            out_fh.flush()
        if console:
            print(line, flush=True)

    # Establish the event cursor before installation. This avoids replaying
    # unrelated bridge history while still capturing every event generated
    # after the hook-install request, including calls emitted before the next
    # long poll starts.
    code, baseline = bridge.events(0, limit=1)
    if code != 200:
        emit({"t": now_ms(), "error": "bridge-unreachable", "status": code, "detail": baseline})
        print(
            "\nThe Umbra dev bridge is not answering. Start the client through the "
            "launcher with the Umbra dev bridge enabled, then run this tool again.",
            file=sys.stderr,
            flush=True,
        )
        return 1
    cursor = int(baseline.get("latest_sequence", 0))

    code, result = bridge.install(args.name)
    if code != 200:
        emit({"t": now_ms(), "error": "install-failed", "status": code, "detail": result})
        print(
            f"\ninstall failed (status {code}): {result.get('error', '') if isinstance(result, dict) else result}\n"
            "The hook was not installed.",
            file=sys.stderr,
            flush=True,
        )
        if out_fh:
            out_fh.close()
        return 1

    emit({"t": now_ms(), "installed": result})
    print(
        f"\nlua-call hook '{args.name}' installed — watching the per-call event stream. Leave this running\n"
        "through the Confirm click; Ctrl-C stops it and clears the hook.\n"
        "\n"
        "  isTutorialMode       -> the client tutorial guard ran\n"
        "  orderTutorialMode    -> the client tutorial-mode arm ran\n"
        "  each lua.call record -> native TSC, thread, closure/proto, source lines, and ESP\n",
        flush=True,
    )

    wait_ms = args.wait_ms if args.wait_ms is not None else max(1000, args.interval_ms)
    wait_ms = max(1000, min(30_000, wait_ms))
    unreachable_reported = False

    try:
        while True:
            code, page = bridge.events(cursor, limit=256, wait_ms=wait_ms)
            if code != 200:
                emit({
                    "t": now_ms(),
                    "error": "bridge-unreachable",
                    "status": code,
                    "detail": page,
                }, console=False)
                if not unreachable_reported:
                    unreachable_reported = True
                    print("bridge unreachable — retrying; records already captured are retained...", file=sys.stderr, flush=True)
                bridge.refresh()
                time.sleep(1.0)
                continue

            unreachable_reported = False
            cursor, matched_items = consume_event_page(page, cursor, limit=256)
            for item in matched_items:
                emit(item)
    except KeyboardInterrupt:
        pass
    finally:
        code, cleared = bridge.clear()
        emit({"t": now_ms(), "cleared": cleared, "status": code})
        if out_fh:
            out_fh.close()

    return 0


if __name__ == "__main__":
    raise SystemExit(main())
