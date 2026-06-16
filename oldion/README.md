# Champion Mods for 4.20

> **Tools and guides for importing legacy (season 1) League champions into the 4.20 game client.**
>
> Originally built to port **Oldion** (2009 pre-rework Sion) into a 4.20 emulator setup.

---

## Overview

The 4.20 League client uses three key file formats for champion models:

| Format | Extension | What it contains |
|--------|-----------|-----------------|
| **SKN** | `.skn` | Skin mesh — vertex data, UVs, bone weights (magic: `3322 1100`) |
| **SKL** | `.skl` | Skeleton — bone hierarchy & transforms (magic: `0x22FD4FC3` on 4.20) |
| **BLND** | `.blnd` | Animation pool — indexes `.anm` files and defines clips (magic: `r3d2blnd`) |
| **ANM** | `.anm` | Animation data — per-bone transform curves (magic: `r3d2anmd`) |

Legacy (season 1) clients used:
| Format | Magic | Notes |
|--------|-------|-------|
| **SKN** | `3322 1100` | **Identical format** to 4.20! Versions 0-4 all compatible. |
| **SKL** | `r3d2sklt` | **Incompatible.** Must be converted to `0x22FD4FC3`. |
| **ANM** | `r3d2anmd` | **Same format** as 4.20. Old V3 files work on 4.20. |
| **BLND** | `r3d2blnd` | **Doesn't exist in legacy.** Must be generated from `.anm` files. |

---

## Tool Reference

### `tools/convert_skl.py` — Skeleton Converter

Converts legacy `r3d2sklt` skeleton files to 4.20 `0x22FD4FC3` format.

**Usage:**
```bash
python3 tools/convert_skl.py
```

**What it does:**
1. Reads bone count, bone names, parent indices, and 4x3 transform matrices from old `.skl`
2. Decomposes transforms into translation/scale/rotation (quaternion)
3. Computes local transforms from global matrices (parent-relative)
4. Writes new-format header with proper section offsets
5. Outputs a `.skl` file the 4.20 client can load

**Validation:** The tool auto-verifies the output format (magic `0x22FD4FC3`, correct bone count).

---

### `tools/create_oldion_inibin.py` — Champion Inibin Generator

Generates the main `Champion.inibin` file by copying a working champion's inibin and remapping all entries.

**How it works:**
1. Reads the source champion's `.inibin` (binary Riot inibin format)
2. Remaps all string entries: `"Sion"` → `"Oldion"`, `"SionQ"` → `"OldionCrypticGaze"`, etc.
3. Adds `MeshSkin1-4` sections for skin variants
4. Adds `Animations` entry pointing to the `.blnd` file
5. Outputs a clean `.inibin` with no remaining source champion references

**Uses:** `install_custom_skin.py` (inibin read/write library) — included in `tools/`.

---

### `tools/Blndrer/` — Animation Blend File Generator

A C#/.NET tool that creates `.blnd` files from champion animation files.

**Source:** [baaannnaaannn/Blndrer](https://github.com/baaannnaaannn/Blndrer)

**Usage:**
```bash
cd tools/Blndrer
dotnet build -c Release
dotnet run --project ./blndrer.csproj -- upgrade <DATA_DIRECTORY>
```

**What it does:**
1. Scans `DATA_DIRECTORY/Characters/` for champion folders
2. Reads each champion's `.ini` file for MeshSkin config
3. Reads `Animations.list` for animation slot definitions
4. Parses the actual `.anm` files to extract FPS and frame counts
5. Generates `Base.blnd`, `Skin1.blnd`, etc. with proper blend data

**Required file structure for input:**
```
DATA/Characters/<Name>/
├── <Name>.ini           # MeshSkin sections (INI format)
├── Animations.list      # Animation slot definitions
├── Animations/          # .anm files
│   ├── Name_Attack1.anm
│   └── ...
├── <Name>.skl           # Skeleton file
├── <Name>.skn           # Mesh file
```

**`Animations.list` format:**
```
AnimationSlot AnimationFile s:FPS
Idle1 Oldion_Idle1 s:30
Attack1 Oldion_Attack1 s:30
...
```

The `s:N` parameter is required (animation FPS, overridden by actual `.anm` file data).

---

## Step-by-Step: Adding a Champion to 4.20

### Phase 1: Set Up Directory Structure

Create the champion directory inside the 4.20 client:
```
Client/DATA/Characters/<Name>/
├── <Name>.inibin                    # Champion data (generate from tools/create_oldion_inibin.py)
├── Skins/Base/
│   ├── Base.inibin                  # Skin config file (copy from working champion, remap names)
│   ├── <Name>.skn                   # Model mesh (from legacy client — compatible!)
│   ├── <Name>.skl                   # Converted skeleton (from tools/convert_skl.py)
│   ├── <Name>_TX_CM.dds             # Texture (from legacy client)
│   ├── <Name>_Base.cac              # Collision mesh (copy from working champion)
│   ├── <Name>Base.blnd              # Generated animation pool (from tools/Blndrer)
│   └── Animations/
│       └── *.anm                    # Animation files (from legacy client — compatible!)
├── Spells/                          # Spell definitions (inibin files)
├── HUD/Icons2D/                     # Spell icons
├── Info/                            # Champion circle/square icons
├── Recommended/                     # Item builds
└── Localization/                    # Champion name/description string files
```

### Phase 2: Convert the Skeleton

```bash
python3 tools/convert_skl.py
```

This reads the old `r3d2sklt` skeleton and writes a new `0x22FD4FC3` skeleton. The bone count, names, hierarchy, and transforms are all preserved.

**Note:** The `.skn` mesh from the legacy client is already compatible (same `3322 1100` format). No conversion needed.

### Phase 3: Generate the Animation Pool

1. Create the `Animations.list` file listing all animation slots:
```
Idle1 Name_Idle1 s:30
Run Name_Run s:30
Attack1 Name_Attack1 s:30
...
```

2. Copy `.anm` files to `<Name>/Animations/`

3. Run Blndrer:
```bash
dotnet run --project tools/Blndrer/blndrer.csproj -- upgrade Client/DATA
```

4. Copy the generated `Base.blnd` to `Skins/Base/<Name>Base.blnd`

### Phase 4: Generate the Champion Inibin

1. Copy the champion `.inibin` from the 4.20 client
2. Run `tools/create_oldion_inibin.py` (configured for your champion)
3. Verify no remaining old-champion references

### Phase 5: Create the Base Skin Inibin

The 4.20 client needs `Skins/Base/Base.inibin` — a small binary file that maps:
- `SimpleSkin` → `Name.skn`
- `Skeleton` → `Name.skl`
- `Texture` → `Name_TX_CM.dds`
- `Animations` → `NameBase.blnd`
- `CACFileName` → `Name_Base.cac`

```bash
python3 tools/create_oldion_inibin.py  # Also generates Base.inibin
```

### Phase 6: Create Server-Side Scripts

Write C# scripts for the game server using the `ISpellScript`, `IBuffGameScript`, and `ICharScript` interfaces. Place them in the server's champion scripts directory.

### Phase 7: Test

1. Set `"champion": "<Name>"` in `GameInfo.json`
2. Launch the server
3. Launch the 4.20 client
4. Verify the model loads with correct textures and animations
5. Test abilities on the server side

---

## File Format Reference

### SKN (mesh) — `3322 1100`

| Offset | Size | Field | Notes |
|--------|------|-------|-------|
| 0 | 4 | FOURCC | Always `0x00112233` (magic) |
| 4 | 2 | Major version | v0, v1, v2, v3, v4 — all supported by 4.20 |
| 6 | 2 | Submesh count | |
| 8 | 2 | Object count | Flags/objects |
| 10 | 2 | Name len + name | Null-terminated model name |
| ... | | Submesh data | Indices + vertices with bone weights |

### SKL (skeleton, new format) — `0x22FD4FC3`

| Offset | Size | Field | Notes |
|--------|------|-------|-------|
| 0 | 4 | File size | Total file size in bytes |
| 4 | 4 | Magic | `0x22FD4FC3` |
| 8 | 8 | Reserved | Zero |
| 14 | 2 | Bone count | Number of skeleton joints |
| 16 | 4 | Influence count | Number of influence entries |
| 20 | 4 | Joints offset | Offset to joint data array |
| 24 | 4 | Joint indices offset | Offset to joint index table |
| 28 | 4 | Influences offset | Offset to influence array |
| 32+ | 4 | Name offsets | Bones names string table offsets |

Each joint entry (100 bytes):
- flags + id (4 bytes)
- parent index (2 bytes, -1 = root)
- flags (2 bytes)
- ELF hash of bone name (4 bytes)
- radius (4 bytes)
- local translation (12 bytes, 3 floats)
- local scale (12 bytes, 3 floats)
- local rotation (16 bytes, 4 floats quaternion)
- inverse global translation (12 bytes)
- inverse global scale (12 bytes)
- inverse global rotation (16 bytes)

### BLND (animation pool) — `r3d2blnd`

Auto-generated by Blndrer. Contains:
- Blend data (animation transition table)
- Blend tracks (weighted blending)
- Event data (particle/sound events per animation)
- Clip data (atomic clip definitions with FPS, frame ranges)
- Animation data references (pointers to `.anm` files)
- Skeleton reference path

---

## Known Issues

1. **Vertex weight remapping:** Legacy `.skn` files have bone weights referencing the old skeleton's bone indices. The converted skeleton preserves the same bone order, so weights remain valid. If the `skn` uses bone indices differently, manual remapping in Blender may be needed.

2. **Spell INIT level display:** The 4.20 client may show all spells at max level when first loading a custom champion. This is a client-side display issue resolved by properly sending spell level packets from the server.

3. **Animation mismatch:** If the `.blnd` references animation names that don't exist in the `Animations/` folder, the client will "t-pose" on those slots.

4. **Missing Base.inibin:** This is the most common "Essential game asset was not found" error cause. Every champion skin needs its own `Base.inibin`.
