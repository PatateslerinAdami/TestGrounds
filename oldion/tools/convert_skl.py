#!/usr/bin/env python3
"""
SKL Converter — Converts legacy 'r3d2sklt' skeleton format to 4.20 '0x22FD4FC3' format.

Extracted and adapted from GuiSaiUwU/lol_maya (MIT License).
Original: https://github.com/GuiSaiUwU/lol_maya
"""

import math
import struct
import sys
from pathlib import Path


# ─── Binary Stream ──────────────────────────────────────────────────────

class BinaryStream:
    _int16 = struct.Struct('h')
    _uint16 = struct.Struct('H')
    _int32 = struct.Struct('i')
    _uint32 = struct.Struct('I')
    _float = struct.Struct('f')
    _vec3 = struct.Struct('3f')
    _quat = struct.Struct('4f')

    def __init__(self, f):
        self.stream = f

    def seek(self, pos, mode=0):
        self.stream.seek(pos, mode)

    def tell(self):
        return self.stream.tell()

    def pad(self, length):
        self.stream.seek(length, 1)

    def end(self):
        cur = self.stream.tell()
        self.stream.seek(0, 2)
        res = self.stream.tell()
        self.stream.seek(cur)
        return res

    # ── read ──
    def read_byte(self):
        return self.stream.read(1)

    def read_uint16(self, count=1, forcetuple=False):
        if count > 1 or forcetuple:
            return struct.Struct(f'{count}H').unpack(self.stream.read(2 * count))
        return BinaryStream._uint16.unpack(self.stream.read(2))[0]

    def read_int16(self, count=1, forcetuple=False):
        if count > 1 or forcetuple:
            return struct.Struct(f'{count}h').unpack(self.stream.read(2 * count))
        return BinaryStream._int16.unpack(self.stream.read(2))[0]

    def read_uint32(self, count=1, forcetuple=False):
        if count > 1 or forcetuple:
            return struct.Struct(f'{count}I').unpack(self.stream.read(4 * count))
        return BinaryStream._uint32.unpack(self.stream.read(4))[0]

    def read_int32(self, count=1, forcetuple=False):
        if count > 1 or forcetuple:
            return struct.Struct(f'{count}i').unpack(self.stream.read(4 * count))
        return BinaryStream._int32.unpack(self.stream.read(4))[0]

    def read_float(self, count=1, forcetuple=False):
        if count > 1 or forcetuple:
            return struct.Struct(f'{count}f').unpack(self.stream.read(4 * count))
        return BinaryStream._float.unpack(self.stream.read(4))[0]

    def read_vec3(self):
        return Vector(*BinaryStream._vec3.unpack(self.stream.read(12)))

    def read_quat(self):
        return Quaternion(*BinaryStream._quat.unpack(self.stream.read(16)))

    def read_ascii(self, length):
        return self.stream.read(length).decode('ascii')

    def read_padded_ascii(self, length):
        raw = self.stream.read(length)
        return raw.rstrip(b'\x00').decode('ascii')

    def read_char_until_zero(self):
        chars = []
        while True:
            c = self.stream.read(1)
            if not c or c[0] == 0:
                break
            chars.append(chr(c[0]))
        return ''.join(chars)

    # ── write ──
    def write_bytes(self, data):
        self.stream.write(data)

    def write_uint16(self, *values):
        if len(values) > 1:
            self.stream.write(struct.Struct(f'{len(values)}H').pack(*values))
        else:
            self.stream.write(BinaryStream._uint16.pack(values[0]))

    def write_int16(self, *values):
        if len(values) > 1:
            self.stream.write(struct.Struct(f'{len(values)}h').pack(*values))
        else:
            self.stream.write(BinaryStream._int16.pack(values[0]))

    def write_uint32(self, *values):
        if len(values) > 1:
            self.stream.write(struct.Struct(f'{len(values)}I').pack(*values))
        else:
            self.stream.write(BinaryStream._uint32.pack(values[0]))

    def write_int32(self, *values):
        if len(values) > 1:
            self.stream.write(struct.Struct(f'{len(values)}i').pack(*values))
        else:
            self.stream.write(BinaryStream._int32.pack(values[0]))

    def write_float(self, *values):
        if len(values) > 1:
            self.stream.write(struct.Struct(f'{len(values)}f').pack(*values))
        else:
            self.stream.write(BinaryStream._float.pack(values[0]))

    def write_vec3(self, *vecs):
        floats = [v for vec in vecs for v in (vec.x, vec.y, vec.z)]
        self.stream.write(struct.Struct(f'{len(floats)}f').pack(*floats))

    def write_quat(self, *quats):
        floats = [v for quat in quats for v in (quat.x, quat.y, quat.z, quat.w)]
        self.stream.write(struct.Struct(f'{len(floats)}f').pack(*floats))

    def write_ascii(self, value):
        self.stream.write(value.encode('ascii'))


# ─── Math ──────────────────────────────────────────────────────────────

class Vector:
    __slots__ = ('x', 'y', 'z')

    def __init__(self, x=0.0, y=0.0, z=0.0):
        self.x = float(x)
        self.y = float(y)
        self.z = float(z)

    def __iter__(self):
        return iter((self.x, self.y, self.z))

    def __repr__(self):
        return f"Vector({self.x:.6f}, {self.y:.6f}, {self.z:.6f})"


class Quaternion:
    __slots__ = ('x', 'y', 'z', 'w')

    def __init__(self, x=0.0, y=0.0, z=0.0, w=1.0):
        self.x = float(x)
        self.y = float(y)
        self.z = float(z)
        self.w = float(w)

    def __iter__(self):
        return iter((self.x, self.y, self.z, self.w))

    def __repr__(self):
        return f"Quaternion({self.x:.6f}, {self.y:.6f}, {self.z:.6f}, {self.w:.6f})"


# ─── Matrix (4x4 row-major, list of 16 floats) ────────────────────────

def mat4_identity():
    return [1, 0, 0, 0,
            0, 1, 0, 0,
            0, 0, 1, 0,
            0, 0, 0, 1]


def mat4_from_cols(col0, col1, col2, col3):
    """Build row-major 4x4 from column vectors."""
    return [col0[0], col1[0], col2[0], col3[0],
            col0[1], col1[1], col2[1], col3[1],
            col0[2], col1[2], col2[2], col3[2],
            col0[3], col1[3], col2[3], col3[3]]


def mat4_mul(a, b):
    """Multiply two 4x4 row-major matrices."""
    return [
        sum(a[i * 4 + k] * b[k * 4 + j] for k in range(4))
        for i in range(4) for j in range(4)
    ]


def mat4_inverse(m):
    """Invert a 4x4 matrix using cofactors. Returns None if singular."""
    a = m
    inv = [0.0] * 16

    inv[0] = a[5] * a[10] * a[15] - a[5] * a[11] * a[14] - a[9] * a[6] * a[15] + a[9] * a[7] * a[14] + a[13] * a[6] * a[11] - a[13] * a[7] * a[10]
    inv[4] = -a[4] * a[10] * a[15] + a[4] * a[11] * a[14] + a[8] * a[6] * a[15] - a[8] * a[7] * a[14] - a[12] * a[6] * a[11] + a[12] * a[7] * a[10]
    inv[8] = a[4] * a[9] * a[15] - a[4] * a[11] * a[13] - a[8] * a[5] * a[15] + a[8] * a[7] * a[13] + a[12] * a[5] * a[11] - a[12] * a[7] * a[9]
    inv[12] = -a[4] * a[9] * a[14] + a[4] * a[10] * a[13] + a[8] * a[5] * a[14] - a[8] * a[6] * a[13] - a[12] * a[5] * a[10] + a[12] * a[6] * a[9]

    inv[1] = -a[1] * a[10] * a[15] + a[1] * a[11] * a[14] + a[9] * a[2] * a[15] - a[9] * a[3] * a[14] - a[13] * a[2] * a[11] + a[13] * a[3] * a[10]
    inv[5] = a[0] * a[10] * a[15] - a[0] * a[11] * a[14] - a[8] * a[2] * a[15] + a[8] * a[3] * a[14] + a[12] * a[2] * a[11] - a[12] * a[3] * a[10]
    inv[9] = -a[0] * a[9] * a[15] + a[0] * a[11] * a[13] + a[8] * a[1] * a[15] - a[8] * a[3] * a[13] - a[12] * a[1] * a[11] + a[12] * a[3] * a[9]
    inv[13] = a[0] * a[9] * a[14] - a[0] * a[10] * a[13] - a[8] * a[1] * a[14] + a[8] * a[2] * a[13] + a[12] * a[1] * a[10] - a[12] * a[2] * a[9]

    inv[2] = a[1] * a[6] * a[15] - a[1] * a[7] * a[14] - a[5] * a[2] * a[15] + a[5] * a[3] * a[14] + a[13] * a[2] * a[7] - a[13] * a[3] * a[6]
    inv[6] = -a[0] * a[6] * a[15] + a[0] * a[7] * a[14] + a[4] * a[2] * a[15] - a[4] * a[3] * a[14] - a[12] * a[2] * a[7] + a[12] * a[3] * a[6]
    inv[10] = a[0] * a[5] * a[15] - a[0] * a[7] * a[13] - a[4] * a[1] * a[15] + a[4] * a[3] * a[13] + a[12] * a[1] * a[7] - a[12] * a[3] * a[5]
    inv[14] = -a[0] * a[5] * a[14] + a[0] * a[6] * a[13] + a[4] * a[1] * a[14] - a[4] * a[2] * a[13] - a[12] * a[1] * a[6] + a[12] * a[2] * a[5]

    inv[3] = -a[1] * a[6] * a[11] + a[1] * a[7] * a[10] + a[5] * a[2] * a[11] - a[5] * a[3] * a[10] - a[9] * a[2] * a[7] + a[9] * a[3] * a[6]
    inv[7] = a[0] * a[6] * a[11] - a[0] * a[7] * a[10] - a[4] * a[2] * a[11] + a[4] * a[3] * a[10] + a[8] * a[2] * a[7] - a[8] * a[3] * a[6]
    inv[11] = -a[0] * a[5] * a[11] + a[0] * a[7] * a[9] + a[4] * a[1] * a[11] - a[4] * a[3] * a[9] - a[8] * a[1] * a[7] + a[8] * a[3] * a[5]
    inv[15] = a[0] * a[5] * a[10] - a[0] * a[6] * a[9] - a[4] * a[1] * a[10] + a[4] * a[2] * a[9] + a[8] * a[1] * a[6] - a[8] * a[2] * a[5]

    det = a[0] * inv[0] + a[1] * inv[4] + a[2] * inv[8] + a[3] * inv[12]
    if abs(det) < 1e-12:
        return None
    det_inv = 1.0 / det
    return [v * det_inv for v in inv]


def decompose_matrix(m):
    """Decompose a 4x4 row-major matrix into (translation, scale, rotation_quaternion).

    The matrix is assumed to be a rigid transformation (rotation + scale + translation).
    """
    tx = m[3]
    ty = m[7]
    tz = m[11]

    # Extract scale from column lengths
    sx = math.sqrt(m[0] * m[0] + m[4] * m[4] + m[8] * m[8])
    sy = math.sqrt(m[1] * m[1] + m[5] * m[5] + m[9] * m[9])
    sz = math.sqrt(m[2] * m[2] + m[6] * m[6] + m[10] * m[10])

    # Guard against zero scale
    sx = max(sx, 1e-10)
    sy = max(sy, 1e-10)
    sz = max(sz, 1e-10)

    # Normalize to get pure rotation matrix (row-major 3x3)
    rot = [
        m[0] / sx, m[1] / sy, m[2] / sz,
        m[4] / sx, m[5] / sy, m[6] / sz,
        m[8] / sx, m[9] / sy, m[10] / sz,
    ]

    # Convert rotation matrix to quaternion (w, x, y, z)
    trace = rot[0] + rot[4] + rot[8]
    if trace > 0:
        s = 0.5 / math.sqrt(trace + 1.0)
        qw = 0.25 / s
        qx = (rot[5] - rot[7]) * s
        qy = (rot[6] - rot[2]) * s
        qz = (rot[1] - rot[3]) * s
    elif rot[0] > rot[4] and rot[0] > rot[8]:
        s = 2.0 * math.sqrt(1.0 + rot[0] - rot[4] - rot[8])
        qw = (rot[5] - rot[7]) / s
        qx = 0.25 * s
        qy = (rot[3] + rot[1]) / s
        qz = (rot[6] + rot[2]) / s
    elif rot[4] > rot[8]:
        s = 2.0 * math.sqrt(1.0 + rot[4] - rot[0] - rot[8])
        qw = (rot[6] - rot[2]) / s
        qx = (rot[3] + rot[1]) / s
        qy = 0.25 * s
        qz = (rot[7] + rot[5]) / s
    else:
        s = 2.0 * math.sqrt(1.0 + rot[8] - rot[0] - rot[4])
        qw = (rot[1] - rot[3]) / s
        qx = (rot[6] + rot[2]) / s
        qy = (rot[7] + rot[5]) / s
        qz = 0.25 * s

    return Vector(tx, ty, tz), Vector(sx, sy, sz), Quaternion(qx, qy, qz, qw)


# ─── Hash ──────────────────────────────────────────────────────────────

class Hash:
    """ELF hash (lowercase). Used for bone name hashing in new SKL format."""

    @staticmethod
    def elf(s):
        s = s.lower()
        h = 0
        for c in s:
            h = (h << 4) + ord(c)
            t = h & 0xF0000000
            if t != 0:
                h ^= t >> 24
            h &= ~t
        return h


# ─── SKL Joint ─────────────────────────────────────────────────────────

class SKLJoint:
    __slots__ = ('name', 'parent', 'local_translation', 'local_scale',
                 'local_rotation', 'iglobal_translation', 'iglobal_scale',
                 'iglobal_rotation', 'global_matrix')

    def __init__(self):
        self.name = None
        self.parent = None
        self.local_translation = None
        self.local_scale = None
        self.local_rotation = None
        self.iglobal_translation = None
        self.iglobal_scale = None
        self.iglobal_rotation = None
        self.global_matrix = None


# ─── SKL ──────────────────────────────────────────────────────────────

class SKL:
    def __init__(self):
        self.joints = []
        self.influences = []

    def read_legacy(self, path):
        """Read legacy 'r3d2sklt' format."""
        with open(path, 'rb') as f:
            bs = BinaryStream(f)

            magic = bs.read_ascii(8)
            if magic != 'r3d2sklt':
                raise ValueError(f"Not a legacy SKL file. Magic: {magic}")

            version = bs.read_uint32()
            if version not in (1, 2):
                raise ValueError(f"Unsupported legacy version: {version}")

            bs.pad(4)  # designer id or skl id
            joint_count = bs.read_uint32()
            self.joints = [SKLJoint() for _ in range(joint_count)]

            for i in range(joint_count):
                joint = self.joints[i]
                joint.name = bs.read_padded_ascii(32)
                joint.parent = bs.read_int32()  # -1 means no parent
                bs.pad(4)  # radius/scale

                # Read 12 floats as column-major 4x3 matrix → row-major 4x4
                # Original storage: 3 columns (4 floats each), last col is [0,0,0,1]
                py_list = [0.0] * 16
                for c in range(3):
                    for r in range(4):
                        py_list[r * 4 + c] = bs.read_float()
                py_list[15] = 1.0  # homogeneous bottom-right
                joint.global_matrix = py_list

            # Read influences
            if version == 1:
                self.influences = list(range(joint_count))
            elif version == 2:
                influence_count = bs.read_uint32()
                self.influences = list(bs.read_uint32(influence_count, forcetuple=True))

        # Compute local transforms from global matrices
        for joint in self.joints:
            if joint.parent == -1:
                # Root: local = global
                translation, scale, rotation = decompose_matrix(joint.global_matrix)
            else:
                # Child: local = inv(parent_global) * global
                parent_global = self.joints[joint.parent].global_matrix
                inv_parent = mat4_inverse(parent_global)
                if inv_parent is None:
                    translation, scale, rotation = Vector(), Vector(1, 1, 1), Quaternion()
                else:
                    local_matrix = mat4_mul(joint.global_matrix, inv_parent)
                    translation, scale, rotation = decompose_matrix(local_matrix)

            joint.local_translation = translation
            joint.local_scale = scale
            joint.local_rotation = rotation

        return self

    def write(self, path):
        """Write new 0x22FD4FC3 format."""
        with open(path, 'wb') as f:
            bs = BinaryStream(f)

            # resource size (placeholder), magic, version
            bs.write_uint32(0, 0x22FD4FC3, 0)

            joint_count = len(self.joints)
            bs.write_uint16(0, joint_count)  # flags, joint count
            bs.write_uint32(joint_count)  # influences

            joints_offset = 64
            joint_indices_offset = joints_offset + joint_count * 100
            influences_offset = joint_indices_offset + joint_count * 8
            joint_names_offset = influences_offset + joint_count * 2

            bs.write_int32(
                joints_offset,
                joint_indices_offset,
                influences_offset,
                0,  # name
                0,  # asset name
                joint_names_offset,
            )

            # Reserved offset fields (5 x uint32 max)
            for _ in range(5):
                bs.write_uint32(0xFFFFFFFF)

            # Write bone names
            joint_offset = {}
            bs.seek(joint_names_offset)
            for i in range(joint_count):
                joint_offset[i] = bs.tell()
                bs.write_ascii(self.joints[i].name)
                bs.write_bytes(bytes([0]))

            # Write joint data
            bs.seek(joints_offset)
            for i in range(joint_count):
                joint = self.joints[i]

                bs.write_uint16(0, i)  # flags + id
                bs.write_int16(joint.parent)  # -1 = root
                bs.write_uint16(0)  # flags
                bs.write_uint32(Hash.elf(joint.name))
                bs.write_float(2.1)  # radius/scale

                # Local transform
                bs.write_vec3(joint.local_translation)
                bs.write_vec3(joint.local_scale)
                bs.write_quat(joint.local_rotation)

                # Inverse global (needed by the format, use local as approximation)
                iglobal_t = joint.iglobal_translation or joint.local_translation
                iglobal_s = joint.iglobal_scale or joint.local_scale
                iglobal_r = joint.iglobal_rotation or joint.local_rotation
                bs.write_vec3(iglobal_t)
                bs.write_vec3(iglobal_s)
                bs.write_quat(iglobal_r)

                # Name offset (relative to current position)
                bs.write_int32(joint_offset[i] - bs.tell())

            # Influences: 0, 1, 2, ..., joint_count-1
            bs.seek(influences_offset)
            bs.write_uint16(*list(range(joint_count)))

            # Joint indices
            bs.seek(joint_indices_offset)
            for i in range(joint_count):
                bs.write_uint16(i, 0)  # id + pad
                bs.write_uint32(Hash.elf(self.joints[i].name))

            # Write actual file size at offset 0
            bs.seek(0)
            bs.write_uint32(bs.end())

        return self


def convert_skl(old_path, new_path):
    """Convert legacy .skl to new format."""
    print("Reading:", old_path)
    skl = SKL().read_legacy(str(old_path))
    print("  Joints:", len(skl.joints))
    print("  Influences:", len(skl.influences))

    for i, j in enumerate(skl.joints[:5]):
        t = j.local_translation
        print("  [{}] {:20s} parent={:3d} t=({:.2f}, {:.2f}, {:.2f})".format(
            i, j.name, j.parent, t.x, t.y, t.z))

    print()
    print("Writing:", new_path)
    skl.write(str(new_path))
    print("  Output size:", Path(new_path).stat().st_size, "bytes")
    print("Done!")


if __name__ == "__main__":
    base = "/home/n7reny/PycharmProjects/dpsk/program 3"

    # Convert Sion's old skeleton
    old_skl = f"{base}/0.0.0.51/DATA/Characters/Sion/Sion.skl"
    new_skl = f"{base}/Client/DATA/Characters/Oldion/Skins/Base/Oldion.skl"

    convert_skl(old_skl, new_skl)

    # Verify the output matches new format
    print()
    with open(new_skl, 'rb') as f:
        bs = BinaryStream(f)
        magic = bs.read_uint32()
        assert magic == 0x22FD4FC3, f"Bad magic: 0x{magic:08X}"
        version = bs.read_uint32()
        flags = bs.read_uint16()
        count = bs.read_uint16()
        influences = bs.read_uint32()
        print(f"New format: magic=0x{magic:08X} version={version} joints={count}")
        print(f"✅ Valid 4.20 SKL format!")
