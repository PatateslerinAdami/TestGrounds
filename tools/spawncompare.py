#!/usr/bin/env python3
"""Lane-minion spawn wire-identity check for the engine-inversion refactor.

A lane-minion spawn is an OnEnterVisibilityClient (0xBA) packet that embeds a
Barrack_SpawnUnit (0x03 at body offset, i.e. byte 9) and carries the minion model name
as a length-prefixed ASCII string (e.g. "Blue_Minion_Basic"). In the first ~3 minutes of a
game no inhibitors fall, so the lane-minion spawn series (timing + composition + per-minion
stat bonuses) is purely game-time-driven and therefore DETERMINISTIC and independent of
player/champion behaviour — making it a sound before/after signal for verifying that moving the
spawn loop from the map script into the engine (docs/LANE_MINION_ENGINE_INVERSION_PLAN.md,
Phases 2-3) did not change the wire.

Embedded Barrack_SpawnUnit layout (offsets within the 0xBA packet, [opcode][netid] sub-header):
  9  Barrack_SpawnUnit opcode (0x03)   23  WaveCount (u8)     27  HealthBonus (i16)
  ... ObjectID / node / BarracksNetID  24  MinionType (u8)    29  MinionLevel (u8)
                                       25  DamageBonus (i16)
The stat fields matter from Phase 3 on: the 90s UpgradeMinionTimer ramp is transmitted via
DamageBonus/HealthBonus, so comparing them verifies the per-barrack ramp produces identical
values to the old global ramp.

Usage:
  spawncompare.py <capture.jsonl>                 # summarize a capture's spawn series
  spawncompare.py <baseline.jsonl> <after.jsonl>  # diff two captures (PASS/FAIL)

Capture format: PACKET_LOG JSON-lines, one {Time,Bytes,Channel,Flags} object per packet
(Bytes = base64). Same schema as Riot .rlp.json. Capture both runs the same way, e.g.:
  env PACKET_LOG=/tmp/after-capture.jsonl dotnet run --project GameServerConsole -c Debug
"""
import base64
import json
import re
import struct
import sys
from collections import Counter, namedtuple

Spawn = namedtuple("Spawn", "time model wave mtype dmg hp lvl")

MODEL_RE = re.compile(rb"([A-Za-z][A-Za-z0-9_]*Minion[A-Za-z0-9_]*)")
# Default tolerance for per-spawn timestamp drift (ms). Spawns are scheduled off game time;
# tiny flush jitter is acceptable, a real timing change is not.
TIME_TOL_MS = 50.0


def load(path):
    with open(path) as f:
        head = f.read(1)
        f.seek(0)
        if head == "[":
            rows = json.load(f)
        else:
            rows = [json.loads(line) for line in f if line.strip()]
    return rows


def spawns(path):
    """Return [Spawn, ...] of lane-minion spawns, in capture order."""
    out = []
    for r in load(path):
        b = base64.b64decode(r["Bytes"])
        if len(b) >= 30 and b[0] == 0xBA and b[9] == 0x03:
            m = MODEL_RE.search(b)
            out.append(Spawn(
                time=r["Time"],
                model=m.group(1).decode() if m else "?",
                wave=b[23],
                mtype=b[24],
                dmg=struct.unpack_from("<h", b, 25)[0],
                hp=struct.unpack_from("<h", b, 27)[0],
                lvl=b[29],
            ))
    return out


def summarize(path):
    s = spawns(path)
    print(f"{path}: {len(s)} lane-minion spawns")
    print("  by model:", dict(Counter(x.model for x in s)))
    # Group into waves: a >5s gap between consecutive spawns starts a new wave.
    waves, cur, last = [], [], None
    for sp in s:
        if last is not None and sp.time - last > 5000:
            waves.append(cur)
            cur = []
        cur.append(sp)
        last = sp.time
    if cur:
        waves.append(cur)
    print(f"  {len(waves)} waves:")
    for i, w in enumerate(waves):
        comp = Counter(sp.model.split("_")[-1] for sp in w)
        comp_s = ", ".join(f"{n}×{k}" for k, n in comp.items())
        maxhp = max(sp.hp for sp in w)
        maxdmg = max(sp.dmg for sp in w)
        bonus = f"  bonus≤(hp {maxhp}, dmg {maxdmg})" if (maxhp or maxdmg) else ""
        print(f"    wave {i+1}: t={w[0].time:.0f}-{w[-1].time:.0f}ms  {len(w)} minions  [{comp_s}]{bonus}")


def diff(base_path, after_path, tol=TIME_TOL_MS):
    a = spawns(base_path)
    b = spawns(after_path)
    ok = True
    # Sessions can differ in length (one ran longer). That's fine — compare the common prefix and
    # treat the extra tail as informational, not a failure. A real regression shows up as a per-spawn
    # mismatch within the prefix or a composition difference over the prefix.
    n = min(len(a), len(b))
    if len(a) != len(b):
        print(f"NOTE: spawn counts differ (baseline {len(a)}, after {len(b)}) — "
              f"comparing the common prefix of {n} spawns.")
    mismatches = 0
    for i in range(n):
        x, y = a[i], b[i]
        if x.model != y.model:
            print(f"FAIL @#{i}: model {x.model!r} (t={x.time:.0f}) -> {y.model!r} (t={y.time:.0f})")
            ok = False
            mismatches += 1
        elif abs(x.time - y.time) > tol:
            print(f"FAIL @#{i}: {x.model} time {x.time:.0f} -> {y.time:.0f} (Δ{y.time-x.time:+.0f}ms > {tol}ms)")
            ok = False
            mismatches += 1
        elif (x.wave, x.mtype, x.dmg, x.hp, x.lvl) != (y.wave, y.mtype, y.dmg, y.hp, y.lvl):
            print(f"FAIL @#{i}: {x.model} @t={x.time:.0f} fields "
                  f"(wave,type,dmg,hp,lvl) {(x.wave,x.mtype,x.dmg,x.hp,x.lvl)} -> {(y.wave,y.mtype,y.dmg,y.hp,y.lvl)}")
            ok = False
            mismatches += 1
        if mismatches >= 20:
            print("  ... (further mismatches suppressed)")
            break
    # Composition totals over the common prefix (order-independent).
    ca, cb = Counter(x.model for x in a[:n]), Counter(x.model for x in b[:n])
    if ca != cb:
        print(f"FAIL: composition totals over common prefix differ\n  baseline {dict(ca)}\n  after    {dict(cb)}")
        ok = False
    print()
    if ok:
        print(f"PASS: {n} spawns identical (model + timing within {tol}ms + stat bonuses). Wire-identical.")
        return 0
    print("DIFF DETECTED — the refactor changed lane-minion spawn output.")
    return 1


if __name__ == "__main__":
    args = sys.argv[1:]
    if len(args) == 1:
        summarize(args[0])
    elif len(args) == 2:
        sys.exit(diff(args[0], args[1]))
    else:
        print(__doc__)
        sys.exit(2)
