# TestGrounds-master — Change Log

> **Created:** 2026-06-15
> **Forked from:** `TestGrounds-old` (Mor version of indev-perf, i didnt keep any changes from indev, just champions)
> **Status:** Build verified ✅ | Runtime unverified ⏳ (except where noted)

---

## 2026-06-16 (Session 3) — Soraka Full Kit + PacketHandlerManager NRE Fix + Engine Fix

### Soraka — Full Kit Implementation ✅ Player playtested (partial visual issues)

Implemented Soraka's 4.17-rework kit (ported from Chronobreak's CBProject-Converted):

| Spell | Class | Status |
|-------|-------|--------|
| **Passive** — Salvation | `CharScriptSoraka.cs` | ✅ Works — MS boost to low-HP allies, `soraka_base_passive_speed.troy` + `soraka_base_passive_cross.troy` + `soraka_base_passive_indicatior.troy` indicator arrow |
| **Q** — Starcall | `SorakaQ.cs` | 🟡 Works — damage, slow (30–50%), heal return, range-based travel delay (0.25–1s), vision bubble during drop. **Star appears on ground** (static, falls from sky not animated). Heal return instant with `global_ss_heal_02.troy` VFX |
| **W** — Astral Infusion | `SorakaW.cs` | ✅ Works — 10% HP cost, ally heal, `soraka_base_w_eff.troy` + `Soraka_base_W_Beam.troy` + `Global_Heal.troy` + `soraka_base_w_buf.troy` + `soraka_base_w_mis.troy` |
| **E** — Equinox | `SorakaE.cs` | 🟡 Works — silence sector ticks, damage, delayed root via timer + `GetUnitsInRange` + `AddBuff("Root")`. Hard to tell if ezreal bot is lagging or rooted, no root indicator|
| **R** — Wish | `SorakaR.cs` + `SorakaRCastTime.cs` (NEW) | ✅ Works — global heal (+50% on <40% HP), `soraka_base_r_cas.troy` on caster, `Soraka_Base_R_tar.troy` + `Global_Heal.troy` on each ally |
| **Basic Attacks** | `SorakaBasicAttacks.cs` | ✅ Stubs (silences `Could not find script` warnings) |

**Extra slot stubs created** (game data references — real logic is inline):
- `SorakaQMissile.cs` — stub
- `SorakaQReturnMissile.cs` — stub
- `SorakaWParticleMissile.cs` — stub
- `SorakaRCastTime.cs` — plays `soraka_base_r_cas.troy` + `Soraka_Base_R_tar.troy`

**Playtest results (2026-06-16):**
- **Overall**: Some lag/performance issues may affect visual feedback
- **Passive**: All elements present; slightly glitchy (possibly performance-related)
- **Q**: Star appears statically on ground ("welding" effect). Travel delay and vision bubble work. Heal return is instant with visual — almost matches original 4.20 behavior
- **W**: Works
- **E**: Works but lag makes root timing hard to verify — needs re-test
- **R**: Works

### PacketHandlerManager — NRE Crash Fix
`NotifyDisconnectFromNet` at line 332 crashed the server when a client disconnected before completing the ENet handshake (`peer.UserData` was null). Added null guard `(peer.UserData != null ? (int)peer.UserData : 0)` in all 3 locations.

### Particle Name Fixes (Soraka)
Corrected particle names to match actual `.troybin` files on disk:
- `Soraka_Base_R_Cas.troy` → `soraka_base_r_cas.troy`
- `Wish_tar.troy` → `Soraka_Base_R_tar.troy` (Wish_tar doesn't exist)
- `Soraka_Base_W_Cas.troy` → `soraka_base_w_eff.troy`
- `Soraka_Base_E_Root.troy` → `soraka_base_e_snare_tar.troy`
- `Soraka_Base_Q_Cast_Hand.troy` → removed (doesn't exist)

---

### Quinn — Full Kit Implementation ⏳ TCP verified, ⏳ client untested

Implemented Quinn's full 4.20 kit:

| Spell | Class | Status |
|-------|-------|--------|
| **Passive** | `CharScriptQuinn.cs` | Stub (deferred) |
| **Human Q** | `QuinnQ` — skillshot blind (ExtraSlot 3 missile) | ✅ TCP: 133 dmg |
| **Human W** | `QuinnW` — vision reveal (2100 range perception bubble) | ✅ TCP: works |
| **Human E** | `QuinnE` — dash to target + pushback + mark | ✅ TCP: 31 dmg |
| **R (Transform)** | `QuinnR` — instant Valor transform, melee range=125, MS +20/30/40% | ✅ TCP: model→QuinnValor, spells swap |
| **Valor Q** | `QuinnValorQ` — AoE slash 275 radius, 70-230 + 0.65bAD + 0.5AP | ✅ |
| **Valor W** | `QuinnW` — same as human (shared) | ✅ |
| **Valor E** | `QuinnValorE` — dash to target, 40-160 + 0.2bAD, speed 2000 | ✅ |
| **Valor R (Skystrike)** | Unified in `QuinnR` via `_isValor` toggle — 100/150/200 + 1.0AD, 400r → revert | ✅ TCP: 132 dmg + model→Quinn |
| **Valor W** | `QuinnW` — same as human (shared) | ❌ Not working in Valor form |

**⚠️ Playtest issues (2026-06-15):**
- **Laggy** — general performance low
- **Valor W not working** — `QuinnW` spell doesn't function in Valor form
- **R cooldown from start** — after R1 transforms, the R button immediately has a cooldown and cannot be pressed again to revert to Quinn
- **R1 should be 20s buff** — Valor form should last 20 seconds, then auto-revert. Currently no duration limit — stays in Valor indefinitely until manual revert works
- **AutoAttacks** - Valor does melee aatack but sends out bolt projectile

**Key fixes applied:**
- R1 instant transform (4.20 QuinnR has `CastType=0, IsToggleSpell=0, ChannelDuration=0` — NO channel)
- R2 revert uses `_isValor` field toggle (not a separate QuinnRFinale class — CSharpScriptEngine couldn't resolve runtime-loaded spell scripts)
- Valor melee range: `Stats.Range.BaseValue = 125f` on transform, reset to 525 on revert
- Valor MS bonus: `MoveSpeed.PercentBonus = 20/30/40%` per R rank
- Human Q damage + blind handled directly in `OnSpellPostCast` with `GetUnitsInRange` (ExtraSlot missile provides visual only)
- Valor Q particles: `FerosciousHowl_cas3.troy` + `HuntersCall_eff2.troy` per enemy
- Valor E dash speed: 1000→2000

**Debugged and fixed:**
- `QuinnValorForm` buff removed — no client `.luaobj` file, caused client crash
- R2 wouldn't fire because spell was on `STATE_COOLDOWN` after R1 — fixed in TCP test by calling `SetCooldown(0, false)` before cast

### TCP Test Harness ✅

Built `TcpTestServer` — embedded TCP command interface on port 5190:

```bash
echo "test Quinn R" | nc 127.0.0.1 5190
# → OK: Quinn model=QuinnValor lvl=7 cast=R(3) spells=[QuinnValorQ,W,QuinnValorE,QuinnR] dmg=132/658HP enemy=Ezreal
```

**Features:**
- Auto-levels champion to 6, learns all spells
- Teleports nearest enemy champion next to test champion
- Kills all turrets (99999 true damage) so teleported enemies don't die to fountain
- Records HP before/after cast → reports damage dealt
- Counts Particle objects before/after → particle tracking (limited — `AddParticle` doesn't call `ObjectManager.AddObject()`)
- Waits for `_game.IsRunning` before casting (forcedStart timer)
- Runs on TcpTestServer thread, no ENet dependency

**Also created:**
- `MoRTestClient/` — headless ENet packet sniffer (connects but RECEIVE events not received — ENet HostService issue)
- `test_champion.sh` — orchestration script for full test flow

### GameServer Fixes

**`Spell.cs` — cooldown guard fix:** Added `SetCooldown(0, false)` to TCP test harness because `Spell.Cast()` checks `State != STATE_READY` — spells on cooldown after first cast would silently return.

**`PacketServer.cs` — CONNECT NPE fix:** Added null guard on `enetEvent.Peer` in `DispatchEnetEvent`:
```csharp
if (enetEvent.Peer != null) { enetEvent.Peer.MTU = PEER_MTU; enetEvent.Data = 0; }
```

**`ApiFunctionManager.cs` — `SnapToWalkableTerrain` re-added:** Restored 29-line expanding ring search for ward placement. Method was removed in master but `IsWalkable()` still existed.

**`start.sh` fixes:**
- JSON comments stripped via `re.sub(r'//.*$', '', raw)` — game Config parser crashed on comments
- Port check changed from `ss -tlnp` (TCP) to `ss -uln` (UDP) — ENet uses UDP
- Port cleanup changed to `fuser -k 5119/udp`

**`PacketNotifier.cs` — Tutorial methods added:** `NotifyS2C_OpenTutorialPopup()` and `NotifyS2C_DisplayLocalizedTutorialChatText()` — missing from master.

### Soraka Fixes ⏳

**`Q.cs` + `W.cs` — `using System;` added:** Runtime Roslyn compilation in CSharpScriptEngine doesn't inherit `<ImplicitUsings>`. `Math.Min()` caused `CS0103` at runtime.

**`E.cs` — restored from old:** Master's version had broken listener registration (`ApiEventManager.OnSpellPostCast.AddListener` in OnActivate), wrong sector params, no detonation particle.

### Nidalee + Jayce — Form Change Implementations ❌ build only, runtime untested

| Champion | Spells | Pattern | Status |
|----------|--------|---------|--------|
| **Nidalee R** | `AspectOfTheCougar` — `ChangeModel` + `SetSpell` ×3 + `SetAutoAttackSpell` ×2 | Human↔Cougar toggle | ❌ Untested |
| **Jayce R** | `JayceStanceHtG` + `JayceStanceGtH` — model `JayceCannon`↔`Jayce` | Hammer↔Cannon, R name swaps | ❌ Untested |
| **Jayce QWE** | 6 spell classes (3 hammer, 3 cannon) with ExtraSlot missiles | Full kit stubs | ❌ Untested |
| **Quinn R** | Unified `QuinnR` with `_isValor` toggle (no separate finale class needed) | Human↔Valor, R = Skystrike | ✅ TCP verified |

### Riven R — Ported from older version of MoR ⏳
- `PassiveBuff.cs` replaced with proper level-scaling version (was flat 10 dmg stub)
- `R.cs`, `CharScriptRiven.cs`, `RivenBasicAttacks.cs`, `RivenFengShuiEngine.cs`, `RivenPassiveWatcher.cs` ported (vfx projectile issue, dmg is roughly correct, No bonus range for spells)

---

---

## 2026-06-15 — Initial Port from TestGrounds-old

### Vision System Port ✅ Build verified, ⏳ Runtime unverified

Restored the full ward/vision system that was partially removed in master.

**`ApiFunctionManager.cs` — `SnapToWalkableTerrain` re-added:**
- Re-added ~29-line expanding ring search (8 rays, 50u step, max 400u) after `IsWalkable`
- Only depends on `IsWalkable()` which still existed in master

**Ward scripts — snap line restored:**
- `Items/Actives/ItemGhostWard.cs` — `truecoords = SnapToWalkableTerrain(truecoords);`
- `Items/Actives/SpiritLantern.cs` — same
- `Items/Actives/TrinketTotemLvl1.cs` — same
- `Items/Actives/TrinketTotemLvl3.cs` — same
- `Items/Actives/TrinketTotemLvl3B.cs` — same

**Missing ward files ported from old:**
- `Items/Actives/SightWard.cs` — shop green ward (3-ward shared limit)
- `Items/Actives/VisionWard.cs` — pink ward (1-per-player limit)
- `Items/ItemCharScripts/SightWard/CharScriptSightWard.cs` — green ward lifecycle

**SightWard VFX fix:**
- `CharScriptSightWard.cs`: `Global_Trinket_MiniYellow` → `Ward_Green_Idle` (idle glow)
- `CharScriptSightWard.cs`: `Global_Trinket_Yellow_Death` → `Ward_Green_Death` (destruction)

All buff infrastructure (`SharedWardBuff.cs`, `VisionWardTracker.cs`, `TrinketTotemLvl1Self.cs`) was already present in master.

---

### Soraka — Full Kit Rewrite ⏳ Build verified, ❌ Runtime unverified

Master was missing most of Soraka's scripts (only `SorakaE.cs` existed, and it was broken).
Ported all missing files from old and rewrote Q/W/R to use ExtraSlot missile pattern.

#### Ported from old (unchanged):
- `Characters/Soraka/CharScriptSoraka.cs` — passive (Salvation: +70% MS toward low-HP allies)
- `Characters/Soraka/SorakaBasicAttacks.cs` — basic attack stubs
- `Characters/Soraka/Buffs/SorakaEPacify.cs` — silence buff wrapper
- `Characters/Soraka/Buffs/SorakaESnare.cs` — snare buff with root particle

#### Q (Starcall) — Rewritten ⏳
**Root cause:** Old approach used raw `AddParticle` without telling the client a missile was fired. Client played cast animation only.

**Fix:** ExtraSlot missile pattern (same as Karma Q):
```
SorakaQ.OnSpellPostCast → SpellCast(ExtraSlot 6 = "SorakaQMissile")
  → Client renders Soraka_Base_Q_Mis.troy (missile flying to ground)
  → SorakaQMissile.OnSpellHit → AoE damage (300r outer, 110r sweet spot ×1.5)
    → Slow buff
    → Per-champ-hit: SpellCast(ExtraSlot 4 = "SorakaQReturnMissile")
      → SorakaQReturnMissile.OnSpellHit → heal Soraka (25-65 + missingHP%)
```

**Files:**
- `Characters/Soraka/Q.cs` — 3 classes: `SorakaQ`, `SorakaQMissile`, `SorakaQReturnMissile`

#### W (Astral Infusion) — Rewritten ⏳
**Root cause:** Same as Q — no server→client missile sync.

**Fix:** ExtraSlot missile pattern:
```
SorakaW.OnSpellPostCast → HP cost (10% max HP) → SpellCast(ExtraSlot 5 = "SorakaWParticleMissile", target ally)
  → Client renders soraka_base_w_mis.troy (spark flying to ally)
  → SorakaWParticleMissile.OnSpellHit → heal ally (120-240 + 0.6AP)
```

**Files:**
- `Characters/Soraka/W.cs` — 2 classes: `SorakaW`, `SorakaWParticleMissile`

#### E (Equinox) — Restored from old ✅
**Root cause:** Master's `SorakaE.cs` had broken listener registration (`ApiEventManager.OnSpellPostCast.AddListener` in OnActivate), missing `OnDeactivate` cleanup, wrong sector params, and no detonation particle.

**Fix:** Replaced with old's working version which uses:
- `OnActivate` for owner/spell capture (no manual listener registration)
- `OnDeactivate` with `RemoveAllListenersForOwner`
- Silence sector: `Tickrate=4, CanHitSameTarget=true, CanHitSameTargetConsecutively=false`
- Root sector: `SingleTick=true` triggered by `RegisterTimer(1.5f, ...)` with `_rootPending` guard
- `AddPosPerceptionBubble` so client renders zone VFX
- Direct `Silence`/`Root` buffs instead of wrapper buffs
- Detonation particle: `Soraka_Base_E_Root.troy` per rooted target

**Files:**
- `Characters/Soraka/SorakaE.cs` — replaced with old version

#### R (Wish) — Enhanced ⏳
**Change:** Added `SpellCast(ExtraSlot 3 = "SorakaRCastTime")` so client renders `Starcall_hit.troy` after-effect. Moved caster VFX to `OnSpellCast`. Heal logic unchanged.

**Files:**
- `Characters/Soraka/R.cs` — added ExtraSlot cast + particles

---

### Tutorial.Chat() Port ✅ Build verified

Ported the `Tutorial.Chat()` / `Tutorial.Popup()` refactor from old.

**Files:**
- `GameServerLib/Messages/Tutorial.cs` — new file (copied from old)
- `GameServerLib/Packets/PacketHandlers/HandleStartGame.cs` — added `using LeagueSandbox.GameServer.Messages;` + 2 Tutorial calls
- `GameServerLib/Packets/PacketNotifier.cs` — added `NotifyS2C_OpenTutorialPopup()` and `NotifyS2C_DisplayLocalizedTutorialChatText()` methods

---

### Poppy — Full Kit Port ✅ Build verified, ⏳ Runtime unverified

Master had no Poppy scripts. Ported all 11 C# files from old.

**Files:**
- `Characters/Poppy/CharScriptPoppy.cs` — passive (Valiant Fighter) + W passive listener
- `Characters/Poppy/Q.cs` — Devastating Blow (COMBAT_ENCHANCER, next-auto bonus damage)
- `Characters/Poppy/W.cs` — Paragon of Demacia (10-stack armor/AD + active AS buff)
- `Characters/Poppy/E.cs` — Heroic Charge (dash + push + wall-slam stun)
- `Characters/Poppy/R.cs` — Diplomatic Immunity (non-target damage immunity)
- `Characters/Poppy/Buffs/PoppyDevastatingBlow.cs`
- `Characters/Poppy/Buffs/PoppyDiplomaticImmunityDmg.cs`
- `Characters/Poppy/Buffs/PoppyDITarget.cs`
- `Characters/Poppy/Buffs/PoppyParagonIcon.cs`
- `Characters/Poppy/Buffs/PoppyParagonOfDemacia.cs`
- `Characters/Poppy/Buffs/PoppyParagonSpeed.cs`

---

### Riven R — Port ⏳ Build verified, ❌ Runtime unverified

Master had Riven Q/W/E/buffs but was missing R, CharScript, and BasicAttacks.

**Files:**
- `Characters/Riven/R.cs`
- `Characters/Riven/CharScriptRiven.cs`
- `Characters/Riven/RivenBasicAttacks.cs`
- `Characters/Riven/Buffs/RivenFengShuiEngine.cs`
- `Characters/Riven/Buffs/RivenPassiveWatcher.cs`

---

### API Differences (old → master)

| Method | Old | Master | Action |
|--------|-----|--------|--------|
| `SnapToWalkableTerrain(Vector2)` | Present | Removed | **Re-added** for vision system |
| `FXFlags.BindDirection` | Default in AddParticle* | Renamed to `SimulateWhileOffScreen` | Champion scripts already updated |
| `RegionType.Default` | Default | Changed to `RegionType.Circle` | Scripts already updated |
| `GetPath(..., useFastPath)` | Optional param | Removed | Scripts already updated |
| `NotifyS2C_OpenTutorialPopup` | Present | Missing | **Added** for Tutorial.Chat |
| `NotifyS2C_DisplayLocalizedTutorialChatText` | Present | Missing | **Added** for Tutorial.Chat |

---

## Verification Legend

| Icon | Meaning |
|------|---------|
| ✅ | Build verified (compiles) |
| ⏳ | Build verified, needs runtime test |
| ❌ | Not runtime tested — may need adjustments |
