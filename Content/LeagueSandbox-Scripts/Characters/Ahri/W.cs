using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.Buildings;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.SpellNS.Missile;
using LeagueSandbox.GameServer.Scripting.CSharp;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace Spells;

public class AhriFoxFire : ISpellScript {
    private  ObjAIBase _ahri;
    private const int   OrbCount  = 3;
    private const float OrbRadius = 150.0f;

    public SpellScriptMetadata ScriptMetadata { get; } = new() {
        TriggersSpellCasts = true
    };

    public void OnActivate(ObjAIBase owner, Spell spell)
    {
        _ahri = owner;
    }

    public void OnSpellPostCast(Spell spell) {
        AddParticleTarget(_ahri, _ahri, "Ahri_FoxFire_cas", _ahri);
        var foxfireBuff = AddBuff("AhriFoxFire", 5f, 1, spell, _ahri, _ahri);
        foxfireBuff.Variables.Set("orbsConsumed", 0);
        
        var facing = new Vector2(_ahri.Direction.X, _ahri.Direction.Z);
        if (facing.LengthSquared() <= float.Epsilon) facing = new Vector2(1.0f, 0.0f);
        facing = Vector2.Normalize(facing);

        // S1 places the three orbs at facing +45° / +165° / +285° (BBGetPointByUnitFacingOffset,
        // Distance 150) — a 120° spacing rotated 45° off the facing vector.
        var baseAngle = MathF.Atan2(facing.Y, facing.X) + MathF.PI / 4.0f;
        var deltaAngle = MathF.Tau / OrbCount;

        for (var i = 0; i < OrbCount; i++) {
            var angle = baseAngle + i * deltaAngle;
            var orbPos = _ahri.Position + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * OrbRadius;

            // Faithful to S1 (BBSpellCast TargetVar=Attacker, OverrideCastPosVar=Point): the orb
            // orbits the cast TARGET (Ahri, tracked live) and spawns at the override-cast point.
            // The circle-missile engine reads the orbit center from Targets[0] and the radius/phase
            // from the launch (= override-cast) position relative to it.
            SpellCast(_ahri, 2, SpellSlotType.ExtraSlots, true, _ahri, orbPos,
                      overrideForceLevel: spell.CastInfo.SpellLevel);
        }
    }
}

public class AhriFoxFireMissile : ISpellScript {
    private ObjAIBase _ahri;

    public SpellScriptMetadata ScriptMetadata { get; } = new() {
        IsDamagingSpell   = true,
        MissileParameters = new MissileParameters {
            Type = MissileType.Circle,
        },
    };

    public void OnActivate(ObjAIBase owner, Spell spell) {
        _ahri = owner;
        ApiEventManager.OnLaunchMissile.AddListener(this, spell , OnLaunchMissile);
    }

    private void OnLaunchMissile(Spell spell,SpellMissile missile)
    {
        ApiEventManager.OnSpellMissileUpdate.AddListener(this, missile, OnUpdateMissile);
    }

    private void OnUpdateMissile(SpellMissile missile, float diff)
    {
        // S1 AhriFoxFireMissile.SpellOnMissileUpdateBuildingBlocks gates the seek behind a
        // per-orb counter: it bumps SpellVars.Ready each update tick (paced by the spell's
        // LuaOnMissileUpdateDistanceInterval = 75u) and only starts looking for a target once
        // Ready >= 3 — so each orb orbits Ahri for ~3 intervals (~225u of arc) before it can
        // launch. Each orb is its own cast, so the counter lives on the orb's own CastInfo.
        var ready = missile.CastInfo.Variables.GetInt("Ready") + 1;
        missile.CastInfo.Variables.Set("Ready", ready);
        if (ready < 3) return;

        // Literal S1 two-phase acquisition (both BBForNClosestVisibleUnitsInTargetArea, Range 650
        // — not the ini CastRadius of 710): first the nearest enemy HERO, and only if none was
        // found (BBIf Count == 0) the nearest of any valid unit. The Visible variant means
        // foxfires never acquire fog-hidden/stealthed targets.
        var targets = ForNClosestVisibleUnitsInTargetArea(_ahri, missile.Position, 650f, 1,
            SpellDataFlags.AffectEnemies | SpellDataFlags.AffectHeroes);
        if (targets.Count == 0)
        {
            targets = ForNClosestVisibleUnitsInTargetArea(_ahri, missile.Position, 650f, 1,
                SpellDataFlags.AffectEnemies | SpellDataFlags.AffectNeutral | SpellDataFlags.AffectMinions | SpellDataFlags.AffectHeroes);
        }
        if (targets.Count == 0) return;

        SpellCast(_ahri, 3, SpellSlotType.ExtraSlots, true, targets[0], missile.Position);
        var foxFireBuff = _ahri.GetBuffWithName("AhriFoxFire");
        if (foxFireBuff != null)
        {
            var consumed = foxFireBuff.Variables.GetInt("orbsConsumed") + 1;
            foxFireBuff.Variables.Set("orbsConsumed", consumed);
            if (consumed >= 3) {
                if (foxFireBuff != null) _ahri.RemoveBuff(foxFireBuff);
            }
        }

        ApiEventManager.OnSpellMissileUpdate.RemoveListener(this, missile, OnUpdateMissile);
        missile.SetToRemove();
    }
}


public class AhriFoxFireMissileTwo : ISpellScript {
    private ObjAIBase _ahri;

    public SpellScriptMetadata ScriptMetadata { get; } = new() {
        IsDamagingSpell   = true,
        MissileParameters = new MissileParameters {
            Type = MissileType.Target,
        },
    };

    public void OnActivate(ObjAIBase owner, Spell spell) {
        _ahri = owner;
        ApiEventManager.OnSpellHit.AddListener(this, spell, OnSpellHit);
    }
    
    private void OnSpellHit(Spell spell, AttackableUnit target, SpellMissile missile) {
        if (!spell.SpellData.IsValidTarget(_ahri, target)) return;
        var mainSpell = _ahri.GetSpell("AhriFoxFire");
        var ap = _ahri.Stats.AbilityPower.Total * mainSpell.SpellData.Coefficient;
        var damage = mainSpell.SpellData.EffectLevelAmount[1][mainSpell.CastInfo.SpellLevel] + ap;

        // S1 AhriFoxFireMissileTwo.TargetExecuteBuildingBlocks: the FIRST foxfire to hit a target
        // deals full damage; every subsequent foxfire on the SAME target deals half. The S1 reduced
        // values {20,35,50,65,80}+0.1875·AP are exactly half of the full {40,70,100,130,160}+0.375·AP,
        // so the rule is a clean ×0.5. "Already hit" is tracked with an AhriFoxFireMissileTwo buff on
        // the target (resolves to BuffScriptEmpty — presence is all we need).
        if (target.HasBuff("AhriFoxFireMissileTwo")) {
            damage *= 0.5f;
        } else {
            AddBuff("AhriFoxFireMissileTwo", 6f, 1, spell, target, _ahri);
        }

        AddParticleTarget(_ahri, target, "Ahri_FoxFire_tar", target);
        target.TakeDamage(_ahri, damage, DamageType.DAMAGE_TYPE_MAGICAL, DamageSource.DAMAGE_SOURCE_SPELL, DamageResultType.RESULT_NORMAL);
    }
}
