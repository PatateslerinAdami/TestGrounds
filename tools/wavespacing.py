#!/usr/bin/env python3
"""Test the "minion waves collapse together over time" hypothesis from a replay / our wire capture.

Each 0x61 wp0 = a unit's true position at that time. We bucket time into windows and, per window,
compute every minion's nearest-OTHER-minion distance (instantaneous spacing) and report the
distribution. If the median nearest-neighbour distance SHRINKS over the game, waves are collapsing
(losing inter-body spacing) -> they then walk stacked -> constant collision -> teleports.

Champions (0x46 sender) are excluded. Uses each unit's most recent wp0 as its position; a unit is
"present" in a window if it emitted within the last `stale` ms.

Usage: python3 tools/wavespacing.py <file> [window_s=10] [stale_ms=2000]
"""
import json, base64, struct, sys, os, math
from collections import defaultdict

def load(p):
    p=os.path.expanduser(p)
    with open(p) as f:
        h=f.read(1); f.seek(0)
        return json.load(f) if h=="[" else [json.loads(l) for l in f if l.strip()]

def first_wp(b):
    # decode only the first subrecord's wp0 (absolute) — enough for a position sample
    o=5; o+=4; cnt=struct.unpack_from("<h",b,o)[0]; o+=2
    if cnt<=0: return None
    bf=b[o]; o+=1; size=bf>>1; tele=bf&1
    if size<=0: return None
    netid=struct.unpack_from("<I",b,o)[0]; o+=4
    if tele: o+=1
    if size>1: nfb=(size-2)//4+1; o+=nfb
    lx=struct.unpack_from("<h",b,o)[0]; lz=struct.unpack_from("<h",b,o+2)[0]
    return netid,(float(lx),float(lz))

def dist(a,b): return math.hypot(a[0]-b[0],a[1]-b[1])
def pct(xs,q):
    xs=sorted(xs)
    return xs[min(len(xs)-1,int(q*len(xs)))] if xs else float('nan')

def main():
    path=sys.argv[1]
    win=float(sys.argv[2]) if len(sys.argv)>2 else 10.0
    stale=float(sys.argv[3]) if len(sys.argv)>3 else 2000.0
    rows=load(path)
    champs=set()
    for e in rows:
        b=base64.b64decode(e["Bytes"])
        if b and b[0]==0x46 and len(b)>=5: champs.add(struct.unpack_from("<I",b,1)[0])
    rows.sort(key=lambda e:e["Time"])
    t0=rows[0]["Time"]
    latest={}  # netid -> (t,pos)
    # walk chronologically; at each sample, snapshot the live set and compute spacing into the window
    win_samples=defaultdict(list)  # window_idx -> list of nearest-neighbour dists
    for e in rows:
        b=base64.b64decode(e["Bytes"])
        if len(b)<11 or b[0]!=0x61: continue
        fw=first_wp(b)
        if not fw: continue
        netid,pos=fw
        t=e["Time"]
        if netid in champs: continue
        latest[netid]=(t,pos)
        # live minions = emitted within `stale`
        live=[(n,p) for n,(tt,p) in latest.items() if t-tt<=stale and n!=netid]
        if not live: continue
        nn=min(dist(pos,p) for _,p in live)
        # ignore absurd (cross-map) — cap to sane lane neighbour band only when computing
        win_idx=int((t-t0)/1000.0/win)
        win_samples[win_idx].append(nn)

    print(f"file={os.path.basename(path)}  window={win:.0f}s")
    print(f"{'t(s)':>6} {'n':>6} {'nn_med':>7} {'nn_p25':>7} {'nn_p10':>7}   (nearest same-area minion spacing)")
    for wi in sorted(win_samples):
        xs=win_samples[wi]
        print(f"{wi*win:6.0f} {len(xs):6} {pct(xs,.5):7.0f} {pct(xs,.25):7.0f} {pct(xs,.10):7.0f}")
    allx=[d for xs in win_samples.values() for d in xs]
    print(f"\nOVERALL nearest-neighbour: median={pct(allx,.5):.0f}u  p25={pct(allx,.25):.0f}u  p10={pct(allx,.10):.0f}u  min={min(allx):.0f}u")
    print("(minion pathfinding radius ~35u, so two centres < ~70u apart = bodies overlapping;")
    print(" Riot keeps a passed body ~33u from the walked line. Falling median over time = wave collapse.)")

if __name__=="__main__":
    main()
