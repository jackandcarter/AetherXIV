#!/usr/bin/env python3
"""Recover narrowly attributable enemy events; never infer spawn or combat parameters.

Run with /usr/bin/python3 on macOS if the default Python lacks expat.
Original chat/player names are deliberately not exported.
"""
import argparse
import collections
import hashlib
import json
from pathlib import Path
import re
import xml.etree.ElementTree as ET

ENEMIES = ("watchwolf", "scarred watchwolf", "Zahar'ak drubber",
           "Zahar'ak halberdier", "battle drake", "young raptor")
NUMBER = {"Two": 2, "Three": 3, "Four": 4}


def parse_event(text):
    for enemy in ENEMIES:
        escaped = re.escape(enemy)
        patterns = (
            ("ready", rf"The {escaped} readies (?P<action>[^.!]+)[.!]"),
            ("hit", rf"(?:Critical! )?The {escaped}'s (?P<action>[^.!]+?) hits .+? for (?P<damage>\d+) points? of damage\."),
            ("miss", rf"The {escaped}'s (?P<action>[^.!]+?) misses .+?\."),
            ("parry", rf".+? (?P<partial>partially )?parries the {escaped}'s (?P<action>[^.!]+?), taking (?P<damage>\d+) points? of damage\."),
            ("block", rf".+? (?P<partial>partially )?blocks the {escaped}'s (?P<action>[^.!]+?), taking (?P<damage>\d+) points? of damage\."),
            ("link", rf"The {escaped} calls for help\. (?P<count>Two|Three|Four) more enemies join the fight!"),
        )
        for kind, pattern in patterns:
            match = re.fullmatch(pattern, text)
            if not match:
                continue
            fields = {k: v for k, v in match.groupdict().items() if v is not None}
            if "action" in fields:
                direction = re.search(r" from the (left|right|rear|front)$", fields["action"])
                if direction:
                    fields["direction"] = direction.group(1)
                    fields["action"] = fields["action"][:direction.start()]
            if "damage" in fields:
                fields["damage"] = int(fields["damage"])
            if "count" in fields:
                fields["count"] = NUMBER[fields["count"]]
            if "partial" in fields:
                fields["partial"] = True
            if kind == "hit":
                fields["critical"] = text.startswith("Critical! ")
            return {"enemy": enemy, "kind": kind, **fields}
    return None


def mine(source, output):
    events, sources, excluded = [], [], []
    hashes = set()
    for path in sorted(source.glob("*_Log.xml")):
        digest = hashlib.sha256(path.read_bytes()).hexdigest()
        if "Unmatched" in path.name or digest in hashes:
            excluded.append({"file": path.name, "sha256": digest,
                             "reason": "derivative unmatched log" if "Unmatched" in path.name else "identical file"})
            continue
        hashes.add(digest)
        rows = ET.parse(path).getroot().findall("Line")
        sources.append({"file": path.name, "sha256": digest, "line_elements": len(rows),
                        "date_basis": "filename only; exact client patch unverified"})
        for index, row in enumerate(rows, 1):
            event = parse_event(row.get("Value", ""))
            if event:
                events.append({"file": path.name, "line_element_index": index,
                               "time": row.get("Time"), "chat_code": row.get("Key"), **event})
    profiles = []
    for enemy in ENEMIES:
        rows = [e for e in events if e["enemy"] == enemy]
        actions = []
        for action in sorted({e["action"] for e in rows if "action" in e and e["action"] != "attack"}):
            matches = [e for e in rows if e.get("action") == action]
            actions.append({"name": action, "outcomes": dict(collections.Counter(e["kind"] for e in matches)),
                            "sources": sorted({e["file"] for e in matches}),
                            "runtime_command_id": None})
        profiles.append({"enemy": enemy, "actions": actions,
                         "link_counts_observed": sorted({e["count"] for e in rows if e["kind"] == "link"}),
                         "actor_class_id": None, "spawn": None,
                         "status": "evidence recovered; runtime identity and parameters unresolved"})
    output.mkdir(parents=True, exist_ok=True)
    for name, data in [("combat-events-expanded.json", events), ("enemy-profiles.json", profiles),
                       ("combat-source-manifest.json", {"included": sources, "excluded": excluded})]:
        (output / name).write_text(json.dumps(data, indent=2) + "\n")
    print(json.dumps({"events": len(events), "kinds": dict(collections.Counter(e["kind"] for e in events)),
                      "named_enemy_action_pairs": sum(len(p["actions"]) for p in profiles)}, indent=2))


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("source", type=Path)
    parser.add_argument("output", type=Path)
    args = parser.parse_args()
    mine(args.source, args.output)
