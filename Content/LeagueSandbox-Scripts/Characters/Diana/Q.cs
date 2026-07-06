using System;
using System.Numerics;
using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.SpellNS.Missile;
using LeagueSandbox.GameServer.Scripting.CSharp;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace Spells;

// Diana Q — Crescent Strike ("DianaArc"). Replay-verified build, see
// docs/DIANA_ARC_THROW_PLAN.md §"P1 RESULTS". The visible/damaging projectile is the circle
// missile DianaArcThrow (CastType 4, CircleMissileAngularVelocity 3.5, MissileLifetime 1.0): it
// spawns at Diana on the rim of a radius-480 circle and orbits an offset center, tracing the
// counter-clockwise crescent. DianaArc is only the aim/trigger spell.
public class DianaArc : ISpellScript {

    private const float ArcRadius = 480f;   // = OT_AreaRadius; fixed orbit radius (→ constant speed)
    private const float MaxRange  = 830f;    // S4 TargeterConstrainedToRange (must be ≤ 2·ArcRadius)
    private const float MinRange  = 100f;    // keep a visible crescent for point-blank clicks

    // Cosmetic trail band edges. ALL arcs pass through BOTH Diana and the aim (centers on the
    // [Diana,aim] perpendicular bisector at √(R²−d²/4)) → the crescent is a lens, pointed at Diana
    // AND at the aim; the tuned JSON angularVels make each edge reach the aim at almost the same
    // instant as the bolt, so they taper to a point at the tip.
    //
    // Riot ships FIVE Outer/Inner variants with fixed radii + tuned angularVels (replay-measured),
    // and picks one PER CAST by distance: a closer cast uses a tighter Outer radius so the band
    // stays wide even on a short chord (a single fixed radius pair would collapse to the bolt path
    // when cast close). Each variant is tuned to converge at its own distance (see `tuned`). We pick
    // the variant whose tuned distance fits the cast (and whose 2·Outer-radius still spans the chord).
    private readonly record struct Band(string Outer, int OuterSlot, float OuterR,
                                        string Inner, int InnerSlot, float InnerR, float MinDist);
    private static readonly Band[] Bands = {
        new("DianaArcThrowOuter",  6, 445f, "DianaArcThrowInner",  4, 550f, 720f), // tuned ~872
        new("DianaArcThrowOuter2", 5, 380f, "DianaArcThrowInner2", 10, 570f, 627f), // tuned ~698
        new("DianaArcThrowOuter3", 7, 350f, "DianaArcThrowInner3", 11, 600f, 518f), // tuned ~556
        new("DianaArcThrowOuter4", 8, 300f, "DianaArcThrowInner4", 12, 620f, 407f), // tuned ~480
        new("DianaArcThrowOuter5", 9, 230f, "DianaArcThrowInner5", 13, 640f, 0f),    // tuned ~334
    };

    public SpellScriptMetadata ScriptMetadata { get; } = new() {
        TriggersSpellCasts = true,
        NotSingleTargetSpell = true,
        DoesntBreakShields = true,
        CastingBreaksStealth = true,
        IsDamagingSpell = true,
    };

    // DELIBERATE DEVIATION FROM S4 (user choice — "modern/playable"). The bolt travels a radius-480
    // circular arc FROM Diana THROUGH the aimed point and stops there; it scales with distance and
    // can hit point-blank.
    //
    // Why radius is FIXED 480 (not midpoint/dist÷2): a circle missile's LINEAR speed = angularVel ×
    // radius, and the client moves it at a FIXED angularVel 3.5 from its own spell data (decomp
    // SpellCircleMissile::UpdateCircleMissile — MissileSpeed is unused). So radius is the ONLY speed
    // lever. S4 used radius 480 → constant 3.5·480 ≈ 1680 u/s (≈ MissileMaxSpeed 1600). A
    // distance-scaled radius made the bolt slow/variable. Fixed 480 restores constant speed.
    //
    // Geometry: aim sits on the radius-480 circle through Diana (chord = aim distance), so the orbit
    // center is on the perpendicular bisector of [Diana, aim], 480 from both. The +angularVel sweep
    // reaches the aim after Δφ = 2·asin(dist/960); we end the missile there (SweepLifetimeOverride =
    // Δφ/angularVel) so it stops at the cursor instead of looping the full 200°. Close aim → small
    // Δφ → quick short arc; far aim → larger Δφ → longer curved arc.
    public void OnSpellPostCast(Spell spell) {
        var owner = spell.CastInfo.Owner;
        var diana = owner.Position;
        var aimRaw = new Vector2(spell.CastInfo.TargetPositionEnd.X, spell.CastInfo.TargetPositionEnd.Z);

        var toAim = aimRaw - diana;
        if (toAim.LengthSquared() <= float.Epsilon) {
            toAim = new Vector2(owner.Direction.X, owner.Direction.Z);
            if (toAim.LengthSquared() <= float.Epsilon) toAim = new Vector2(1f, 0f);
        }
        var dist = Math.Clamp(toAim.Length(), MinRange, MaxRange);
        var aimDir = Vector2.Normalize(toAim);
        var aim = diana + aimDir * dist;

        // All three band arcs pass through BOTH Diana and the aim (centers on the [Diana,aim]
        // perpendicular bisector). Pick the bisector SIDE whose +angularVel sweep reaches the aim the
        // short way (using the bolt radius); the trails reuse that side so the band stays on one side.
        var mid = (diana + aim) * 0.5f;
        var perp = new Vector2(-aimDir.Y, aimDir.X);
        var hMain = MathF.Sqrt(MathF.Max(0f, ArcRadius * ArcRadius - dist * dist * 0.25f));
        var (mainCenter, _) = PickCenter(diana, aim, mid + perp * hMain, mid - perp * hMain);
        var perpSigned = Vector2.Dot(mainCenter - mid, perp) >= 0f ? perp : -perp;
        var level = spell.CastInfo.SpellLevel;

        // Pick the band variant tuned for this cast distance (closer → tighter Outer → wider band).
        var band = Bands[^1];
        foreach (var b in Bands) { if (dist >= b.MinDist) { band = b; break; } }

        // Damaging bolt (radius 480) + the selected cosmetic band edges. Each ends exactly at the aim,
        // so the three taper to a point there; the tuned JSON angularVels make them arrive ~together.
        SpawnBandArc(owner, 0, diana, aim, mid, perpSigned, ArcRadius, level);
        SpawnBandArc(owner, band.OuterSlot, diana, aim, mid, perpSigned, band.OuterR, level);
        SpawnBandArc(owner, band.InnerSlot, diana, aim, mid, perpSigned, band.InnerR, level);
    }

    // Spawns one lens-band arc via SpellCast: a circle of radius `radius` through BOTH Diana and
    // `aim` (orbit center on the [Diana,aim] perpendicular bisector at √(radius²−half²) along
    // `perpSigned`). pos=center → CastInfo.TargetPosition = orbit center (the CLIENT reads the orbit
    // center from TargetPosition); endPos=aim → TargetPositionEnd = the sweep-end aim (the circle
    // missile self-computes its despawn from this via MissileParameters.CircleSweepToTarget). Launch
    // stays Diana (overrideCastPos = Zero). fireWithoutCasting → no NPC_CastSpellAns; the spell's
    // MissileParameters{Circle} auto-creates the orbit missile.
    private static void SpawnBandArc(ObjAIBase owner, int slot, Vector2 diana, Vector2 aim,
                                     Vector2 mid, Vector2 perpSigned, float radius, byte level) {
        var half = Vector2.Distance(diana, aim) * 0.5f;
        var r = MathF.Max(radius, half);                       // chord can't exceed the diameter
        var center = mid + perpSigned * MathF.Sqrt(r * r - half * half);

        SpellCast(owner, slot, SpellSlotType.ExtraSlots, center, aim, true, Vector2.Zero,
                  overrideForceLevel: level);
    }

    // Returns (center, sweepRadians) for whichever candidate center makes the +angularVel sweep
    // travel from Diana to the aim the short way (Δφ in (0, π]).
    private static (Vector2, float) PickCenter(Vector2 diana, Vector2 aim, Vector2 cA, Vector2 cB) {
        foreach (var c in new[] { cA, cB }) {
            var p0 = MathF.Atan2(diana.Y - c.Y, diana.X - c.X);
            var pa = MathF.Atan2(aim.Y - c.Y, aim.X - c.X);
            var d = pa - p0;
            while (d <= 0f) d += MathF.Tau;
            while (d > MathF.Tau) d -= MathF.Tau;
            if (d <= MathF.PI) return (c, d);
        }
        // Degenerate (Diana ≈ aim): fall back to cA with its raw delta.
        var p0b = MathF.Atan2(diana.Y - cA.Y, diana.X - cA.X);
        var pab = MathF.Atan2(aim.Y - cA.Y, aim.X - cA.X);
        var db = pab - p0b; while (db <= 0f) db += MathF.Tau;
        return (cA, db);
    }
}

public class DianaArcThrow : ISpellScript {
    private ObjAIBase _diana;

    public SpellScriptMetadata ScriptMetadata { get; } = new() {
        // SpellCast (fireWithoutCasting) auto-creates this orbit missile; CircleSweepToTarget makes
        // it end at the cast TargetPosition (the aim). DianaArc passes pos=aim, endPos=orbit center.
        NotSingleTargetSpell = true,
        DoesntBreakShields = true,
        CastingBreaksStealth = true,
        IsDamagingSpell   = true,
        MissileParameters = new MissileParameters {
            Type = MissileType.Circle,
            CircleSweepToTarget = true,
        },
    };

    public void OnActivate(ObjAIBase owner, Spell spell) {
        _diana = owner;
        ApiEventManager.OnSpellHit.AddListener(this, spell, OnSpellHit);
    }

    public void OnSpellPreCast(ObjAIBase owner, Spell spell, AttackableUnit target, Vector2 start, Vector2 end)
    {
        ApiEventManager.OnLaunchMissile.AddListener(this, spell, OnLaunchMissile);
    }

    private void OnLaunchMissile(Spell spell, SpellMissile missile)
    {
        ApiEventManager.OnSpellMissileEnd.AddListener(this, missile, OnMissileEnd);
    }

    private void OnMissileEnd(SpellMissile missile)
    {
        var ap = _diana.Stats.AbilityPower.Total * missile.SpellOrigin.SpellData.Coefficient;
        var dmg = missile.SpellOrigin.SpellData.EffectLevelAmount[1][missile.SpellOrigin.CastInfo.SpellLevel] + ap;
        var units = GetUnitsInRange(_diana, missile.Position, 480, true,
            SpellDataFlags.AffectEnemies | SpellDataFlags.AffectNeutral | SpellDataFlags.AffectMinions |
            SpellDataFlags.AffectHeroes);
        AddParticlePos(_diana, "Diana_Base_Q_End.troy", missile.Position, missile.Position);
        AddPosPerceptionBubble(missile.Position, 480f, 0.5f, _diana.Team);
        ApiEventManager.OnLaunchMissile.RemoveListener(this, missile.SpellOrigin, OnLaunchMissile);
        ApiEventManager.OnSpellMissileEnd.RemoveListener(this, missile, OnMissileEnd);
    }

    private void OnSpellHit(Spell spell, AttackableUnit target, SpellMissile missile) {
        // The crescent pierces and damages each enemy it sweeps over once (the spell's flags also
        // list AffectFriends, so filter to enemies here).
        if (target.Team == _diana.Team) return;
        if (!spell.SpellData.IsValidTarget(_diana, target)) return;

        // S4 Crescent Strike numbers live on the Q-slot spell (DianaArc): 60/95/130/165/200 + 0.7 AP.
        // The missile spell's own Effect/Coefficient (50-210 / 0.4) is vestigial sub-cast data.
        var q = _diana.GetSpell("DianaArc");
        var level = Math.Clamp((int)spell.CastInfo.SpellLevel, 1, 5);
        var damage = q.SpellData.EffectLevelAmount[1][level]
                     + _diana.Stats.AbilityPower.Total * q.SpellData.Coefficient;

        AddParticleTarget(_diana, target,"Diana_Base_Q_Tar.troy", target);
        target.TakeDamage(_diana, damage, DamageType.DAMAGE_TYPE_MAGICAL, DamageSource.DAMAGE_SOURCE_SPELL,
                          DamageResultType.RESULT_NORMAL);

        // Moonlight: the debuff Diana R (Lunar Rush) keys off. CombatDehancer, 3.0s (replay-verified).
        AddBuff("DianaMoonlight", 3f, 1, spell, target, _diana);
    }
}

// Cosmetic outer trail edge of the crescent band (Diana_Base_Q_Trail.troy, replay radius 445,
// angularVel 4.2). Spawned by DianaArc.SpawnArc alongside the damaging bolt; carries NO damage
// (no OnSpellHit handler) — purely the visual fan of the moon arc.
public class DianaArcThrowOuter : ISpellScript {
    public SpellScriptMetadata ScriptMetadata { get; } = new() {
        NotSingleTargetSpell = true,
        DoesntBreakShields = true,
        CastingBreaksStealth = true,
        MissileParameters = new MissileParameters { Type = MissileType.Circle, CircleSweepToTarget = true },
    };
}

// Cosmetic inner trail edge of the crescent band (Diana_Base_Q_Trail.troy, replay radius 550,
// angularVel 2.95). Spawned by DianaArc.SpawnArc alongside the bolt; carries NO damage — the other
// visual fan of the moon arc.
public class DianaArcThrowInner : ISpellScript {
    public SpellScriptMetadata ScriptMetadata { get; } = new() {
        NotSingleTargetSpell = true,
        DoesntBreakShields = true,
        CastingBreaksStealth = true,
        MissileParameters = new MissileParameters { Type = MissileType.Circle, CircleSweepToTarget = true },
    };
}

// Wider band variants (selected by cast distance in DianaArc.OnSpellPostCast). Each has its own
// fixed radius + tuned angularVel in its JSON; all are cosmetic (no damage). See the Bands table.
public class DianaArcThrowOuter2 : ISpellScript {
    public SpellScriptMetadata ScriptMetadata { get; } = new() { NotSingleTargetSpell = true, DoesntBreakShields = true, CastingBreaksStealth = true, MissileParameters = new MissileParameters { Type = MissileType.Circle, CircleSweepToTarget = true } };
}
public class DianaArcThrowOuter3 : ISpellScript {
    public SpellScriptMetadata ScriptMetadata { get; } = new() { NotSingleTargetSpell = true, DoesntBreakShields = true, CastingBreaksStealth = true, MissileParameters = new MissileParameters { Type = MissileType.Circle, CircleSweepToTarget = true } };
}
public class DianaArcThrowOuter4 : ISpellScript {
    public SpellScriptMetadata ScriptMetadata { get; } = new() { NotSingleTargetSpell = true, DoesntBreakShields = true, CastingBreaksStealth = true, MissileParameters = new MissileParameters { Type = MissileType.Circle, CircleSweepToTarget = true } };
}
public class DianaArcThrowOuter5 : ISpellScript {
    public SpellScriptMetadata ScriptMetadata { get; } = new() { NotSingleTargetSpell = true, DoesntBreakShields = true, CastingBreaksStealth = true, MissileParameters = new MissileParameters { Type = MissileType.Circle, CircleSweepToTarget = true } };
}
public class DianaArcThrowInner2 : ISpellScript {
    public SpellScriptMetadata ScriptMetadata { get; } = new() { NotSingleTargetSpell = true, DoesntBreakShields = true, CastingBreaksStealth = true, MissileParameters = new MissileParameters { Type = MissileType.Circle, CircleSweepToTarget = true } };
}
public class DianaArcThrowInner3 : ISpellScript {
    public SpellScriptMetadata ScriptMetadata { get; } = new() { NotSingleTargetSpell = true, DoesntBreakShields = true, CastingBreaksStealth = true, MissileParameters = new MissileParameters { Type = MissileType.Circle, CircleSweepToTarget = true } };
}
public class DianaArcThrowInner4 : ISpellScript {
    public SpellScriptMetadata ScriptMetadata { get; } = new() { NotSingleTargetSpell = true, DoesntBreakShields = true, CastingBreaksStealth = true, MissileParameters = new MissileParameters { Type = MissileType.Circle, CircleSweepToTarget = true } };
}
public class DianaArcThrowInner5 : ISpellScript {
    public SpellScriptMetadata ScriptMetadata { get; } = new() { NotSingleTargetSpell = true, DoesntBreakShields = true, CastingBreaksStealth = true, MissileParameters = new MissileParameters { Type = MissileType.Circle, CircleSweepToTarget = true } };
}
