#!/usr/bin/env python3
"""Aggregate our server-side PATH_LOG (pathlog.jsonl from env PATH_LOG=<path>).

Each line is one ISSUED minion path that broadcast:
  {"t":ms,"net":id,"reason":..,"n":count,"chord":..,"look":..,"depth":..,"clr":..,"bodyd":..}

Reports, overall and per reason, the same geometry tools/minionroute.py extracts from a Riot
replay, so the two diff directly.

Riot 4.20 baseline (343e3502, minionroute.py):
  chord median ~277u | look median ~110u | depth median ~25u
  body-route passed-body clearance from WALKED path (clr) median ~33u
Our wire showed clr ~5u and chord/look ~2x Riot -> we route THROUGH bodies + plan too long/early.

Usage: python3 tools/pathlogstats.py /tmp/claude-1000/pathlog.jsonl
"""
import json, sys, os
from collections import Counter, defaultdict

def pct(xs, q):
    xs = sorted(x for x in xs if x is not None and x >= 0)
    if not xs: return float('nan')
    return xs[min(len(xs)-1, int(q*len(xs)))]

rows = [json.loads(l) for l in open(os.path.expanduser(sys.argv[1])) if l.strip()]
if not rows:
    print("empty"); sys.exit(0)

span = (max(r["t"] for r in rows) - min(r["t"] for r in rows)) / 1000.0
print(f"file={os.path.basename(sys.argv[1])}  span={span:.0f}s  issued-paths={len(rows)}  ({len(rows)/max(span,1):.1f}/s)")
print(f"reason mix: {dict(Counter(r['reason'] for r in rows))}")
print()

def report(label, rs):
    if not rs: return
    chord=[r["chord"] for r in rs]; look=[r["look"] for r in rs]
    depth=[r["depth"] for r in rs]
    clr=[r["clr"] for r in rs if r["clr"]>=0]
    bodyd=[r["bodyd"] for r in rs if r["bodyd"]>=0]
    nhist=dict(sorted(Counter(r["n"] for r in rs).items()))
    body_share = 100*len(bodyd)/len(rs)
    print(f"[{label}]  n={len(rs)}  ({100*len(rs)/len(rows):.0f}% of all)")
    print(f"   chord  median={pct(chord,.5):.0f}  p90={pct(chord,.9):.0f}    (Riot ~277)")
    print(f"   look   median={pct(look,.5):.0f}  p90={pct(look,.9):.0f}    (Riot ~110)")
    print(f"   depth  median={pct(depth,.5):.0f}  p90={pct(depth,.9):.0f}    (Riot ~25)")
    print(f"   body-route share={body_share:.0f}%  clr median={pct(clr,.5):.0f} p25={pct(clr,.25):.0f}   (Riot ~33)   bodyd median={pct(bodyd,.5):.0f}")
    print(f"   n-hist: {nhist}")
    print()

report("ALL", rows)
by_reason = defaultdict(list)
for r in rows: by_reason[r["reason"]].append(r)
for reason in sorted(by_reason, key=lambda k: -len(by_reason[k])):
    report(reason, by_reason[reason])
