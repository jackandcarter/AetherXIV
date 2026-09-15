#!/usr/bin/env python3
"""Umbra bridge pointer-chase monitor for the tutorial-mode machine (X/Z).

Light instrument (~9 HTTP requests/tick, 250ms) — does NOT heap-scan, so it
won't hammer the bridge like the earlier scan loop did.

Anchors (from docs/development/CLIENT_LPB_FORMAT_AND_TUTORIAL_CONFIRM.md §6):
  X  - tutorial/notice event handler: vtable 0x105706C
       +0x00 vtable | +0x04 string/format buffer | +0x08 sub-object Y (NULL = crash)
       +0x0C second sub-object
  Z  - owner class holding X at Z->field_60; vtable 0xFD5A74 (.rdata)
  Candidate pointer globals registered by Z's ctor (slot 6):
       0x134BE50 / 0x134BE70 / 0x134BE90 / 0x134BFD0

Each tick:
  1. Read mode gate g[0x12D7C40] and sibling g[0x12D7C41] (module offsets).
  2. Read the pointer-global cluster(s); dereference each live-looking
     pointer and read BOTH documented layouts unconditionally:
       X-layout: +0x04 / +0x08 (field_8 — the Confirm-crash deref) / +0x0C
       Z-layout: +0x60 (field_60 -> X) / +0x64 (flag_0x64 teardown gate)
     The packed runtime populates vtables per process, so the constants are
     recorded as hints but NEVER gate the field reads (the 2026-08-11
     capture found a live object at 0x2E00228 whose vtable 0xFE00A4 matched
     neither constant, so its field_8 was never observed).
  3. Log one JSONL line.

Bridge robustness: the dev-bridge HTTP listener can stall internally (see
UmbraDevBridgeService.ServeAsync). This tool re-reads the control file for a
rotated token/pid, uses short per-request timeouts, and — crucially — bails
out of a tick at the FIRST failed peek instead of burning 9x timeout sleeps
(the old behavior stretched one wedged tick to ~40 s and hid the wedge).
A failed tick emits an explicit "bridge-wedge" record so the log stays
readable and the cadence recovers immediately when the bridge returns.

Usage:
  python3 tools/Development/umbra-xwatch.py --control PATH [--interval-ms 250] [--out FILE]
"""
import argparse
import json
import time
import urllib.request
import urllib.error

DEFAULT_CONTROL = "/Users/imac/Library/Application Support/Demi Dev Unit/AetherXIV Launcher/Umbra/Cache/DevBridge/control.json"
BASE = "http://127.0.0.1:8797"
# The injected framework reads the game's own address space, so image-resident
# globals must be addressed module-relative (base + RVA) — never via a
# hardcoded 0x400000 image base. /memory/peek's PeekModule path resolves the
# real main-module base, which is what the breakpoint and lua-hook tools
# already rely on (their plans target the main module, ffxivgame.exe).
MAIN_MODULE = "ffxivgame.exe"

# Global pointer candidates (registered by Z ctor slot 6), as RVAs
# (absolute 0x134BE50..0x134BFD0 minus the 0x400000 image base).
PTR_GLOBALS = [0xF4BE50, 0xF4BE70, 0xF4BE90, 0xF4BFD0]

# Mode gate globals as RVAs (absolute 0x12D7C40 / 0x12D7C41 - 0x400000).
MODE_OFFSETS = {
    "g40": 0xED7C40,
    "g41": 0xED7C41,
}

VTABLE_X = 0x105706C
VTABLE_Z = 0xFD5A74
VTABLE_X_BASE = 0xFD4F48


def load_token(control_path: str):
    with open(control_path) as fh:
        data = json.load(fh)
    return data.get("token"), data.get("pid")


class Bridge:
    def __init__(self, control_path: str):
        self.control_path = control_path
        self.token = None
        self.pid = None
        self.last_error = None
        self.refresh()

    def refresh(self):
        try:
            token, pid = load_token(self.control_path)
            if token:
                self.token = token
                self.pid = pid
        except Exception:
            pass

    def _post(self, path: str, body: dict, timeout: float = 4.0):
        if not self.token:
            self.last_error = "no-token"
            return 0, {"error": "no-token"}
        req = urllib.request.Request(
            f"{BASE}{path}",
            data=json.dumps(body).encode(),
            headers={"Authorization": f"Bearer {self.token}", "Content-Type": "application/json"},
        )
        try:
            with urllib.request.urlopen(req, timeout=timeout) as resp:
                payload = json.load(resp)
                if resp.status != 200:
                    self.last_error = payload.get("error") if isinstance(payload, dict) else str(payload)
                else:
                    self.last_error = None
                return resp.status, payload
        except urllib.error.HTTPError as e:
            try:
                payload = json.load(e)
            except Exception:
                payload = {"error": str(e)}
            self.last_error = payload.get("error") if isinstance(payload, dict) else str(e)
            return e.code, payload
        except Exception as e:
            self.last_error = str(e)
            return 0, {"error": str(e)}

    def peek(self, address: int, size: int):
        code, res = self._post("/memory/peek", {"address": address, "size": size})
        if code != 200 or not res.get("success"):
            return None
        raw = bytes.fromhex(res.get("hex", ""))
        return raw if len(raw) >= size else None

    def peek_module(self, offset: int, size: int, module: str = MAIN_MODULE):
        code, res = self._post("/memory/peek", {"module": module, "offset": offset, "size": size})
        if code != 200 or not res.get("success"):
            return None
        raw = bytes.fromhex(res.get("hex", ""))
        return raw if len(raw) >= size else None

    def u32(self, address: int):
        raw = self.peek(address, 4)
        return int.from_bytes(raw, "little") if raw else None

    def u32_module(self, offset: int, module: str = MAIN_MODULE):
        raw = self.peek_module(offset, 4, module)
        return int.from_bytes(raw, "little") if raw else None

    def read_mode(self):
        out = {}
        for key, offset in MODE_OFFSETS.items():
            raw = self.peek_module(offset, 1)
            out[key] = raw[0] if raw else None
        return out

    def get(self, path: str, timeout: float = 4.0):
        if not self.token:
            self.last_error = "no-token"
            return 0, {"error": "no-token"}
        req = urllib.request.Request(
            f"{BASE}{path}",
            headers={"Authorization": f"Bearer {self.token}"},
        )
        try:
            with urllib.request.urlopen(req, timeout=timeout) as resp:
                payload = json.load(resp)
                self.last_error = None if resp.status == 200 else str(payload)
                return resp.status, payload
        except Exception as e:
            self.last_error = str(e)
            return 0, {"error": str(e)}

    def main_module_base(self):
        code, res = self.get("/modules")
        if code != 200:
            return None
        for module in (res.get("modules") or []):
            if (module.get("name") or "").lower() == MAIN_MODULE.lower():
                base = module.get("base_address")
                try:
                    return int(base, 16)
                except (TypeError, ValueError):
                    return None
        return None


def inspect(bridge: Bridge, addr: int):
    """Read BOTH documented layouts for a candidate object address.

    Returns None if any field read fails (the bridge wedged mid-tick), so
    the caller can bail immediately instead of burning more timeouts.
    """
    def u32(offset: int):
        value = bridge.u32(addr + offset)
        return value if value is not None else None

    vt = bridge.u32(addr)
    if vt is None:
        return None

    rec = {
        "kind": "X" if vt in (VTABLE_X, VTABLE_X_BASE) else ("Z" if vt == VTABLE_Z else "?"),
        "addr": f"0x{addr:X}",
        "vtable": f"0x{vt:X}",
    }

    # X-layout fields — always read, never gated on the vtable hint.
    field4 = u32(0x04)
    field8 = u32(0x08)
    fieldc = u32(0x0C)
    if field4 is None or field8 is None or fieldc is None:
        return None
    rec["x_field4"] = field4
    rec["x_field8"] = field8
    rec["x_fieldc"] = fieldc
    if field8 and field8 > 0x10000:
        y20 = bridge.u32(field8 + 0x20)
        rec["y_byte20"] = y20 if y20 is not None else None

    # Z-layout fields — always read. field_60 -> X; flag_0x64 teardown gate.
    z60 = u32(0x60)
    z64 = u32(0x64)
    if z60 is None or z64 is None:
        return None
    rec["z_field60"] = z60
    rec["z_flag64"] = z64
    if z60 and z60 > 0x10000:
        xp_vt = bridge.u32(z60)
        xp_field8 = bridge.u32(z60 + 0x08)
        rec["x_vtable"] = f"0x{xp_vt:X}" if xp_vt is not None else None
        rec["x_field8"] = xp_field8
        if xp_field8 and xp_field8 > 0x10000:
            y20 = bridge.u32(xp_field8 + 0x20)
            rec["y_byte20"] = y20 if y20 is not None else None

    return rec


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--control", default=DEFAULT_CONTROL)
    ap.add_argument("--interval-ms", type=int, default=250)
    ap.add_argument("--out", default=None)
    args = ap.parse_args()

    bridge = Bridge(args.control)
    out_fh = open(args.out, "a") if args.out else None

    base = bridge.main_module_base()
    print(
        "# main module base = {}".format(("0x%X" % base) if base else "unresolved (bridge not ready?)"),
        flush=True,
    )

    def emit(line, console=True):
        if out_fh:
            out_fh.write(line + "\n")
            out_fh.flush()
        if console:
            print(line, flush=True)

    print("# umbra-xwatch pointer-chase — watching (Ctrl-C to stop)", flush=True)
    emit("# umbra-xwatch pointer-chase t={}".format(int(time.time() * 1000)))
    consecutive_failures = 0
    last_token_refresh = 0.0
    while True:
        tick = int(time.time() * 1000)

        # Re-read the control file so a client restart (new pid/token) is
        # picked up without restarting this tool.
        if time.time() - last_token_refresh > 10.0:
            bridge.refresh()
            last_token_refresh = time.time()

        rec = {"t": tick, "mode": {}, "objs": [], "ptrs": {}}
        failed = False

        # 1. Mode gates. Bail at the first failed peek so one wedged
        #    request costs ~4 s, not 9 x 4 s.
        for key, offset in MODE_OFFSETS.items():
            raw = bridge.peek_module(offset, 1)
            if raw is None:
                failed = True
                break
            rec["mode"][key] = raw[0]

        # 2. Pointer cluster + object fields.
        if not failed:
            for g in PTR_GLOBALS:
                v = bridge.u32_module(g)
                if v is None:
                    failed = True
                    break
                rec["ptrs"][f"0x{g:X}"] = v
                if v and v > 0x10000:
                    obj = inspect(bridge, v)
                    if obj is None:
                        failed = True
                        break
                    rec["objs"].append(obj)

        if not failed:
            consecutive_failures = 0
            emit(json.dumps(rec), console=False)
        else:
            consecutive_failures += 1
            rec["wedge"] = True
            rec["consecutive_failures"] = consecutive_failures
            rec["pid"] = bridge.pid
            rec["error"] = bridge.last_error
            rec["mode"] = {k: None for k in MODE_OFFSETS} if not rec["mode"] else rec["mode"]
            if consecutive_failures == 1:
                print("bridge wedge detected — retrying...", file=sys.stderr, flush=True)
            emit(json.dumps(rec), console=False)

        time.sleep(args.interval_ms / 1000.0)


if __name__ == "__main__":
    main()
