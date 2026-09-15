#!/usr/bin/env python3
"""Install and stream a tutorial-mode code breakpoint via the Umbra bridge.

Two plans are available (--name):

  tutorial-mode-confirm  ffxivgame.exe+0x497358. The mode-change handler's
                         `mov al,[esp+0x38]`. `mode_byte` is the value it is
                         about to compare against g[0x12d7c40] (mode==1 arms
                         X->field_8). `x` (ESI, saved from ECX at 0x897337)
                         is the real X object address. Values:
                           0 -> no-op branch (handler ran, mode never armed)
                           1 -> attach branch (X->field_8 should be attached)
                           2 -> sibling branch

  tutorial-mode-attach   ffxivgame.exe+0x4973CB. The mode==1 attach arm's
                         `mov [esi+0x8],edi` (X->field_8 = Y). `x` (ESI) is
                         X and `y` (EDI) is Y (zero when the 0x2c allocation
                         failed). A hit with non-zero `y` proves the attach
                         happened and names the exact X to watch at Confirm.

  tutorial-mode-gate     ffxivgame.exe+0x497541. Inside the 0x497530 wrapper,
                         right after `call edx` (S->vtbl[7]()) and before the
                         `cmp al,0x1` gate. `mode_byte` is AL = the vtbl[7]
                         result: 1 -> modeChange (0x497310) will run; 0 -> the
                         else branch (0x496510). `x` (ESI) is S (mode-source
                         object), `y` (EDI) is X (ItemCommandBase). No hit
                         means the wrapper never fired.

  vtable-fd785c-dispatch ffxivgame.exe+0x3493E0. The scalar-deleting-
                         destructor dispatch target of the class whose vtable
                         sits at 0xFD785C (the vtable the Confirm-click crash
                         unwinds through). `ecx` (captured at detour entry) is
                         `this` — the object pointer: 0 -> the dispatch ran on
                         a NULL object (the crash), non-zero -> the object
                         existed and the NULL deref is deeper. `esp` is the
                         entry stack; `x`/`y` are the caller's ESI/EDI.

Usage:
  python3 tools/Development/umbra-breakpoint.py [--control PATH] [--out FILE]
                                                [--name tutorial-mode-attach]

Run this BEFORE reaching the linkpearl step; keep it running through the
Confirm click. It installs the breakpoint, streams hits as JSONL, and clears
the breakpoint on exit (Ctrl-C).
"""
import argparse
import json
import sys
import time
import urllib.request
import urllib.error

DEFAULT_CONTROL = "/Users/imac/Library/Application Support/Demi Dev Unit/AetherXIV Launcher/Umbra/Cache/DevBridge/control.json"
BASE = "http://127.0.0.1:8797"
PLANS = ("tutorial-mode-confirm", "tutorial-mode-attach", "tutorial-mode-gate", "vtable-fd785c-dispatch")


def load_token(control_path: str):
    with open(control_path) as fh:
        data = json.load(fh)
    return data.get("token"), data.get("pid")


class Bridge:
    def __init__(self, control_path: str):
        self.control_path = control_path
        self.token = None
        self.pid = None
        self.refresh()

    def refresh(self):
        try:
            token, pid = load_token(self.control_path)
            if token:
                self.token = token
                self.pid = pid
        except Exception:
            pass

    def _request(self, method: str, path: str, body: dict = None, timeout: float = 5.0):
        if not self.token:
            return 0, {"error": "no-token"}
        data = json.dumps(body).encode() if body is not None else None
        req = urllib.request.Request(
            f"{BASE}{path}",
            data=data,
            method=method,
            headers={
                "Authorization": f"Bearer {self.token}",
                "Content-Type": "application/json",
            },
        )
        try:
            with urllib.request.urlopen(req, timeout=timeout) as resp:
                return resp.status, json.load(resp)
        except urllib.error.HTTPError as e:
            try:
                return e.code, json.load(e)
            except Exception:
                return e.code, {"error": str(e)}
        except Exception as e:
            return 0, {"error": str(e)}

    def install(self, name):
        return self._request("POST", "/breakpoint/install", {"name": name})

    def status(self):
        return self._request("GET", "/breakpoints")

    def clear(self):
        return self._request("POST", "/breakpoint/clear", {})


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--control", default=DEFAULT_CONTROL)
    ap.add_argument("--out", default=None)
    ap.add_argument("--interval-ms", type=int, default=50)
    ap.add_argument("--name", choices=PLANS, default="tutorial-mode-confirm")
    args = ap.parse_args()

    bridge = Bridge(args.control)
    out_fh = open(args.out, "a") if args.out else None

    def emit(line, console=True):
        if out_fh:
            out_fh.write(line + "\n")
            out_fh.flush()
        if console:
            print(line, flush=True)

    code, result = bridge.install(args.name)
    if code != 200:
        detail = result if isinstance(result, dict) else {}
        reason = detail.get("error", "") if detail else ""
        emit(json.dumps({"t": int(time.time() * 1000), "error": "install-failed", "status": code, "detail": result}))
        print(
            f"\ninstall failed (status {code}): {reason}\n"
            "The Umbra dev bridge is not answering on 127.0.0.1:8797.\n"
            "Start the client through the launcher with the Umbra dev bridge\n"
            "ENABLED (a launch log must contain '--umbra-enabled true'), then\n"
            "run this tool again.",
            file=sys.stderr,
            flush=True,
        )
        sys.exit(1)

    emit(json.dumps({"t": int(time.time() * 1000), "installed": result}))
    print(
        f"\nbreakpoint '{args.name}' installed — watching. Leave this running\n"
        "through the Confirm click; Ctrl-C stops it and clears the breakpoint.\n",
        flush=True,
    )
    last_hits = result.get("hits", 0)
    last_refresh = time.time()
    unreachable_reported = False

    try:
        while True:
            if time.time() - last_refresh > 10.0:
                bridge.refresh()
                last_refresh = time.time()

            code, snap = bridge.status()
            if code != 200:
                emit(json.dumps({
                    "t": int(time.time() * 1000),
                    "error": "bridge-unreachable",
                    "status": code,
                    "detail": snap,
                }), console=False)
                if not unreachable_reported:
                    unreachable_reported = True
                    print("bridge unreachable — retrying...", file=sys.stderr, flush=True)
                time.sleep(1.0)
                continue
            unreachable_reported = False

            hits = snap.get("hits", 0)
            if hits != last_hits:
                last_hits = hits
                emit(json.dumps({
                    "t": int(time.time() * 1000),
                    "hits": hits,
                    "mode_byte": snap.get("mode_byte"),
                    "eax_before": snap.get("eax_before"),
                    "eax_after": snap.get("eax_after"),
                    "esp": snap.get("esp"),
                    "x": snap.get("x"),
                    "y": snap.get("y"),
                    "ecx": snap.get("ecx"),
                }))

            time.sleep(args.interval_ms / 1000.0)
    except KeyboardInterrupt:
        pass
    finally:
        code, cleared = bridge.clear()
        emit(json.dumps({"t": int(time.time() * 1000), "cleared": cleared, "status": code}))


if __name__ == "__main__":
    main()
