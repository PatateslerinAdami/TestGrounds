#!/usr/bin/env python3
"""Dump the constant pool of every function in a Lua 5.1 precompiled chunk (.luaobj).

unluac (and luadec) can recover the control flow of stripped League bytecode but lose local
variable NAMES, so table-construction data that flowed through a temporary often decompiles to a
nil placeholder (`L0_1.GroupsChance = L1_2` where `L1_2` was never assigned). The literal numbers
and strings are still in the bytecode constant tables, though — this reads them straight out so
you can recover values unluac dropped (e.g. Map1 NeutralMinionSpawn's GroupsChance = 100,
GroupsRespawnTime = 300/50/360/420, GroupDelaySpawnTime = 15/25/50/800).

Lua 5.1, little-endian, int=4, size_t=4, instruction=4, lua_Number=8-byte double — the format
League 4.x ships.

Usage:
  luaconst.py <file.luaobj>                 # dump every function's constants
  luaconst.py <file.luaobj> <substr>        # only functions whose constant strings contain <substr>
"""
import struct
import sys

if len(sys.argv) < 2:
    print(__doc__)
    sys.exit(2)

data = open(sys.argv[1], "rb").read()
needle = sys.argv[2] if len(sys.argv) > 2 else None
if data[:4] != b"\x1bLua" or data[4] != 0x51:
    sys.exit("not a Lua 5.1 precompiled chunk")
p = [12]  # skip the 12-byte header


def u8():
    v = data[p[0]]; p[0] += 1; return v


def u32():
    v = struct.unpack_from("<I", data, p[0])[0]; p[0] += 4; return v


def f64():
    v = struct.unpack_from("<d", data, p[0])[0]; p[0] += 8; return v


def s():
    n = u32()
    if n == 0:
        return None
    b = data[p[0]:p[0] + n - 1]; p[0] += n
    return b.decode("latin1")


funcs = []


def proto(depth):
    s()                          # source name
    u32(); u32()                 # line defined / last line defined
    u8(); u8(); u8(); u8()       # nups, numparams, is_vararg, maxstacksize
    n = u32(); p[0] += n * 4     # code
    kn = u32(); consts = []
    for _ in range(kn):          # constants
        t = u8()
        if t == 0:
            consts.append(None)
        elif t == 1:
            consts.append(bool(u8()))
        elif t == 3:
            consts.append(f64())
        elif t == 4:
            consts.append(s())
        else:
            raise ValueError("bad constant type %d at offset %d" % (t, p[0]))
    funcs.append((depth, consts))
    pn = u32()
    for _ in range(pn):          # nested prototypes
        proto(depth + 1)
    li = u32(); p[0] += li * 4   # line info
    lv = u32()
    for _ in range(lv):          # locals: name + startpc + endpc
        s(); u32(); u32()
    uv = u32()
    for _ in range(uv):          # upvalue names
        s()


proto(0)

for i, (depth, consts) in enumerate(funcs):
    strs = [c for c in consts if isinstance(c, str)]
    if needle is not None and needle not in strs:
        continue
    nums = [int(c) if isinstance(c, float) and c == int(c) else c
            for c in consts if isinstance(c, (int, float)) and not isinstance(c, bool)]
    print("--- func #%d (depth %d) ---" % (i, depth))
    print("  NUMS:", nums)
    print("  STRS:", strs)
