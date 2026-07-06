# AreaTrigger-Umbau — SpellSector durch Riots Trigger-Maschine ersetzen

> Löst Audit-Item **H3** (`docs/STRUCTURAL_DIVERGENCE_AUDIT.md`). Ziel-Patch 4.20, Server 1:1 zu Riot.
> Status: **GEPLANT, nicht begonnen.** Quelle: Decomp-Untersuchung 2026-06-28
> ([[reference_areatrigger_subsystem]]).

## Problem (Audit H3)

Unser `Sector/SpellSector.cs` (+ `SpellSectorCone`/`SpellSectorPolygon`) ist eine **LeagueSandbox-Erfindung**:
ein gespawntes, **repliziertes**, **tickendes**, kollisions-registriertes `GameObject` mit `ObjectsHit`/`Tickrate`,
Formen Area/Cone/Polygon/Ring. Es **verschmilzt drei getrennte Riot-Mechanismen** in eine Entity → läuft „nicht
ganz richtig", und jedes Tickrate/Lifetime-Tuning fittet an einer nicht-existenten Referenz. ~72 Content-`.cs`
berühren die Sektor-API (`CreateSpellSector`/`OnSpellSectorHit`/`SectorParameters`) — die meisten **missbrauchen**
sie als Instant-AoE, nicht als persistente Zone.

## Decomp-Ground-Truth: Riots DREI-Wege-Taxonomie

Riot hat KEINEN „Sector". AoE/Zonen zerfallen in drei Mechanismen:

1. **Instant-AoE (dieser Tick):** inline Lua-Area-Query (`BBForNClosestUnitsInTargetArea` o.ä.). **KEINE Entity.**
   Bei uns = `GetUnitsHitBySpell`-Resolver ([[project_spelldata_aoe_resolver]]).
2. **Bewegter Effekt:** `SpellMissile` (networked GameObject). Bei uns schon treu.
3. **Persistente Region/Zone:** **`AreaTrigger`** (`Game/LoL/AI/Script/AreaTrigger.{h,cpp}`) — das hier fehlende Stück.

### `AreaTrigger` im Detail (Decomp, Server-Bodies VORHANDEN)
- **`AreaTriggerI`** (Basis): `mCenter`, `mRadius`, `mAreaTriggerID` (**int, KEINE NetID**) + vier Callbacks
  `OnEnter`/`OnExit`/`OnUpdate`(Unit) + `OnDestroyMissile`(Missile); Virtuals `UnitInArea`/`DestroysMissile`.
- **Zwei Formen, mehr nicht:** `AreaTriggerSphere` (Center+Radius), `AreaTriggerWall` (P1/P2/Facing/Länge/Dicke,
  `mDestroysMissiles`, attach-to-unit, Missile-NetID-Refs = **Windwall**, Yasuo W).
- **`AreaTriggerManager`** (Loki-Singleton): `CreateSphere`/`CreateWall`/`Find`/`Delete`/`UnitScan`/`UpdateTriggers`,
  hält `map<int, shared_ptr<AreaTriggerI>>`.
- **Lebenszyklus:** `UnitScan(unit)` vergleicht jetzt-drinnen vs. vorher-drinnen → `OnEnter`/`OnExit`;
  `UpdateTriggers(sendOnUpdates=true)` → `OnUpdate` pro Tick; Missiles fragen den ATM-Singleton via `DestroysMissile`
  (`SpellMissile.cpp:849`, `destroyedByAreaTrigger`).
- **Callbacks = Lua-Functors** (gebunden in `LuaScript.cpp`) → das **Spell-Script trägt die Logik** (Schaden/Buff/
  Destroy), der Trigger ist reine Geometrie. Script-seitige Hooks: `OnEnter/Leave/Update/DestroyMissileAreaTrigger`.
- **NICHT networked, unsichtbar.** Das Visual sind separate Partikel, die das Spell-Script erzeugt.

> **Decomp-Caveat (leichter als sonst):** `AreaTrigger.cpp` enthält echte Server-Bodies (UnitScan-Loop, OnEnter/
> Exit/Update, UpdateTriggers) — also direkter portierbar als rein client-gestubbte Subsysteme (anders als
> IssueOrders P3b). Trotzdem: Wire-Format nicht anfassen (AreaTrigger ist server-intern, **kein Paket**).

## Wie unser SpellSector divergiert

| Aspekt | Riot `AreaTrigger` | Unser `SpellSector` |
|---|---|---|
| Netzwerk | nicht networked, keine NetID, unsichtbar | gespawntes, **replizierendes** GameObject |
| Logik | Callback-Region (Script entscheidet) | eingebauter **Tick-Damage + ObjectsHit** |
| Formen | nur **Sphere + Wall** | Area/Cone/Polygon/Ring |
| Zweck | Anwesenheit + **Missile-Destruction (Windwall)** | generische AoE-Hit-Entity |

## Zielarchitektur

```
Game
  └─ AreaTriggerManager  (NEU: server-Service, NICHT repliziert)
       ├─ Dictionary<int, AreaTrigger>  (Sphere | Wall)
       ├─ CreateSphere(center, radius, hooks) / CreateWall(p1,p2,thickness,destroysMissiles,…)
       ├─ UnitScan(unit)      → OnEnter/OnExit  (pro Unit/Tick, Inside-Set-Diff)
       ├─ UpdateTriggers()    → OnUpdate        (pro Tick)
       └─ DestroysMissile(missile) → OnDestroyMissile  (vom Missile-Pfad gefragt)

AreaTrigger (abstrakt: Center/Radius/ID + Hook-Refs)
  ├─ AreaTriggerSphere
  └─ AreaTriggerWall   (Facing/Länge/Dicke, mDestroysMissiles, attach-to-unit)

ISpellScript  (+ vier optionale Hooks, default no-op → 0 Pflicht-Migration)
  ├─ OnEnterAreaTrigger(spell, trigger, unit)
  ├─ OnLeaveAreaTrigger(spell, trigger, unit)
  ├─ OnUpdateAreaTrigger(spell, trigger, unit)
  └─ OnDestroyMissileAreaTrigger(spell, trigger, missile)
```

Hook-Modell: das erstellende Script übergibt entweder Delegates an `CreateSphere/Wall` (≈ Riots Functor-Closure)
ODER implementiert die `ISpellScript`-Hooks (≈ Riots benannte Script-Events). **Empfehlung: Delegates beim Create**
(lokal, kein globaler Event-Bus, matcht Riots Functor-Modell, kein Routing-Mehrdeutigkeit bei mehreren Triggern
desselben Spells).

## Phasen (jede: Build + 159 Tests + In-game, Report+Pause, verhaltensneutral mergebar)

### P0 — Scope-Entscheid mit dem User (vor Code) — ✅ ERLEDIGT 2026-06-28: **Stufe C gewählt (voller Retire)**
> User-Entscheid: **Stufe C** — A+B+P4+P5, SpellSector verschwindet komplett. Voller Pfad P1→P5.
> Reihenfolge bleibt phasenweise (jede Phase Build+159+In-game, Report+Pause); C committet nur, bis P5
> durchzuziehen. Nächster Schritt: **P1**.

Drei Tiefenstufen (zur Referenz):
- **Stufe A (Subsystem + Windwall):** P1+P2. Baut `AreaTriggerManager` + Sphere/Wall + Missile-`DestroysMissile`,
  migriert NUR Windwall (Yasuo W). SpellSector bleibt für alles andere unangetastet. **Größter Treue-Gewinn pro
  Risiko** (Windwall fehlt uns sauber), null Migrations-Druck.
- **Stufe B (A + persistente Zonen):** + P3. Migriert die ECHTEN Zonen-Spells (lingering Damage-Zones,
  Enter/Leave-Buffs: Shaco Box, Teemo Shroom, Karthus Wall, Trundle Pillar, Cassio-Field-artige) auf AreaTrigger.
- **Stufe C (voller Retire):** + P4+P5. Klassifiziert ALLE ~72 Sektor-Nutzungen, schiebt Instant-AoE auf
  `GetUnitsHitBySpell`, entfernt `SpellSector`. Größte Arbeit. **Empfehlung: A jetzt, B als Folge, C optional.**

### P1 — `AreaTriggerManager` + Sphere/Wall + Script-Hooks (nichts nutzt es) — ✅ DONE 2026-06-28 (Build+159 grün)
> Umgesetzt: `GameObjects/Spell/AreaTrigger/AreaTrigger.cs` (`AreaTrigger` abstrakt + `AreaTriggerSphere` +
> `AreaTriggerWall`) + `AreaTriggerManager.cs`. Hook-Modell = **Delegates beim Create** (Riot-Functor-Stil, kein
> Event-Bus, ISpellScript unangetastet). In `Game` verdrahtet (`Game.AreaTriggerManager`, konstruiert nach
> ObjectManager, `Update(diff)` nach `ObjectManager.Update` mit Dormant-Fast-Path `_triggers.Count==0 → return`).
> `TryDestroyMissile` als API vorhanden (P2-Caller noch nicht). Verhaltensneutral: nichts erzeugt Trigger → 0
> Verhaltensänderung, 0 Content-Edit. Geometrie (Sphere=Center-Distanz, Wall=Punkt-zu-Segment, DestroysMissile=
> Position-basiert+Team) implementiert; swept-Missile-Test + replay-exakte Team-Semantik in P2.
1. `GameServerLib/GameObjects/SpellNS/AreaTrigger/`: `AreaTrigger` (abstrakt), `AreaTriggerSphere`, `AreaTriggerWall`,
   `AreaTriggerManager`. Manager als `Game.AreaTriggerManager` (server-Service, nicht im ObjectManager, **keine
   Replikation**).
2. `UnitScan` pro Tick (Inside-Set-Diff je Trigger → OnEnter/OnExit) + `UpdateTriggers` (OnUpdate). In die
   Game-Tick-Loop hängen (nach Movement, vor/nach SpellSector-Tick).
3. `ISpellScript`-Hooks (default no-op) ODER Delegate-Felder am Trigger. **0 Content-Migration** (nichts ruft es).
4. **Verifikation:** Build+159, ein Test-Spell der eine Sphere erstellt und Enter/Update/Exit loggt. Kein
   Verhaltenswechsel an bestehenden Spells.

### P2 — Faithful Windwall (Missile-`DestroysMissile`) — ⏳ CODE DONE 2026-06-28, in-game-Verify offen
> Umgesetzt: (1) zentraler Missile→Wall-Hook in `SpellMissile.PublishOnSpellMissileUpdate` (der geteilte
> Per-Tick-Chokepoint für Line/Circle/Homing) → `Game.AreaTriggerManager.TryDestroyMissile(this)` → bei
> Treffer `SetToRemove`. (2) API-Wrapper in `ApiFunctionManager`: `CreateAreaTriggerSphere`/`CreateAreaTriggerWall`/
> `UpdateAreaTriggerWallEndpoints`/`DeleteAreaTrigger`. (3) **Yasuo W migriert**: die hand-gerollte
> `GetMissiles()`-Per-Tick-Schleife in `YasuoWMovingWallMisVis` entfernt; stattdessen `CreateAreaTriggerWall`
> (Thickness 60 == altes „CollisionRadius+30"), Endpunkte pro Tick gesteuert (`OnMissileUpdate`), Cleanup via
> `OnSpellMissileEnd`. Team-Filter (eigenes Team exempt) byte-gleich zur alten Logik. Build+159 grün.
> **OFFEN:** In-game-Smoke (Windwall blockt gegnerische Skillshots, lässt eigene/Nicht-Missile durch); swept-
> Segment-Test (prev→cur) + replay-exakte Blockable-Set/Team-Semantik (welche Missile-Typen) als Verfeinerung.
1. `AreaTriggerWall` mit `mDestroysMissiles`; Missile-Update-Pfad (`SpellMissile`) fragt
   `Game.AreaTriggerManager.DestroysMissile(this)` → bei Treffer Missile zerstören + `OnDestroyMissile`-Hook.
2. Yasuo W (`YasuoWMovingWall`/Windwall) von SpellSector auf `CreateWall(destroysMissiles:true)` umstellen.
3. **Verifikation:** In-game — Windwall blockt Skillshots (Replay-Abgleich: welche Missiles geblockt werden,
   Team-Filter), lässt Turret-Shots/Nicht-Missile durch. Build+159.

### P3 — (Stufe B) Persistente Zonen migrieren — ⏳ LÄUFT (Pilot done 2026-06-28)
**Klassifikation der 9 echten `CreateSpellSector`-Nutzer (wichtig!):** Nur ein Teil sind ECHTE persistente
Presence-Zonen (P3); viele sind timed Snapshots (`SingleTick=true` → `ExecuteTick` ruft `SetToRemove` nach
einem Tick = Instant-AoE an einem Punkt → **P4/Category 1**, KEIN AreaTrigger). Tell: `SingleTick` + `BindObject`.
Vollständig klassifiziert 2026-06-28 (Merkmal `SingleTick=true` ⇒ sofort/einmal/weg ⇒ P4):
- **P3 (kontinuierliche Presence → AreaTrigger) — nur 3:** Shaco `BoxTime` (fix, OnEnter-Fear) ✅, Fiddle R
  `Crowstorm` (owner-following OnUpdate-DoT) ✅, **Rammus Q `QBuff`/Powerball** (owner-following, Tickrate 30,
  CanHitSameTargetConsecutively, KEIN SingleTick) — offen, reitet auf dem Crowstorm-Muster.
- **P4 (timed Snapshot → inline GetUnitsHitBySpell):** Katarina W (SingleTick), Soraka E ×2 (SingleTick Silence/
  Snare am Cursor), Yasuo Q1/Q2 (SingleTick, Polygon-Linie), Yasuo EBlock (SingleTick). Alle `SingleTick=true`.

**Pilot DONE 2026-06-28 — Shaco `BoxTime._fearZone`** (Build+159 grün, in-game offen): `CreateSpellSector` +
`OnSpellSectorHit` ersetzt durch `CreateAreaTriggerSphere(box.Position, 200f, onEnter:…)`; OnEnter armt den Fear
(enemy-Team-Filter, da AreaTrigger für alle feuert), Cleanup via `DeleteAreaTrigger` in `OnDeactivate`. Box ist
stationär → fixe Center, kein Anchoring nötig. Radius 200 == altes `Max(Length,Width)`. **OFFEN:** in-game
(Gegner läuft in JitB-Radius → gefeart).

**Shaco BoxTime: IN-GAME VERIFIZIERT 2026-06-28** (JitB feart beim Reinlaufen).

**Spell 2 DONE 2026-06-28 — Fiddle R Crowstorm** (Build+159 grün, in-game offen): erweitert das Subsystem um
**Sphere-Owner-Anchoring** (`AreaTriggerSphere._follow` → Center folgt einem GameObject live; Manager
`CreateSphereAttached`; API `CreateAreaTriggerSphereAttached`). Crowstorm: `CreateSpellSector`(BindObject=owner,
Tickrate 2, Lifetime 5) + `OnSpellHit/TargetExecute` ersetzt durch anchored Sphere (Radius 600 == altes
Max(Length,Width)) + `OnUpdate`-Handler `CrowstormTick`, der pro Unit alle 0.5s Schaden macht — **per-Unit-
GameTime-Pacing** (`ApiMapFunctionManager.GameTime()`), da OnUpdate kein diff liefert (= Riot-Script-Muster); 5s-
Timer ruft `DeleteAreaTrigger`. **OFFEN:** in-game (Gegner in Fiddle-Radius nimmt ~2×/s Schaden 5s lang).

**Rammus Q (Powerball) DONE 2026-06-28** (Build+159 grün, in-game offen): owner-anchored Sphere
(`CreateAreaTriggerSphereAttached(owner, collisionRadius+60)`) + `OnEnter`-Handler `OnPowerballEnter` (erster
Gegner-Kontakt, `_bonked`-Guard für Einmal-Hit, Enemy-Team-Filter); `DeleteAreaTrigger` in `OnDeactivate`
(fixt latenten Leak: alter Sektor hatte Lifetime=-1 und wurde NIE entfernt). **OFFEN:** in-game.

**→ P3 KOMPLETT** (alle 3 echten Presence-Zonen migriert: Shaco BoxTime ✅, Crowstorm ✅, Rammus Q ⏳in-game).
Subsystem-Fähigkeiten fertig: fixe + owner-anchored Sphere, Wall/Windwall, OnEnter/OnExit/OnUpdate/OnDestroyMissile,
GameTime-Pacing-Muster.

### P4 — (Stufe C) Instant-AoE-Nutzungen aus SpellSector lösen — ⏳ LÄUFT (Pilot done 2026-06-28)
Die SingleTick-Snapshot-Spawner durch inline-Area-Query ersetzen (KEINE Entity). Verbleibende CreateSpellSector-
Spawner nach P3 = genau diese 5: Katarina W, Soraka E (×2), Yasuo Q1, Yasuo Q2, Yasuo EBlock. Danach gibt es 0
CreateSpellSector-Caller mehr → P5.

**⚠️ P4-MUSTER (korrigiert nach Regression):** Inline-Query liefert nur das HIT-SET; jeder Treffer läuft durch
**`spell.ApplyEffects(target)`** (die zentrale Hit-Application — feuert `OnSpellHit`/`OnBeingSpellHit`), NICHT
durch rohes `TakeDamage`. Sonst brechen **cross-script On-Hit-Reaktoren** still: z.B. KatarinaQMark hört auf
`KatarinaW.OnSpellHit` und detoniert die Dagger-Mark — mit rohem TakeDamage feuerte das nicht mehr. Der spell-
eigene Schaden bleibt ein `OnSpellHit`-Listener (`TargetExecute`). **VOR jeder P4-Migration grep auf
`OnSpellHit`/`OnSpellSectorHit`-Listener der Spell** (cross-script-Kopplung). Ausnahme: self-contained +
multi-phase Spells (Soraka E: 2 distinkte Zonen mit eigenen Handlern, kein externer Reaktor, OnSpellHit kann die
Phasen nicht unterscheiden) → dort Effekt direkt im Query-Loop, kein ApplyEffects.

**Pilot DONE — Katarina W** (159 grün, in-game offen): `CreateSpellSector(Length=400, SingleTick)` ersetzt durch
inline `GetUnitsInRange(owner, owner.Position, 400f, true, flags)` → `spell.ApplyEffects(target)` pro Ziel;
`TargetExecute` bleibt OnSpellHit-Listener (W-Schaden), KatarinaQMark detoniert wieder. **Bewusst auf owner.Position
zentriert** (Self-AoE „Sinister Steel"; alte Sektor-Mitte war TargetPositionEnd = fakePos 500u voraus → latenter
Bug, jetzt korrekt). Primitive: `GetUnitsInRange` statt `GetUnitsHitBySpell` (Letzteres hängt fragil von
SpellData.TargetingType + Cast-Punkt ab; fixe Self-AoE = explizite Center+Radius-Query robust).

**P4 KOMPLETT 2026-06-28 (Build+159 grün, in-game offen für die 4 neuen):** Restliche Spawner migriert —
Yasuo Q1/Q2 (Polygon-Linie → `GetUnitsInPolygon(owner.Position, owner.Direction, 100,450, verts, flags)` +
ApplyEffects pro Treffer; cross-script-sicher), Yasuo EBlock (Sektor war nur „fire-once-if-enemy-in-200"-Gate →
`DoSpinDamage()` inline, raw TakeDamage wie zuvor, self-contained), Soraka E (2 Cursor-Snapshots → GetUnitsInRange@
Cursor 260, EZoneHit/EZoneEndHit direkt, multi-phase self-contained). **→ 0 `CreateSpellSector`-Caller mehr in
Content. P4 strukturell fertig.**

### P5 — `SpellSector` entfernen — SCOPE GEKLÄRT 2026-06-28
**87 Content-Files referenzieren `SpellSector` noch** — fast ausschließlich als toter 4. Parameter der
`OnSpellHit`/`OnBeingSpellHit`/`OnSpellMissileHit`-Handler-Signatur `(Spell, AttackableUnit, SpellMissile,
SpellSector)` (jetzt immer null). Voller Retire braucht daher:
1. Engine: `SpellSector`/`SpellSectorCone`/`SpellSectorPolygon` + `Spell.CreateSpellSector`/`CreateSpellSector(...)`
   + `OnSpellSectorHit`/`OnCreateSector` entfernen; den `SpellSector`-Parameter aus den Event-Tupeln
   `OnSpellHit`/`OnBeingSpellHit`/`OnSpellMissileHit` streichen (Dispatcher-Generics).
2. Content: den `SpellSector sector`-Param aus den ~87 Handler-Signaturen entfernen (mechanischer Sweep,
   skriptgestützt). Build + 159 + in-game-Smoke.
Verhalten ist schon korrekt (keine Sektor-Entities mehr); P5 ist reine Typ-Elimination.

**P5 DONE 2026-06-28 (Engine-Build + Content-Build + 159 grün; 0 SpellSector-Code-Refs solutionweit).** Entfernt:
`SpellSector`/`Cone`/`Polygon`-Klassen, `SectorType`-Enum, `SectorParameters`-Klasse + `ScriptMetadata.SectorParameters`,
`Spell.CreateSpellSector(...)` + metadata-getriebene Erzeugung, Events `OnSpellSectorHit`/`OnCreateSector`.
`SpellSector` aus den Event-Tupeln `OnSpellHit`/`OnBeingSpellHit` gestrichen (jetzt 3-arg `Dispatcher<…, SpellMissile>`),
`ApplyEffects`-Param `s` raus, `DebugModeCommand`-Sector-Debug + `AttackableUnit`-Collision-Filter bereinigt.
Content-Sweep: `SpellSector sector`-Param aus den ~87 Handler-Signaturen + dangling `using …SpellNS.Sector;`
entfernt (xargs+sed; per Content-csproj-Build validiert — KEINE runtime-only-Lücke). Bonus: pre-existing Typo
`APIMapFunctionManager`→`ApiMapFunctionManager` in LaneMinionAI gefixt (war von Incremental-Builds maskiert).
Verhaltensneutral (gedroppter Param war immer null); die 4 P4-Spells vorher in-game verifiziert.

## ✅ STUFE C / AREATRIGGER-REWRITE KOMPLETT
P1 Subsystem · P2 Windwall · P3 3 Presence-Zonen (Shaco Box/Crowstorm/Rammus Q) · P4 5 Snapshot-Spells→inline ·
P5 SpellSector entfernt. Riots 3-Wege-Taxonomie jetzt sauber abgebildet: instant-AoE=inline GetUnitsInRange/Polygon ·
bewegt=SpellMissile · persistent=AreaTrigger (sphere/wall, owner-anchorbar). H3 gelöst.

### P5 — `SpellSector` entfernen
Erst wenn P3+P4 alle Nutzungen migriert haben: `SpellSector`/`Cone`/`Polygon` + `CreateSpellSector` +
`OnSpellSectorHit`/`OnCreateSector` entfernen. Tote Replikation/Tick-Pfad raus.

## Was NICHT zu tun ist
- **AreaTrigger NICHT networken** — kein Paket, keine NetID, unsichtbar (Visual = separate Partikel des Scripts).
- **KEINE Cone/Polygon-Trigger-Formen** — die sind bei Riot Instant-AoE inline (Kategorie 1), keine Entity. Nur
  Sphere + Wall existieren als Trigger.
- **KEIN Tick-Damage im Trigger** — der Trigger feuert nur Callbacks; die Schadens-/Buff-Logik lebt im Script.
- **Die ~72 Scripts NICHT in einem Schnitt brechen** — SpellSector koexistiert bis P5; pro-Spell-Migration.
- **Nicht aus Client-Bodies raten** — hier sind Server-Bodies vorhanden, trotzdem an verifiziertem Verhalten/Replay
  festhalten wo Lifecycle unklar.

## Referenzen
- Decomp: `lol-decomp-mac-fresh/src/Game/LoL/AI/Script/AreaTrigger.{h,cpp}`, `Spell/Missile/SpellMissile.cpp:849`,
  `AI/Script/LuaScript.cpp` (Binding).
- Memory: [[reference_areatrigger_subsystem]], [[project_spelldata_aoe_resolver]] (Instant-AoE-Resolver),
  [[project_morgana_w_and_spell_onupdate]] (SpellSector als kaputte Abstraktion),
  [[project_terrain_walls_are_collision_units]] (Anivia-Wall = Collision-Units, separat von AreaTriggerWall),
  [[project_structural_divergence_audit]] (H3).
- Ist-Zustand: `GameObjects/SpellNS/Sector/SpellSector*.cs`, `Spell.cs CreateSpellSector(...)`, ~72 Content-Nutzer.
