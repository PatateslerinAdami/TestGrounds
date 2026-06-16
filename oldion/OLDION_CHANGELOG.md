# Oldion — Changelog

> **Oldion**: Pre-rework (2009) Sion, ported as a standalone champion into the 4.20 client.
> **Last Updated:** June 11, 2026 — Spell debugging session concluded

---

## Current Status

### ✅ Working

| Feature | Status | Notes |
|---------|--------|-------|
| **Model** | ✅ | 2009 Sion `.skn` (193KB, format v2) loads and renders |
| **Skeleton** | ✅ | 47 bones, converted `r3d2sklt` → `0x22FD4FC3` |
| **Textures** | ✅ | 2009 base texture (131KB) + load screens |
| **Animations** | ✅ | Walk, idle, death, dance, laugh, taunt all animate |
| **Champion name** | ✅ | Displays "Oldion" in-game |
| **Spell icons** | ✅ | Old 2009 Sion icons load on buttons |
| **Passive tooltip** | ✅ | "Feel No Pain" displays (fontconfig patch) |
| **Q tooltip** | 🟡 | "Cryptic Gaze" name shows — numbers/stats section missing |
| **Mini-map icon** | ✅ | Oldion circle/square |

### ❌ Not Working

| Issue | Description |
|-------|-------------|
| **Q/W/E/R spells** | **NO spell behavior whatsoever.** Pressing buttons produces no response — no cast, no cooldown, no damage, no buffs. Completely unaffected by any server-side code changes. |
| **W/E/R tooltips** | Show "spell not found" or broken text |
| **Passive icon** | No icon visible on HUD buff bar |
| **Passive block** | No confirmed damage blocking behavior |
| **All VFX** | No particle effects for any spell (`.troybin` rename applied, untested) |

---

## What We Tried — Complete History (June 10-11, 2026)

### Problem
Oldion spells (Q/W/E/R) produce zero behavior. The champion model loads, buttons are pressable, but spells never activate server-side. The passive `CharScript` might load (tooltip exists) but no spell scripts execute.

### Attempt 1: Rename files to TestGrounds convention
- Renamed `OldionCrypticGaze.cs` → `Q.cs`, `OldionDeathsCaress.cs` → `W.cs`, etc.
- Combined basic attack stubs into `OldionBasicAttacks.cs`
- **Result:** No change. Builds pass, spells don't activate.

### Attempt 2: Fix buff namespace (`Spells` → `Buffs`)
- Found buff classes (`OldionEnrageBuff`, `OldionCannibalismBuff`) were in `namespace Spells` but server resolves buffs via `CreateObjectStatic<IBuffGameScript>("Buffs", Name)`.
- Extracted buffs to `Buffs/` directory with `namespace Buffs`.
- **Result:** No change.

### Attempt 3: Fix `Stats.CurrentHealth` direct set → `TakeHeal` API
- `OldionCannibalismBuff` was setting `ally.Stats.CurrentHealth = ...` directly. `CurrentHealth` has `internal set` — blocked for dynamically compiled script assemblies.
- Changed to `ally.TakeHeal(_owner, healAmt, HealType.OutgoingHeal)`.
- **Result:** No change (spells never reached this code anyway).

### Attempt 4: Add game strings to `fontconfig_en_US.txt`
- Added `game_character_displayname_Oldion`, `game_character_passiveName_Oldion`, spell display names/tooltips.
- **Result:** Passive and Q tooltips now display. W/E/R still broken.

### Attempt 5: Rename particles `.troy` → `.troybin`
- All 68 Oldion particle files were `.troy` extension. The 4.20 client uses `.troybin` (verified with Ahri/Aatrox/Katarina).
- Renamed all files + updated 7 C# references.
- **Result:** Untested — spells never activate so particles can't be verified.

### Attempt 6: Remove `.luaobj` files to eliminate client-side spell conflicts
- The `SionW.luaobj` contains `BBSetSpell` → `"DeathsCaress"` (spell swap) + `BBIncreaseShield` (auto-shield).
- The `SionE.luaobj` contains `SpellToggleSlot` (toggle) + `BBIncPermanentStat`.
- The `SionR.luaobj` contains `BBSpellBuffAdd` (auto-buff).
- Hypothesis: Client-side Lua was conflicting with server C# spell logic.
- Removed all 66 `.luaobj` files.
- **Result:** **Game crashed on spell hover.** `.luaobj` files are REQUIRED for spell UI display.

### Attempt 7: Restore `.luaobj`, rename `.inibin` to match server spell names
- Renamed `SionQ.inibin` → `OldionCrypticGaze.inibin` etc.
- **Result:** No change. Reverted.

### Attempt 8: Align buff names with `.luaobj` expectations
- `.luaobj` files reference buff names: `"Death's Caress"`, `"Enrage"`, `"Cannibalism"`.
- Server C# was using: `"OldionDeathsCaressShield"`, `"OldionEnrageBuff"`, `"OldionCannibalismBuff"`.
- Renamed C# classes and `AddBuff` calls to match.
- Also removed duplicate VFX calls (`.luaobj` handles `AutoBuffActivateEffect`).
- **Result:** No change.

### Attempt 9: Rewrite spells to use `SetSpell` toggle (Aatrox W pattern)
- Created `OldionDeathsCaressDetonate` and `OldionEnrageCancel` classes.
- Used `owner.SetSpell("OldionDeathsCaressDetonate", 1, true)` for toggling.
- Added `ExtraSpell1`/`ExtraSpell2` to `Oldion.json`.
- **Result:** **Game crashed on spell hover.** Swapped spells had no client `.luaobj` data.

### Attempt 10: Hex-edit `SionW.luaobj` to disable `BBSetSpell`
- Changed `BBSetSpell` → `BBXetSpell` (corrupted function name, Lua can't find it).
- Changed `BBSetSlotSpellCooldownTime` similarly.
- **Result:** No change. Spells still don't activate.

### Attempt 11: Port Chronobreak Sion spell names directly
- Changed `Oldion.json` spell names to `CrypticGaze`, `DeathsCaress`, `Enrage`, `Cannibalism` (matching Chronobreak's old Sion).
- Renamed C# classes to match: `CrypticGaze`, `DeathsCaress`, `Enrage`, `Cannibalism`.
- Simplified spells to absolute minimum: single `AddBuff` call on `OnSpellPostCast`.
- **Result:** No change. Completely unaffected.

---

## Root Cause Analysis

After 11 attempts, **zero observable change** in spell behavior. The key observations:

1. **`dotnet build` succeeds** — all classes compile, namespaces match, spell names in JSON match class names.
2. **Server restart is confirmed** — lobby and game server both restarted.
3. **Changes to JSON spell names** (e.g., `OldionCrypticGaze` → `CrypticGaze`) had no visible effect — suggesting the server isn't dynamically recompiling scripts at runtime, OR the scripts are compiled but never executed for Oldion.
4. **Katarina works** with the same `ISpellScript` interface and `namespace Spells` pattern.
5. **Passive tooltip works** — fontconfig strings load correctly.

### Hypotheses (unconfirmed)

| # | Theory | Evidence |
|---|--------|----------|
| A | **CSharpScriptEngine runtime compilation fails silently** for Oldion scripts. The Roslyn compiler might reject them at runtime even though `dotnet build` passes. | No spell behavior despite correct code |
| B | **The dynamically compiled assembly is shadowed** by the pre-compiled `LeagueSandbox-Scripts.dll`. The `CSharpScriptEngine` looks for `"CSharpScriptEngine_Compiler"` assembly but the DLL loads first. | Changes to JSON spell names had no effect |
| C | **Champion model name mismatch** — client sends `"Sion"` spell names, server expects `"CrypticGaze"` etc. Cast packets never reach correct handler. | "Spell not found" tooltips |
| D | **`.luaobj` spell names mismatch** — client resolves spells via `.luaobj` internal names (`SionQ`, `SionW`), not server spell names. | Tooltips broken for W/E/R |

### Most Likely: Theory C or D
The client and server are using **different spell name systems**. The server JSON maps `Spell1: "CrypticGaze"` but the client `.luaobj`/`.inibin` files reference `SionQ`. The cast packet from client carries the client-side spell name, which the server can't match to any registered spell handler.

---

## Server Scripts (Current State)

```
TestGrounds-indev-perf/Content/LeagueSandbox-Scripts/Characters/Oldion/
├── Q.cs              # CrypticGaze — OnSpellHit → damage + stun
├── W.cs              # DeathsCaress — OnSpellPostCast → AddBuff shield
├── E.cs              # Enrage — OnSpellPostCast → toggle HasBuff/AddBuff
├── R.cs              # Cannibalism — OnSpellPostCast → AddBuff lifesteal
├── CharScriptOldion.cs  # Feel No Pain — OnTakeDamage block
├── OldionBasicAttacks.cs
└── Buffs/
    ├── DeathsCaressShield.cs   # Shield absorb + AOE explode
    ├── EnrageBuff.cs           # AD buff + HP cost + kills give maxHP
    ├── EnrageBuffMaxHP.cs      # +1 HP per stack
    └── CannibalismBuff.cs      # Lifesteal + AS

Oldion.json spell names: CrypticGaze, DeathsCaress, Enrage, Cannibalism
```

---

## Client Files

```
Client/DATA/Characters/Oldion/
├── Spells/           # 17 .inibin + 65 .luaobj + 25 .preload
├── Skins/Base/Particles/  # 68 .troybin particles (renamed from .troy)
├── HUD/Icons2D/     # Spell + passive icons
├── Oldion.inibin     # Champion data
├── Oldion.skn/.skl   # 2009 model + skeleton
└── _luaobj_backup/   # 95 original files backup
```

---

## Next Steps (if revisited)

1. **Test with a known-working champion's spell names** — temporarily set Oldion.json `Spell1: "KatarinaQ"` and see if Katarina's Q fires. This proves whether the spell resolution system works at all.
2. **Add debug logging** to `CSharpScriptEngine.Load()` to see if Oldion scripts are compiled at runtime.
3. **Check if the champion `Model` field** (`Oldion.json` → `"Oldion"`) matches the directory name and `.inibin` character name everywhere.
4. **Trace spell cast packets** — see what spell name the client sends when the Oldion Q button is pressed.
5. **Try using `SionQ`/`SionW`/`SionE`/`SionR` as spell names** in JSON + rename C# classes to match — fully align server names with client `.luaobj` names.
