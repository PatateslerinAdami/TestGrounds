#!/usr/bin/env python3
"""Regenerate Oldion.inibin with proper animations and fixed references."""

import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
from install_custom_skin import read_inibin_entries, _rebuild_inibin, riot_hash

SION_INI = Path("/home/n7reny/PycharmProjects/dpsk/program 3/Client/DATA/Characters/Sion/Sion.inibin")
OLDION_INI = Path("/home/n7reny/PycharmProjects/dpsk/program 3/Client/DATA/Characters/Oldion/Oldion.inibin")

def create_oldion_inibin():
    data = SION_INI.read_bytes()
    parsed = read_inibin_entries(SION_INI)
    entries = parsed['entries']
    print(f"Read Sion.inibin: {len(entries)} entries")

    # Exact string replacements (match entire string value)
    replace_exact = {
        "SionQ": "OldionCrypticGaze",
        "SionW": "OldionDeathsCaress",
        "SionE": "OldionEnrage",
        "SionR": "OldionCannibalism",
        "SionBasicAttack": "OldionBasicAttack",
        "SionBasicAttack2": "OldionBasicAttack2",
        "SionBasicAttack3": "OldionBasicAttack3",
        "SionCritAttack": "OldionCritAttack",
        "SionBasicAttackTower": "OldionBasicAttackTower",
        "SionBasicAttackTower2": "OldionBasicAttackTower2",
        "SionBasicAttackPassive": "OldionBasicAttackPassive",
        "SionBasicAttackPassive2": "OldionBasicAttackPassive2",
        "SionPassive": "OldionFeelNoPain",
        "Sion": "Oldion",
        "SionQDamage": "",
        "SionEMissile": "",
        "SionQHitParticleMissile": "",
        "SionQHitParticleMissile2": "",
        "SionQIndicatorMissile2": "",
        "SionQIndicatorMissile": "",
        "SionQShapeCut": "",
        "SionVOModeChange": "",
    }

    # Replace broader string patterns (substring)
    replace_substring = {
        "Sion": "Oldion",
        "sion": "Oldion",
    }

    modifications = {}
    for key_hash, entry in entries.items():
        if entry['type'] != 12:
            continue
        val = entry['value']
        if not isinstance(val, str):
            continue

        new_val = val
        # Try exact match first (longest)
        sorted_exact = sorted(replace_exact.keys(), key=len, reverse=True)
        for old in sorted_exact:
            if val == old or val == old:
                new_val = replace_exact[old] if replace_exact[old] else ""
                break

        # Fallback: substring replace
        if new_val == val:
            new_val = val.replace("Sion", "Oldion").replace("sion", "Oldion")

        if new_val != val:
            modifications[key_hash] = new_val
            print(f"  Remap: '{val}' -> '{new_val}'")

    # Apply modifications
    for key_hash, new_val in modifications.items():
        entries[key_hash] = {'type': 12, 'value': new_val}

    # Add/update specific entries
    new_entries = {
        # Info icons
        riot_hash("Info*IconCircle"): {'type': 12, 'value': "Oldion_Circle.dds"},
        riot_hash("Info*IconSquare"): {'type': 12, 'value': "Oldion_Square.dds"},
        # MeshSkin base
        riot_hash("MeshSkin*ChampionSkinName"): {'type': 12, 'value': "Oldion"},
        riot_hash("MeshSkin*ChampionSkinID"): {'type': 3, 'value': 14},
        riot_hash("MeshSkin*SimpleSkin"): {'type': 12, 'value': "Oldion.skn"},
        riot_hash("MeshSkin*Skeleton"): {'type': 12, 'value': "Oldion.skl"},
        riot_hash("MeshSkin*Texture"): {'type': 12, 'value': "Oldion_TX_CM.dds"},
        riot_hash("MeshSkin*Animations"): {'type': 12, 'value': "OldionBase.blnd"},
        # MeshSkin1-4 (using 4.20 Sion's skin IDs)
        riot_hash("MeshSkin1*ChampionSkinName"): {'type': 12, 'value': "Oldion Skin01"},
        riot_hash("MeshSkin1*ChampionSkinID"): {'type': 3, 'value': 15},
        riot_hash("MeshSkin1*SimpleSkin"): {'type': 12, 'value': "Oldion_Skin01.skn"},
        riot_hash("MeshSkin1*Skeleton"): {'type': 12, 'value': "Oldion_Skin01.skl"},
        riot_hash("MeshSkin1*Texture"): {'type': 12, 'value': "Oldion_Skin01_TX_CM.dds"},
        riot_hash("MeshSkin1*Animations"): {'type': 12, 'value': "OldionBase.blnd"},
        riot_hash("MeshSkin2*ChampionSkinName"): {'type': 12, 'value': "Oldion Skin02"},
        riot_hash("MeshSkin2*ChampionSkinID"): {'type': 3, 'value': 16},
        riot_hash("MeshSkin2*SimpleSkin"): {'type': 12, 'value': "Oldion_Skin02.skn"},
        riot_hash("MeshSkin2*Skeleton"): {'type': 12, 'value': "Oldion_Skin02.skl"},
        riot_hash("MeshSkin2*Texture"): {'type': 12, 'value': "Oldion_Skin02_TX_CM.dds"},
        riot_hash("MeshSkin2*Animations"): {'type': 12, 'value': "OldionBase.blnd"},
        riot_hash("MeshSkin3*ChampionSkinName"): {'type': 12, 'value': "Oldion Skin03"},
        riot_hash("MeshSkin3*ChampionSkinID"): {'type': 3, 'value': 17},
        riot_hash("MeshSkin3*SimpleSkin"): {'type': 12, 'value': "Oldion_Skin03.skn"},
        riot_hash("MeshSkin3*Skeleton"): {'type': 12, 'value': "Oldion_Skin03.skl"},
        riot_hash("MeshSkin3*Texture"): {'type': 12, 'value': "Oldion_Skin03_TX_CM.dds"},
        riot_hash("MeshSkin3*Animations"): {'type': 12, 'value': "OldionBase.blnd"},
        riot_hash("MeshSkin4*ChampionSkinName"): {'type': 12, 'value': "Oldion Skin04"},
        riot_hash("MeshSkin4*ChampionSkinID"): {'type': 3, 'value': 18},
        riot_hash("MeshSkin4*SimpleSkin"): {'type': 12, 'value': "Oldion_Skin04.skn"},
        riot_hash("MeshSkin4*Skeleton"): {'type': 12, 'value': "Oldion_Skin04.skl"},
        riot_hash("MeshSkin4*Texture"): {'type': 12, 'value': "Oldion_Skin04_TX_CM.dds"},
        riot_hash("MeshSkin4*Animations"): {'type': 12, 'value': "OldionBase.blnd"},
        # Voice override
        riot_hash("Data*CharAudioNameOverride"): {'type': 12, 'value': "Oldion"},
    }

    for key_hash, entry_data in new_entries.items():
        entries[key_hash] = entry_data

    # Rebuild and write
    _rebuild_inibin(data, entries, OLDION_INI)
    size = OLDION_INI.stat().st_size
    print(f"Written Oldion.inibin ({size} bytes)")

    # Verify no remaining Sion refs in string entries
    verify = read_inibin_entries(OLDION_INI)
    remaining = 0
    for h, e in verify['entries'].items():
        if e['type'] == 12 and isinstance(e['value'], str) and 'Sion' in e['value']:
            if 'ContentFormatVersion' not in e['value']:
                print(f"  REMAINING Sion: hash={h:#010x} val='{e['value']}'")
                remaining += 1
    if remaining == 0:
        print("No remaining Sion references!")
    return True

if __name__ == "__main__":
    create_oldion_inibin()
