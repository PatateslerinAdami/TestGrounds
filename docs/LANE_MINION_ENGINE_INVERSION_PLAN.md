# Lane-Minion Engine Inversion Plan

**Goal:** Invert our lane-minion spawn architecture to match Riot's engine/script split — the
**engine drives the spawn loop per-barrack** and **calls back into the content script** for wave
composition, instead of the content script driving one global loop across all barracks.

Targeting patch **4.20**, benchmarked against the **4.17 mac decomp**
(`obj_Barracks` in `src/Game/LoL/WorldObject/Barracks/Barracks.cpp`) and the authoritative 4.20
Lua (`LEVELS/Map1/Scripts/LevelScript.lua`). Cross-checked against
`docs/LANE_MINION_DECOMP_AUDIT.md`.

---

## 0. Honest motivation — what this buys (and what it doesn't)

Read this first so the effort is scoped correctly.

The behavioral lane-minion gaps from the decomp audit are **already mostly closed** on the current
global-loop model (stat ramp S1✅, `AllInhibitorsAreDead` reset S3✅, drip-on-wave-length S6✅,
cannon-frequency ramp S4✅, deny-gold gate D1✅, level scaling SC2✅). And the per-barrack-specific
mechanics that a per-barrack model would "unlock" are **inert on SR**: the audit's 4.20 resolution
(OQ#2) found the inhibitor-destruction stat deltas (`HPInhibitor`/`DamageInhibitor`/`ExpInhibitor`/
`GoldInhibitor`) are **all 0** in every base minion table, and the local/shared-gold split (OQ#16)
is **uniformly 0** on SR.

So this inversion is **primarily an architectural / correctness-of-foundation change**, not a big
gameplay fix. What it actually buys:

1. **Faithfulness to Riot's structure** — eliminates the global-loop hacks (`_minionNumber`,
   `NextSpawnTime`, `_cannonMinionCount`, `_waveCount` shared across all 6 barracks) that the
   audit repeatedly had to paper over. Per-barrack state is how Riot models it.
2. **Per-barrack `waveCounts`** — fixes the cannon-frequency math being driven by one global
   counter instead of `waveCounts % CANNON_FREQ` *per barrack* (today they happen to stay in sync
   because all barracks spawn on the same global clock; they would desync the moment one barrack's
   spawning is disabled — see #4 below).
3. **Per-barrack spawn-disable** (`SetDisableMinionSpawn`/`DISABLE_MINION_SPAWN_*`) — currently
   unmodelled; only expressible once each barrack owns its own loop.
4. **A correct home for the few remaining gaps** — super-minion stop timing (S5), `MAX_MINIONS_EVER`
   end-game throttle (S8), real `Barrack_SpawnUnit.WaveCount` (S7) — all fall out of the per-barrack
   structure instead of needing more global-loop special-casing.
5. **Maintainability** — the content script (`LevelScript.cs`) shrinks to *content callbacks*
   (`GetInitSpawnInfo` / `GetMinionSpawnInfo` analogs), matching the Lua 1:1 and making future map
   scripts trivial to author.

**Hard invariant:** the verified outbound wire behavior must NOT change
(`docs/LANE_MINION_WIRE_VERIFICATION.md`: 2 packets/spawn, first wave ~90s, 30s wave interval,
~805ms drip, 3 melee + 3 caster, cannon on waves 3/6/9/12, super/double-super on inhib-down). This
is the acceptance test for every phase. The inversion is internal plumbing only.

---

## 1. The two architectures side by side

### Riot (target)

`obj_Barracks` is a real engine WorldObject, **one per barrack** (6 on SR: 3 lanes × 2 teams),
ticked by `GameObjectManager::Update` every frame (`GameObjectManager.cpp:386`).

- **`OnCreate`** (`Barracks.cpp:153-305`): calls Lua `GetInitSpawnInfo(lane, team)` once → reads
  `WaveSpawnRate` → `waveSpawnInterval`, `SingleMinionSpawnDelay` → `minionSpawnInterval`,
  `IsDestroyed`, and builds the per-barrack `minionTable` (`std::vector<MinionData>`) from
  `MinionInfoTable`.
- **`Update(dt)`** (`Barracks.cpp:339-531`): three independent timers —
  1. **Inhibitor-respawn** (`mInhibitorRespawnTime` countdown) → `ReactivateDampener()` → Lua
     `BarrackReactiveEvent(team, lane)`.
  2. **Super-minion** (`mSuperMinionSpawnTime` countdown, enabled when inhib dies) → Lua
     `DisableSuperMinions(team, lane)`.
  3. **Wave** (`waveTimer >= waveSpawnInterval`): `waveCounts++`, bump global `WaveTracker::GlobalWave`
     (once/frame), call Lua `GetMinionSpawnInfo(lane, waveCounts, 0, team, GlobalWave)` → per-type
     `NumPerWave`/`Armor`/`MagicResistance`/`HPBonus`/`DamageBonus`/`ExpGiven`/`GoldGiven`/
     `LocalGoldGiven` + `SpawnOrderMinionNames` → `SetMinionSpawnOrder`.
  - **Drip**: `curSpawnTimer > minionSpawnInterval` → walk `minionSpawnOrder`, decrement the first
    type with `NumToSpawnForWave > 0`, spawn exactly one (`break`).
- **Inhibitor death** (`DisableInhibitor`, `Barracks.cpp:937-942`): sets `mInhibitorRespawnTime`,
  `mInhibitorDestroyed=true`, `mSuperMinionSpawnTime = respawn − 2*waveInterval/1000`,
  `mSuperMinionsEnabled=true`.
- Actual minion creation is packet-driven (`DoBarrackSpawnUnit` → `obj_AI_Minion::Create`); our
  equivalent is `ApiMapFunctionManager.CreateLaneMinion`.

`obj_Barracks` member fields (`Barracks.h:28-101`): `waveTimer`, `waveCounts`, `curSpawnNum`,
`curSpawnTimer`, `waveSpawnInterval`, `minionSpawnInterval`, `minionTable`, `minionSpawnOrder`,
`mExperienceRadius`, `mGoldRadius`, `isDestroyed`, `BarrackLane`, `mInhibitorRespawnTime`,
`mInhibitorDestroyed`, `mSuperMinionSpawnTime`, `mSuperMinionsEnabled`, `mBarracksEnabled`.

### Ours (current)

- `LevelScript.CLASSIC.Update(diff)` drives **one global** clock: `NextSpawnTime`, `_minionNumber`
  (drip index), `_waveCount`, `_cannonMinionCount` — shared across all barracks
  (`LevelScript.cs:242-292, 367-369`).
- `SetUpLaneMinion()` iterates **all** `LevelScriptObjects.SpawnBarracks` each drip tick and spawns
  one minion per barrack (`LevelScript.cs:375-495`).
- Barracks are inert `MapObject` data (name+pos), **no class, no `Update`**
  (`LevelScriptObjects.SpawnBarracks` is `Dictionary<string, MapObject>`).
- Inhibitor respawn lives in `LevelScriptObjects.OnUpdate` (`:187-209`), not on a barrack.
- `ObjectManager.Update` **already ticks every GameObject's `Update(diff)`** each frame
  (`ObjectManager.cs:148`) — this is the exact equivalent of Riot's `GameObjectManager::Update`,
  so we get the per-barrack tick for **free** once a barrack is a GameObject.

### Mapping

| Riot | Ours (today) | Ours (after) |
|------|--------------|--------------|
| `obj_Barracks` (spawn building) | `MapObject` (inert) | **new `SpawnBarrack` plain server class** (NOT a GameObject — see decision) |
| `obj_BarracksDampener` (destructible) | `Inhibitor` | `Inhibitor` (unchanged) |
| `GameObjectManager::Update` → `obj_Barracks::Update` | `ObjectManager.Update` → (nothing) | **engine-side `BarracksSpawnManager.Update`** → `SpawnBarrack.Update` |
| Lua `GetInitSpawnInfo` | inline in `SetUpLaneMinion` | `IMapScript.GetInitSpawnInfo(...)` callback |
| Lua `GetMinionSpawnInfo` | `MinionWaveToSpawn` + ramp lookup | `IMapScript.GetMinionSpawnInfo(...)` callback |
| Lua `BarrackReactiveEvent` / `DisableSuperMinions` | `LevelScriptObjects.OnUpdate` respawn branch | barrack timers → script callbacks |
| `waveCounts` (per barrack) | `_waveCount` (global) | `SpawnBarrack.WaveCount` |
| `minionTable`/`minionSpawnOrder` | `MinionWaveTypes` lists | per-barrack, built from callback |

**Decision (REVISED after object-model investigation):** the spawn controller is a **plain
server-side class `SpawnBarrack`, NOT a `GameObject`**, owned and ticked by a new engine-side
`BarracksSpawnManager`. Rationale:

- **The codebase precedent is `Fountain`** (`GameServerLib/GameObjects/Fountain.cs`): a pure
  server-side object that is **not** a `GameObject` — no `NetId`, no visibility, no collision, no
  replication — with its own `Update(float)`. This is exactly the shape we need and the idiomatic
  way this codebase models server-only controllers.
- **Making it a `GameObject` is a wire-risk trap.** `ObjectManager.AddObject` is **not** conditional:
  for any non-`Champion` it immediately calls `SpawnObject` → `UpdateVisionSpawnAndSync` → `Sync`
  → spawn packet (`ObjectManager.cs:348-378, 321-342`; `GameObject.cs:309-365`). Suppressing that
  needs overriding `OnAdded` (skip `CollisionHandler.AddObject` + vision provider), `AutoProvidesVision`
  → false, `IsAffectedByFoW` → true, and `OnSpawn`/`Sync` no-ops — four overrides whose only purpose
  is to undo being a `GameObject`. A `GameObject` also always burns a `NetId`. Not worth it.
- A plain class **cannot** leak to clients (no NetId, never touches `ObjectManager`/visibility/
  collision at all), so the biggest risk in §4 (phantom replicated object) **disappears by
  construction**.

This still fully achieves the inversion: the **engine** (`BarracksSpawnManager`, in `GameServerLib`)
owns and drives the per-barrack loop and calls **content callbacks** on `IMapScript`. "Engine drives,
script decides" — the goal — does not require the controller to be a `GameObject`; it requires the
loop to live engine-side and the script to only answer content questions.

`SpawnBarrack` mirrors Riot's `obj_Barracks` (spawn) and stays separate from the destructible
`Inhibitor` (= `obj_BarracksDampener`), which is untouched. It links its lane `Inhibitor` to read
dead/respawning/super state.

**One caveat to verify in Phase 1:** today `Fountain.Update` is called from
`LevelScriptObjects.OnUpdate` (`:184`), i.e. **content-side**, via `CLASSIC.Update`. To genuinely
invert, the new `BarracksSpawnManager` must be ticked from the **engine** — `MapScriptHandler.Update`
(`:97-127`, in `GameServerLib`) — NOT from the content map script. Otherwise the loop is still
nominally content-driven. (Whether to also move `Fountain` ticking engine-side is out of scope.)

---

## 2. Target design

### 2.1 New classes: `SpawnBarrack` + `BarracksSpawnManager` (engine side)

**`SpawnBarrack`** — a plain server-side class (Fountain pattern, **not** a `GameObject`), one per
barrack. Suggested location `GameServerLib/GameObjects/SpawnBarrack.cs` (alongside `Fountain.cs`).
No `NetId`, no base constructor, no `ObjectManager` interaction — it only holds spawn state and calls
`CreateLaneMinion`.

**`BarracksSpawnManager`** — engine-side owner of all `SpawnBarrack`s (mirrors Riot's
`GameObjectManager` ticking every `obj_Barracks`). Holds `List<SpawnBarrack>`, exposes
`Update(float diff)` that ticks each, and is itself ticked from `MapScriptHandler.Update`
(engine-side — see §1 caveat). Lives in `GameServerLib/Handlers/` or as a field on `MapScriptHandler`.

`SpawnBarrack` state (mirrors `obj_Barracks`):
```
TeamId   Team;            Lane Lane;
Vector2  SpawnPosition;          // barrack CentralPoint
float    _waveTimer;             // ms accumulator, fires at WaveSpawnInterval
float    _dripTimer;             // ms accumulator, fires at MinionSpawnInterval
int      WaveCount;              // per-barrack wave counter (-> Barrack_SpawnUnit.WaveCount, S7)
int      WaveSpawnInterval;      // from GetInitSpawnInfo (30000)
int      MinionSpawnInterval;    // from GetInitSpawnInfo (800)
bool     IsDestroyed;            // barrack can stop spawning
bool     _spawnEnabled;          // SetDisableMinionSpawn gate
float    _disableSpawnUntil;     // DISABLE_MINION_SPAWN_* support
List<MinionSpawnSlot> _currentWave;   // resolved spawn order + per-type remaining count
Inhibitor _linkedInhibitor;      // lane inhibitor, for dead/respawn/super state
```

`Update(diff)`:
```
1. tick inhibitor-respawn / super-minion observation if we model it here (or keep in Inhibitor —
   see §3 Phase 4)
2. _waveTimer += diff; if (_spawnEnabled && _waveTimer >= WaveSpawnInterval) {
       _waveTimer -= WaveSpawnInterval; WaveCount++;
       _currentWave = MapScript.GetMinionSpawnInfo(this, WaveCount, ...);   // callback
   }
3. _dripTimer += diff; if (_dripTimer >= MinionSpawnInterval) {
       _dripTimer -= MinionSpawnInterval;
       var slot = next slot in _currentWave with Remaining > 0;
       if (slot != null) { slot.Remaining--; SpawnOne(slot.Type); }
   }
```
`SpawnOne(type)` builds the waypoints/turret-gates (the logic currently in `SetUpLaneMinion`
:394-450, moved here or into a shared helper) and calls `CreateLaneMinion(...)` exactly as today —
**same parameters, same packet**. This is what preserves wire-identity.

### 2.2 Content callbacks (script side, in `IMapScript` / `CLASSIC`)

Replace the script-driven loop with two pure callbacks that mirror the Lua, returning data only:

```csharp
// Lua GetInitSpawnInfo(lane, team) — called once when the SpawnBarrack is created.
MinionSpawnInit GetInitSpawnInfo(TeamId team, Lane lane);
//   -> { WaveSpawnInterval=30000, MinionSpawnInterval=800, IsDestroyed=false }

// Lua GetMinionSpawnInfo(lane, waveCount, _, team, globalWave) — called once per wave.
MinionWave GetMinionSpawnInfo(TeamId team, Lane lane, int waveCount, bool inhibDead, bool allInhibDead);
//   -> ordered list of (MinionSpawnType, count) + per-type stat ramp / gold / exp,
//      i.e. today's MinionWaveToSpawn + _minionRamps lookup, returned as data.
```

Everything content-side stays in the script: `_minionRamps` (90s `UpgradeMinionTimer`), cannon
frequency, super/double-super selection, give-radii. The script no longer touches timing.

The 90s `UpgradeMinminTimer` ramp tick stays in the script and is read on-demand inside
`GetMinionSpawnInfo` (matches the Lua: `UpgradeMinionTimer` is a separate repeating `InitTimer`
callback; `GetMinionSpawnInfo` just reads the current accumulated bonus). This also sidesteps the
update-ordering question (map script ticks before objects in `Game.Update`).

---

## 3. Phased implementation

Each phase ends with: **build green, 136 tests green, and a replay wire-diff
(`tools/wirecompare.py summary`) showing zero change vs the pre-inversion baseline.**

### Phase 1 — `SpawnBarrack` + `BarracksSpawnManager` scaffolding, behavior-frozen
- Add the plain `SpawnBarrack` class and `BarracksSpawnManager` (engine-side); tick the manager from
  `MapScriptHandler.Update`. Because `SpawnBarrack` is not a `GameObject`, there is **no** vision/
  collision/spawn-packet/NetId to disable — wire-invisibility is free.
- In `LevelScriptObjects.LoadSpawnBarracks` (or the manager's init), create one `SpawnBarrack` per
  `ObjBuildingBarracks` MapObject, link its lane `Inhibitor` via `InhibitorList[opposingTeam][lane]`.
- **Do NOT move the loop yet.** Phase 1 only proves the manager exists, ticks each barrack, and the
  old global loop still produces identical output (the new `Update` is a no-op stub).
- Test: replay diff unchanged.

### Phase 2 — Move the spawn loop into `SpawnBarrack`, kill the global loop
- Implement `Update` (the 2-timer wave+drip loop) and `SpawnOne` (port `SetUpLaneMinion`'s waypoint/
  turret-gate construction per-barrack).
- Add `GetInitSpawnInfo`/`GetMinionSpawnInfo` callbacks to `CLASSIC`; barrack calls them.
- Delete `CLASSIC.Update`'s spawn clock (`NextSpawnTime`, `_minionNumber`, `_waveCount`,
  `_cannonMinionCount`) and `SetUpLaneMinion`. Keep `_minionRamps` + ramp tick + announcements.
- **Critical wire-parity checks:** first wave at the same game-time (~90s — note current
  `NextSpawnTime` init and the S2 "first wave at 10s" open item; preserve *current* behavior in this
  phase, fix S2 separately so the diff stays clean), 30s cadence, 800ms drip, 3+3 composition,
  spawn order {Super, Melee, Cannon, Caster}, cannon on waves 3/6/9/12, super/double-super gating.
- This is the phase where per-barrack `WaveCount` replaces the global counter — verify cannon
  cadence per lane is identical (it should be, since all barracks start synchronized).

### Phase 3 — Per-barrack ramp & give-radii (faithful, behavior-identical on SR)
- Move the stat ramp from one global `_minionRamps` to per-barrack (or per-(team,lane)) accumulators
  so the structure matches Riot's per-barrack `MinionInfoTable`.
- **On SR this is behavior-identical** (all barracks ramp on the same 90s clock with the same
  deltas, and inhibitor deltas are 0 — OQ#2). The point is structural faithfulness + a correct home
  for inhibitor bonuses *if* a future non-SR map uses non-zero `*Inhibitor` deltas.
- Test: replay diff still zero.

### Phase 4 — Super-minion window fix (S5) — DONE 2026-06-23

**Corrected semantics (audit S5 was wrong).** Verified against the fresh mac decomp
(`obj_Barracks::DisableInhibitor`, `lol-decomp-mac-fresh/.../Barracks.cpp:931`):
```cpp
mInhibitorRespawnTime  = seconds;                              // 240s → inhibitor respawns
mSuperMinionSpawnTime  = seconds - 2*waveSpawnInterval/1000;   // 240 − 60 = 180s → DisableSuperMinions
mSuperMinionsEnabled   = true;
```
So Riot's super minions run for **180s** (respawn − 2×waveInterval), then stop while the inhibitor
is still down for the remaining ~60s; the inhibitor respawns at 240s. The audit's "supers spawn the
full down-window / stop at ReactivateDampener" was incorrect, and so was its proposed fix (run to
240s). Our old code stopped supers at the 225s `RespawningState` visual flip — i.e. 45s **too late**,
not too early.

**Implementation (deviates from the original "timer on the engine object" sketch — cleaner):** the
super window is derived from the **existing** inhibitor respawn countdown rather than a duplicate
timer — supers active while `DeadInhibitors[team][inhib] remaining > 2×waveInterval` (60s). This
avoids a second timer that could desync with the respawn, and keeps super-enablement a *composition*
decision (content's job, consistent with the inversion). Changes:
- `LevelScriptObjects.IsSuperMinionWindowActive(team, lane)` (+ `SUPER_MINION_STOP_BEFORE_RESPAWN_MS`).
- `SpawnBarrackMinion` gates supers on the window, not the inhibitor's visual `RegenerationState`.
- `MinionWaveToSpawn` double-super gated on the per-lane super window (`window ? (allInhibsDead ?
  Double : Super) : Regular`) so a lane whose window closed never spawns supers even if other lanes'
  inhibitors are down.
- The inhibitor's 240s respawn + 15s `RespawningState` visual warning are unchanged.

**Verification:** behavior only changes once an inhibitor is down, so the pre-inhibitor wire is
unchanged → the freeze baseline (no inhib deaths in ~163s) must still `spawncompare` PASS (regression
gate). The 180s window itself needs a late-game capture with an inhibitor down to observe directly.

### Phase 5 — Remaining low-priority faithful bits (optional, separate commits)
- S7: real `Barrack_SpawnUnit.WaveCount = WaveCount` (`PacketNotifier.cs:453`).
- S8: `MAX_MINIONS_EVER=180` / `2MeleeMinions` end-game throttle — **still blocked** on
  `GetTotalTeamMinionsSpawned` semantics (held in audit). Only do this if that's resolved.
- Per-barrack `SetDisableMinionSpawn` / `DISABLE_MINION_SPAWN_*` (currently unmodelled; now has a
  home, but no SR caller — low priority).

---

## 4. Risks & watch-items

1. **Wire drift is the whole risk.** Every phase must pass `tools/wirecompare.py` against a frozen
   pre-inversion replay. Capture that baseline replay BEFORE starting Phase 2.
2. **Update ordering.** `MapScriptHandler.Update` runs Collision → Pathing → `MapScript.Update`
   (content) (`:97-127`); tick `BarracksSpawnManager` here too. The 90s ramp tick stays in the
   content script and is read **on-demand** inside the `GetMinionSpawnInfo` callback (§2.2), so
   correctness does not depend on whether the manager ticks before or after `MapScript.Update`.
3. **Object lifetime / visibility — RESOLVED by design.** Because `SpawnBarrack` is a plain class
   (not a `GameObject`), it never touches `ObjectManager`/visibility/collision and **cannot** leak a
   phantom object to clients. This risk is eliminated by the revised §1 decision; the only spawn
   packets emitted are the real `CreateLaneMinion` ones (identical to today).
4. **Barrack ↔ Inhibitor linkage.** SR has 3 inhibitors/team but the spawn barracks and inhibitors
   are distinct MapObjects; confirm the lane/team key matches `InhibitorList[team][lane]` exactly
   (the `MapObject.GetOpposingTeamID`/`GetTeamID` parsing is already used and trusted).
5. **First-wave timing (S2).** The audit flags our current first wave possibly at 10s vs 90s. Keep
   *whatever the pre-inversion code does* through Phases 1-3 so the diff stays clean; fix S2 as an
   isolated change with its own verification. Do not bundle it into the inversion.
6. **Other maps.** Map10/Map11/Map16/Map31 share the same `LevelScript` pattern. The `IMapScript`
   callback interface must be added to all map scripts (or defaulted in `EmptyMapScript`). Scope the
   inversion to Map1 first, then port the others once the interface is proven.

---

## 5. Acceptance

- Build green; 136 tests green at every phase.
- `wirecompare.py summary` zero-diff vs frozen baseline through Phase 3.
- Phase 4's only intended diff = super minions persisting the full inhibitor-down window.
- `LevelScript.cs` no longer owns spawn *timing* — only content callbacks + ramp + announcements.
- Per-barrack `WaveCount` drives cannon cadence; global spawn counters deleted.
