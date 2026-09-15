#!/usr/bin/env python3
"""Mode-gate transition tracker for the umbra-xwatch stream.

Reads the jsonl stream produced by umbra-xwatch.py and emits a compact
line for every *state transition* (mode gates g40/g41, the pointer-global
cluster, object vtable/field changes, and wedge toggles), including the
elapsed seconds since the previous transition. Raw stream continues to be
written by umbra-xwatch.py; this tool just diffs it.

Usage:
  python3 tools/Development/umbra-modegate-watch.py --in /tmp/xw.jsonl --out /tmp/mg.jsonl
"""

import argparse
import json
import time


def sig(d):
    """A comparable signature of everything we care about in one tick."""
    mode = (d.get("mode", {}).get("g40"), d.get("mode", {}).get("g41"))
    ptrs = tuple(sorted((k, v) for k, v in d.get("ptrs", {}).items()))
    objs = []
    for o in d.get("objs", []):
        oo = {k: o[k] for k in sorted(o) if k not in ("addr",)}
        objs.append((o.get("addr"), oo))
    objs.sort()
    return (mode, ptrs, tuple(objs), d.get("wedge", False))


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--in", dest="src", default="/tmp/xw.jsonl")
    ap.add_argument("--out", default="/tmp/mg.jsonl")
    ap.add_argument("--interval-ms", type=int, default=500)
    args = ap.parse_args()

    out_fh = open(args.out, "w")
    last_sig = None
    last_t = None
    first = True
    pos = 0
    out_fh.write("# umbra-modegate transitions t={}\n".format(int(time.time() * 1000)))
    out_fh.flush()

    while True:
        try:
            with open(args.src) as fh:
                fh.seek(pos)
                lines = fh.readlines()
                pos = fh.tell()
        except Exception:
            lines = []
        for line in lines:
            line = line.strip()
            if not line or line.startswith("#"):
                continue
            try:
                d = json.loads(line)
            except Exception:
                continue
            s = sig(d)
            t = d["t"]
            if first:
                first = False
                last_sig = s
                last_t = t
                continue
            if s != last_sig:
                dt = (t - last_t) / 1000.0
                rec = {
                    "t": t,
                    "dt": round(dt, 3),
                    "mode": {"g40": d["mode"].get("g40"), "g41": d["mode"].get("g41")},
                    "ptrs": d.get("ptrs"),
                    "objs": d.get("objs", []),
                    "wedge": d.get("wedge", False),
                }
                out_fh.write(json.dumps(rec) + "\n")
                out_fh.flush()
                last_sig = s
            last_t = t
        time.sleep(args.interval_ms / 1000.0)


if __name__ == "__main__":
    main()
