#!/usr/bin/env python3
"""
Custom Skin Installer for League of Legends 4.20 Client
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

Usage:
    python install_custom_skin.py <skin_name> <path_to_zip>
    python install_custom_skin.py --uninstall <skin_name>
    python install_custom_skin.py --list

Examples:
    python install_custom_skin.py fiora_ivy "33273 - ivy valentine soul calibur fiora fixed.zip"
    python install_custom_skin.py --uninstall fiora_ivy
    python install_custom_skin.py --list
"""

import struct
import json
import os
import sys
import shutil
import zipfile
import tempfile
from pathlib import Path

# ─── Configuration ───────────────────────────────────────────────────────────

CLIENT_DIR = Path(__file__).resolve().parent / "Client" / "DATA"
CHARACTERS_DIR = CLIENT_DIR / "Characters"
PARTICLES_DIR = CLIENT_DIR / "Particles"
MANIFEST_PATH = CLIENT_DIR.parent / "custom_skin_manifest.json"


# ─── Riot Hash (HashStringNorm from HashFunctions.cs) ──────────────────────

def riot_hash(s: str) -> int:
    """HashStringNorm: h = char + 65599 * h, unsigned 32-bit"""
    h = 0
    for c in s.lower():
        h = (ord(c) + 65599 * h) & 0xFFFFFFFF
    return h


# ─── Inibin Binary Patch (read+append, no full rewrite) ────────────────────

def read_inibin_entries(path: Path) -> dict:
    """Read inibin and return {hash -> {type, value}}."""
    data = path.read_bytes()
    str_table_len = struct.unpack_from('<H', data, 1)[0]
    format_bits = struct.unpack_from('<H', data, 3)[0]
    
    entries = {}
    offset = 5
    for type_id in range(16):
        if not (format_bits & (1 << type_id)):
            continue
        count = struct.unpack_from('<H', data, offset)[0]
        offset += 2
        keys = [struct.unpack_from('<I', data, offset + i*4)[0] for i in range(count)]
        offset += count * 4
        
        for i in range(count):
            if type_id == 0:
                v = struct.unpack_from('<I', data, offset)[0]; offset += 4
            elif type_id == 1:
                v = struct.unpack_from('<f', data, offset)[0]; offset += 4
            elif type_id == 2:
                v = data[offset] / 10.0; offset += 1
            elif type_id == 3:
                v = struct.unpack_from('<h', data, offset)[0]; offset += 2
            elif type_id == 4:
                v = data[offset]; offset += 1
            elif type_id == 5:
                bcount = (count + 7) // 8
                bits_data = data[offset:offset+bcount]
                v = int((bits_data[i // 8] >> (i % 8)) & 1)
                if i == count - 1:
                    offset += bcount
            elif type_id == 6:
                offset += 3; v = None
            elif type_id == 7:
                offset += 12; v = None
            elif type_id == 8:
                offset += 2; v = None
            elif type_id == 9:
                offset += 8; v = None
            elif type_id == 10:
                offset += 4; v = None
            elif type_id == 11:
                offset += 16; v = None
            elif type_id == 12:
                str_off = struct.unpack_from('<h', data, offset)[0]; offset += 2
                abs_off = len(data) - str_table_len + str_off
                end = data.index(0, abs_off)
                v = data[abs_off:end].decode('ascii', errors='replace')
            elif type_id == 13:
                v = struct.unpack_from('<q', data, offset)[0]; offset += 8
            else:
                v = None
            entries[keys[i]] = {'type': type_id, 'value': v}
    
    return {
        'format_bits': format_bits,
        'str_table_len': str_table_len,
        'entries': entries,
        'raw_size': len(data),
    }


def patch_inibin_append(path: Path, entries_info: list):
    """
    Append new entries to an inibin file by modifying the raw binary.
    entries_info: list of [(hash, type_id, value), ...]
    
    Strategy: For each entry type, we find (or create) the segment and append.
    For string (type 12) entries, we add the string to the string table.
    For u32 (type 0) entries, we append to the u32 segment.
    """
    data = bytearray(path.read_bytes())
    
    str_table_len = struct.unpack_from('<H', data, 1)[0]
    format_bits = struct.unpack_from('<H', data, 3)[0]
    
    # Group new entries by type
    new_by_type = {}
    for h, t, v in entries_info:
        new_by_type.setdefault(t, []).append((h, v))
    
    # Read existing string table (at the end of the file)
    str_table = data[-str_table_len:]
    
    # Track which types need their bit in format_bits set
    format_changed = False
    for type_id in new_by_type:
        if not (format_bits & (1 << type_id)):
            format_bits |= (1 << type_id)
            format_changed = True
    
    if format_changed:
        struct.pack_into('<H', data, 3, format_bits)
    
    # Process each type
    # We need to find each existing segment or create a new one at the right position
    # The structure is: keys then values, per segment
    
    # For simplicity: just append new entries AFTER the last segment, before string table
    
    # Find the offset before the string table (where the last data segment ends)
    # We'll use a simpler approach: read all entries, add new ones, rebuild
    # But let's extract raw segment data and rebuild
    
    data = path.read_bytes()
    entries = read_inibin_entries(path)['entries']
    
    # Add new entries
    for h, t, v in entries_info:
        entries[h] = {'type': t, 'value': v}
    
    # Now rebuild the whole file from entries
    _rebuild_inibin(data, entries, path)


def _rebuild_inibin(original_data: bytes, entries: dict, path: Path):
    """
    Rebuild inibin from entries dict, preserving original formatting.
    """
    str_table_len = struct.unpack_from('<H', original_data, 1)[0]
    format_bits = struct.unpack_from('<H', original_data, 3)[0]
    
    # Determine which types are present
    by_type = {}
    for key, entry in entries.items():
        t = entry['type']
        by_type.setdefault(t, []).append((key, entry['value']))
    
    # Update format_bits to include all present types
    for t in by_type:
        format_bits |= (1 << t)
    
    # Read the original string table to preserve existing strings
    original_str_table = original_data[-str_table_len:]
    # Parse existing strings
    existing_strings = {}
    i = 0
    while i < len(original_str_table):
        end = original_str_table.find(b'\0', i)
        if end == -1:
            break
        s = original_str_table[i:end].decode('ascii', errors='replace')
        if s:
            existing_strings[s] = i
        i = end + 1
    
    # Build new string table: original strings + new type 12 values
    str_offsets = dict(existing_strings)  # copy
    new_str_data = bytearray(original_str_table)  # start with original
    
    if 12 in by_type:
        for key, val in by_type[12]:
            if isinstance(val, str) and val not in str_offsets:
                str_offsets[val] = len(new_str_data)
                new_str_data.extend(val.encode('ascii') + b'\0')
    
    new_str_table_len = len(new_str_data)
    
    # Build binary
    buf = bytearray()
    buf.extend(struct.pack('<B', 2))              # version
    buf.extend(struct.pack('<H', new_str_table_len))
    buf.extend(struct.pack('<H', format_bits))
    
    # Write segments in type order
    for type_id in sorted(by_type.keys()):
        items = by_type[type_id]
        count = len(items)
        buf.extend(struct.pack('<H', count))
        
        # Keys
        for key, _ in items:
            buf.extend(struct.pack('<I', key))
        
        # Values
        if type_id == 0:
            for _, val in items:
                buf.extend(struct.pack('<I', int(val)))
        elif type_id == 1:
            for _, val in items:
                buf.extend(struct.pack('<f', float(val)))
        elif type_id == 2:
            for _, val in items:
                buf.extend(struct.pack('<B', round(val * 10)))
        elif type_id == 3:
            for _, val in items:
                buf.extend(struct.pack('<h', int(val)))
        elif type_id == 4:
            for _, val in items:
                buf.extend(struct.pack('<B', int(val)))
        elif type_id == 5:
            # Write as bit array (single byte for up to 8 entries)
            bit_byte = 0
            for idx, (_, val) in enumerate(items):
                if val and isinstance(val, (list, tuple)):
                    bit_byte = val[0]
                elif val:
                    bit_byte |= (1 << idx)
            bcount = (count + 7) // 8
            for _ in range(bcount):
                buf.append(bit_byte if _ == 0 else 0)
        elif type_id == 6:
            for _, val in items:
                if isinstance(val, tuple) and len(val) >= 3:
                    buf.extend(bytes(val[:3]))
                else:
                    buf.extend(b'\0\0\0')
        elif type_id == 7:
            for _, val in items:
                if isinstance(val, tuple):
                    buf.extend(struct.pack('<fff', *val))
                else:
                    buf.extend(b'\0' * 12)
        elif type_id == 8:
            for _, val in items:
                if isinstance(val, tuple) and len(val) >= 2:
                    buf.extend(bytes([round(val[0]*10), round(val[1]*10)]))
                else:
                    buf.extend(b'\0\0')
        elif type_id == 9:
            for _, val in items:
                if isinstance(val, tuple):
                    buf.extend(struct.pack('<ff', *val))
                else:
                    buf.extend(b'\0' * 8)
        elif type_id == 10:
            for _, val in items:
                if isinstance(val, tuple):
                    buf.extend(bytes(round(v*10) for v in val))
                else:
                    buf.extend(b'\0' * 4)
        elif type_id == 11:
            for _, val in items:
                if isinstance(val, tuple):
                    buf.extend(struct.pack('<ffff', *val))
                else:
                    buf.extend(b'\0' * 16)
        elif type_id == 12:
            for _, val in items:
                off = str_offsets.get(val, 0)
                buf.extend(struct.pack('<h', off))
        elif type_id == 13:
            for _, val in items:
                buf.extend(struct.pack('<q', int(val)))
    
    # String table at end
    buf.extend(new_str_data)
    
    path.write_bytes(bytes(buf))


def patch_inibin_for_skin(inibin_path: Path, skin_id: int, skin_name: str,
                          skn_file: str, skl_file: str, tex_file: str):
    """Read inibin, add MeshSkin{skin_id} entries, write back."""
    section = f"MeshSkin{skin_id}"
    
    entries_to_add = [
        (riot_hash(f"{section}*ChampionSkinName"), 12, skin_name),
        (riot_hash(f"{section}*ChampionSkinID"), 0, skin_id),
        (riot_hash(f"{section}*SimpleSkin"), 12, skn_file),
        (riot_hash(f"{section}*Skeleton"), 12, skl_file),
        (riot_hash(f"{section}*Texture"), 12, tex_file),
    ]
    # Add Animations field (use champion's base blend file)
    anim_val = 'Fiora.blnd'
    existing = read_inibin_entries(inibin_path)['entries']
    anim_hash = riot_hash('MeshSkin*Animations')
    if anim_hash in existing and existing[anim_hash]['type'] == 12:
        anim_val = existing[anim_hash]['value']
    entries_to_add.append((riot_hash(f"{section}*Animations"), 12, anim_val))
    
    patch_inibin_append(inibin_path, entries_to_add)
    print(f"  ✓ Patched {inibin_path.name}: added {section} entries (SkinID {skin_id})")


# ─── Manifest Management ────────────────────────────────────────────────────

def load_manifest():
    if MANIFEST_PATH.exists():
        return json.loads(MANIFEST_PATH.read_text())
    return {"skins": {}}

def save_manifest(manifest):
    MANIFEST_PATH.write_text(json.dumps(manifest, indent=2))

def add_to_manifest(skin_name, champion, files_copied, inibin_backup=None):
    manifest = load_manifest()
    manifest.setdefault("skins", {})[skin_name] = {
        "champion": champion,
        "files": [str(f) for f in files_copied],
        "inibin_backup": str(inibin_backup) if inibin_backup else None,
    }
    save_manifest(manifest)


# ─── File Operations ────────────────────────────────────────────────────────

def copy_skinned(target_dir: Path, skin_prefix: str, src_skn: Path, src_skl: Path,
                 src_tex: Path, files_copied: list):
    """Copy .skn, .skl, and texture with proper naming."""
    dest_skn = target_dir / f"{skin_prefix}.skn"
    dest_skl = target_dir / f"{skin_prefix}.skl"
    dest_tex = target_dir / f"{skin_prefix}_TX_CM.dds"
    
    shutil.copy2(src_skn, dest_skn); files_copied.append(dest_skn)
    shutil.copy2(src_skl, dest_skl); files_copied.append(dest_skl)
    if src_tex and src_tex.exists():
        shutil.copy2(src_tex, dest_tex); files_copied.append(dest_tex)


# ─── Skin Installation ──────────────────────────────────────────────────────

def install_skin(skin_name: str, zip_path_str: str):
    zip_path = Path(zip_path_str)
    if not zip_path.exists():
        print(f"✗ Zip not found: {zip_path}")
        return False
    
    tmpdir = Path(tempfile.mkdtemp(prefix=f"skin_{skin_name}_"))
    
    try:
        # Extract — handle outer zip that may contain inner zip
        files_in_zip = []
        with zipfile.ZipFile(zip_path) as zf:
            for info in zf.infolist():
                if info.filename.endswith('/'):  # skip dirs
                    continue
                if info.filename.lower().endswith('.zip'):
                    # Inner zip — extract and re-extract
                    inner_path = tmpdir / "inner.zip"
                    with open(inner_path, 'wb') as f:
                        f.write(zf.read(info))
                    with zipfile.ZipFile(inner_path) as izf:
                        izf.extractall(tmpdir)
                    inner_path.unlink()
                else:
                    zf.extract(info, tmpdir)
                files_in_zip.append(info.filename)
        
        found = list(tmpdir.rglob("*"))
        print(f"Extracted {len(found)} files")
        
        # Find champion models
        all_skn = sorted(tmpdir.rglob("*.skn"), key=lambda p: p.stat().st_size, reverse=True)
        all_skl = list(tmpdir.rglob("*.skl"))
        all_dds = list(tmpdir.rglob("*.dds"))
        
        if not all_skn:
            print("✗ No .skn files found. Contents:")
            for f in found:
                if f.is_file():
                    print(f"  {f.relative_to(tmpdir)}")
            return False
        
        # Detect champion name
        champion = None
        for skn in all_skn:
            name = skn.stem
            for char_dir in CHARACTERS_DIR.iterdir():
                if char_dir.is_dir() and name.lower().startswith(char_dir.name.lower()):
                    champion = char_dir.name
                    break
            if champion:
                break
        if not champion:
            champion = "Fiora"  # fallback
        
        char_dir = CHARACTERS_DIR / champion
        if not char_dir.exists():
            print(f"✗ Champion directory not found: {char_dir}")
            return False
        
        inibin_path = char_dir / f"{champion}.inibin"
        if not inibin_path.exists():
            print(f"✗ Inibin not found: {inibin_path}")
            return False
        
        # Backup inibin
        inibin_backup = char_dir / f"{champion}.inibin.{skin_name}.bak"
        if not inibin_backup.exists():
            shutil.copy2(inibin_path, inibin_backup)
            print(f"  ✓ Inibin backed up")
        
        # Determine next skin ID by scanning MeshSkin section names
        entries = read_inibin_entries(inibin_path)['entries']
        max_section = 0
        for key, entry in entries.items():
            if entry['type'] == 12:
                for i in range(50):
                    if key == riot_hash(f"MeshSkin{i}*ChampionSkinName"):
                        max_section = max(max_section, i)
        new_id = max_section + 1
        print(f"  SkinID: {new_id}")
        
        # Skin filename prefix
        skin_prefix = skin_name  # already includes champion prefix like fiora_ivy
        
        # Find model files
        base_skn = next((f for f in all_skn if f.stem.lower() == champion.lower()), None)
        base_skl = next((f for f in all_skl if f.stem.lower() == champion.lower()), None)
        base_tex = next((f for f in all_dds if 'base' in f.stem.lower() or 'tx_cm' in f.stem.lower()), None)
        
        if not base_skn: base_skn = all_skn[0]
        if not base_skl: base_skl = all_skl[0] if all_skl else all_skn[0]
        if not base_tex: base_tex = next((f for f in all_dds if champion.lower() in f.stem.lower() and 'tx_cm' in f.stem.lower()), None)
        
        files_copied = []
        copy_skinned(char_dir, skin_prefix, base_skn, base_skl, base_tex, files_copied)
        print(f"  ✓ Copied: {skin_prefix}.skn/.skl/_TX_CM.dds")
        
        # Load screen
        ls_files = [f for f in tmpdir.rglob("*") if 'loadscreen' in f.name.lower() or 'load_screen' in f.name.lower()]
        for lf in ls_files:
            if lf.suffix.lower() == '.dds' and lf.stat().st_size > 1000:
                dst = char_dir / f"{champion}LoadScreen_{new_id}.dds"
                if not dst.exists():
                    shutil.copy2(lf, dst); files_copied.append(dst)
                    print(f"  ✓ Load screen: {dst.name}")
        
        # Info icons
        circle_src = next((f for f in all_dds if 'circle' in f.stem.lower()), None)
        square_src = next((f for f in all_dds if 'square' in f.stem.lower()), None)
        info_dir = char_dir / "Info"
        if circle_src:
            dst = info_dir / f"{champion}_Circle_{new_id}.dds"
            shutil.copy2(circle_src, dst); files_copied.append(dst)
            print(f"  ✓ Circle icon")
        if square_src:
            dst = info_dir / f"{champion}_Square_{new_id}.dds"
            shutil.copy2(square_src, dst); files_copied.append(dst)
            print(f"  ✓ Square icon")
        
        # Custom animations
        anim_dir = char_dir / "Animations"
        anm_files = list(tmpdir.rglob("*.anm"))
        for anm in anm_files:
            dst = anim_dir / anm.name
            if not dst.exists():
                shutil.copy2(anm, dst); files_copied.append(dst)
        if anm_files:
            print(f"  ✓ {len(anm_files)} animation(s)")
        
        # Particle files
        for ext in ['.dds', '.sco', '.troybin']:
            for pf in tmpdir.rglob(f"*{ext}"):
                if pf.name in [f.name for f in files_copied]:
                    continue
                pf_dst = PARTICLES_DIR / pf.name
                if not pf_dst.exists():
                    shutil.copy2(pf, pf_dst); files_copied.append(pf_dst)
        dds_particles = [f for f in files_copied if f.suffix == '.dds' and f.parent == PARTICLES_DIR]
        if dds_particles:
            print(f"  ✓ {len(dds_particles)} particle texture(s)")
        sco_particles = [f for f in files_copied if f.suffix == '.sco']
        if sco_particles:
            print(f"  ✓ {len(sco_particles)} .sco mesh(es)")
        
        # Patch inibin
        display_name = f"SC {skin_name.replace('_',' ').title()}"
        patch_inibin_for_skin(inibin_path, new_id, display_name,
                              f"{skin_prefix}.skn", f"{skin_prefix}.skl",
                              f"{skin_prefix}_TX_CM.dds")
        
        add_to_manifest(skin_name, champion, files_copied, inibin_backup)

        print("")
        print("✅ '{display_name}' (SkinID {new_id}) installed!".format(display_name=display_name, new_id=new_id))
        print("   {count} files added".format(count=len(files_copied)))
        return True

    finally:
        shutil.rmtree(tmpdir, ignore_errors=True)


def uninstall_skin(skin_name: str):
    manifest = load_manifest()
    if skin_name not in manifest.get("skins", {}):
        print(f"✗ Skin '{skin_name}' not found in manifest")
        return False
    
    info = manifest["skins"][skin_name]
    for f in map(Path, info.get("files", [])):
        if f.exists():
            f.unlink()
            print(f"  ✗ Removed {f.name}")
    
    if info.get("inibin_backup"):
        backup = Path(info["inibin_backup"])
        if backup.exists():
            inibin_path = CHARACTERS_DIR / info["champion"] / f"{info['champion']}.inibin"
            shutil.copy2(backup, inibin_path)
            backup.unlink()
            print(f"  ✓ Restored inibin")
    
    del manifest["skins"][skin_name]
    save_manifest(manifest)
    print()
    print("✅ '{skin_name}' uninstalled".format(skin_name=skin_name))
    return True


def list_skins():
    manifest = load_manifest()
    skins = manifest.get("skins", {})
    if not skins:
        print("No custom skins installed.")
        return
    print(f"Installed ({len(skins)}):")
    for name, info in skins.items():
        print(f"  {name} → {info['champion']} (SkinID from inibin) [{len(info['files'])} files]")


# ─── CLI ────────────────────────────────────────────────────────────────────

if __name__ == "__main__":
    if len(sys.argv) < 2:
        print(__doc__); sys.exit(1)
    
    cmd = sys.argv[1]
    
    if cmd == "--uninstall":
        uninstall_skin(sys.argv[2])
    elif cmd == "--list":
        list_skins()
    else:
        skin_name = cmd
        zip_path = sys.argv[2] if len(sys.argv) > 2 else None
        if not zip_path:
            root = Path(__file__).resolve().parent
            zips = list(root.glob(f"*{skin_name}*"))
            if zips:
                zip_path = str(zips[0])
                print(f"Found zip: {zips[0].name}")
            else:
                print(f"✗ No zip path provided and no match found")
                sys.exit(1)
        install_skin(skin_name, zip_path)
