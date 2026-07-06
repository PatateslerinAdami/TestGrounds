#!/usr/bin/env python3
"""Characterise HOW Riot routes a minion around obstacles (esp. other minions) on the wire.

Decodes WaypointGroup (0x61) like wpath.py, then for every MINION multi-waypoint path (n>=3)
measures the GEOMETRY of the detour and whether it is routing around another unit:

  detour_depth = max perpendicular distance of an intermediate waypoint from the straight
                 chord wp0->wpLast  (= "how far it bends out", the go-around spacing)
  lookahead    = distance from wp0 (the unit's current pos) to the apex waypoint
                 (= is the bend computed EARLY/far-ahead, or LAST-MINUTE/close?)
  chord_len    = wp0->wpLast straight distance (how long the issued path is)

Body-route correlation: maintains every unit's latest position (each 0x61 re-anchors wp0 to
the true position), and for each n>=3 path finds the nearest OTHER unit to the chord at emit
time. If one sits within `near` of the chord, the path is counted as a BODY route, and we
record that unit's perpendicular clearance from BOTH the straight chord and the detoured
polyline -> the gap Riot keeps when sliding past a body.

Usage: python3 tools/minionroute.py <replay.rlp.json | our_capture.jsonl> [near=250]
"""
import json, base64, struct, sys, os, math
from collections import Counter, defaultdict

def load(p):
    p=os.path.expanduser(p)
    with open(p) as f:
        h=f.read(1); f.seek(0)
        return json.load(f) if h=="[" else [json.loads(l) for l in f if l.strip()]

def parse(b):
    o=5; sync=struct.unpack_from("<i",b,o)[0]; o+=4
    cnt=struct.unpack_from("<h",b,o)[0]; o+=2; out=[]
    for _ in range(cnt):
        bf=b[o]; o+=1; size=bf>>1; tele=bf&1; netid=0; wps=[]
        if size>0:
            netid=struct.unpack_from("<I",b,o)[0]; o+=4
            if tele: o+=1
            if size>1: nfb=(size-2)//4+1; fbb=b[o:o+nfb]; o+=nfb
            else: fbb=b"\x00"
            fbit=lambda i:(fbb[i//8]>>(i%8))&1
            lx=struct.unpack_from("<h",b,o)[0]; o+=2; lz=struct.unpack_from("<h",b,o)[0]; o+=2
            wps.append((float(lx),float(lz))); fl=0
            for _i in range(1,size):
                if fbit(fl): lx+=struct.unpack_from("<b",b,o)[0]; o+=1
                else: lx=struct.unpack_from("<h",b,o)[0]; o+=2
                fl+=1
                if fbit(fl): lz+=struct.unpack_from("<b",b,o)[0]; o+=1
                else: lz=struct.unpack_from("<h",b,o)[0]; o+=2
                fl+=1
                wps.append((float(lx),float(lz)))
        out.append((netid,size,tele,wps))
    return sync,out

def sub(a,b): return (a[0]-b[0], a[1]-b[1])
def dot(a,b): return a[0]*b[0]+a[1]*b[1]
def norm(a): return math.hypot(a[0],a[1])
def dist(a,b): return math.hypot(a[0]-b[0],a[1]-b[1])

def perp_to_seg(p,a,b):
    """Perpendicular-ish distance of p to segment a-b (clamped param)."""
    ab=sub(b,a); L2=dot(ab,ab)
    if L2<1e-9: return dist(p,a)
    t=max(0.0,min(1.0,dot(sub(p,a),ab)/L2))
    proj=(a[0]+ab[0]*t, a[1]+ab[1]*t)
    return dist(p,proj)

def perp_to_polyline(p,pts):
    return min(perp_to_seg(p,pts[i],pts[i+1]) for i in range(len(pts)-1))

def pct(xs,q):
    if not xs: return float('nan')
    xs=sorted(xs); return xs[min(len(xs)-1,int(q*len(xs)))]

def main():
    path=sys.argv[1]
    near=float(sys.argv[2]) if len(sys.argv)>2 else 250.0
    rows=load(path)
    # champions = 0x46 sender (S2C_HeroStats) -> exclude from "minion"
    champs=set()
    for e in rows:
        b=base64.b64decode(e["Bytes"])
        if b and b[0]==0x46 and len(b)>=5: champs.add(struct.unpack_from("<I",b,1)[0])

    # chronological pass; track latest pos per unit; collect minion n>=3 geometry
    latest={}   # netid -> (t, pos)
    depths=[]; lookaheads=[]; chords=[]; napex=[]
    body_routes=0; total_multi=0
    body_clear_chord=[]; body_clear_poly=[]
    rows.sort(key=lambda e: e["Time"])
    for e in rows:
        b=base64.b64decode(e["Bytes"])
        if len(b)<11 or b[0]!=0x61: continue
        t=e["Time"]
        try: sync,mv=parse(b)
        except: continue
        for netid,n,tele,wps in mv:
            if not wps: continue
            # update position table (wp0 = true pos at emit)
            latest[netid]=(t,wps[0])
            if netid in champs: continue
            if n<3: continue
            total_multi+=1
            chord_a, chord_b = wps[0], wps[-1]
            clen=dist(chord_a,chord_b)
            # apex = intermediate wp with max perp distance to chord
            best=0.0; apex=None
            for w in wps[1:-1]:
                d=perp_to_seg(w,chord_a,chord_b)
                if d>best: best=d; apex=w
            if apex is None: continue
            depths.append(best); chords.append(clen)
            lookaheads.append(dist(chord_a,apex))
            napex.append(n)
            # body-route: nearest OTHER unit to the chord at emit time
            best_u=None; best_d=1e9
            for onet,(ot,opos) in latest.items():
                if onet==netid: continue
                if t-ot>1500: continue   # stale (>1.5s) -> skip
                d=perp_to_seg(opos,chord_a,chord_b)
                # also require it to be roughly between the endpoints (param in [0,1])
                ab=sub(chord_b,chord_a); L2=dot(ab,ab)
                tt=dot(sub(opos,chord_a),ab)/L2 if L2>1e-9 else -1
                if d<best_d and 0.05<tt<0.95:
                    best_d=d; best_u=opos
            if best_u is not None and best_d<near:
                body_routes+=1
                body_clear_chord.append(best_d)
                body_clear_poly.append(perp_to_polyline(best_u,wps))

    print(f"file={os.path.basename(path)}   near_thresh={near:.0f}u")
    print(f"minion multi-waypoint (n>=3) paths analysed: {total_multi}")
    print()
    print("DETOUR DEPTH (max perp from straight chord = how far it bends out / go-around spacing):")
    print(f"  median={pct(depths,.5):.0f}u  p75={pct(depths,.75):.0f}u  p90={pct(depths,.9):.0f}u  max={max(depths) if depths else 0:.0f}u")
    print("LOOKAHEAD (wp0 -> apex distance = EARLY[far] vs LAST-MINUTE[close]):")
    print(f"  median={pct(lookaheads,.5):.0f}u  p25={pct(lookaheads,.25):.0f}u  p75={pct(lookaheads,.75):.0f}u  p90={pct(lookaheads,.9):.0f}u")
    print("CHORD LEN (wp0 -> wpLast issued-path length):")
    print(f"  median={pct(chords,.5):.0f}u  p75={pct(chords,.75):.0f}u  p90={pct(chords,.9):.0f}u")
    print(f"n-of-multipath hist: {dict(sorted(Counter(napex).items()))}")
    print()
    print(f"BODY-ROUTE share (another unit within {near:.0f}u of the chord, between endpoints):")
    print(f"  {body_routes}/{total_multi} = {100*body_routes/max(total_multi,1):.0f}%")
    if body_clear_chord:
        print(f"  blocking unit's clearance from STRAIGHT chord:  median={pct(body_clear_chord,.5):.0f}u  p25={pct(body_clear_chord,.25):.0f}u")
        print(f"  blocking unit's clearance from DETOURED path :  median={pct(body_clear_poly,.5):.0f}u  p25={pct(body_clear_poly,.25):.0f}u")
        print(f"  => Riot bends the path so a body it passes ends up ~{pct(body_clear_poly,.5):.0f}u (median) from the walked line")

if __name__=="__main__":
    main()
