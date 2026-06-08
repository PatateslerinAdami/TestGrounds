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
using LeagueSandbox.GameServer.GameObjects.SpellNS.Sector;
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

        var baseAngle = MathF.Atan2(facing.Y, facing.X);
        var deltaAngle = MathF.Tau / OrbCount;
        
        for (var i = 0; i < OrbCount; i++) {
            var angle = baseAngle + i * deltaAngle;
            var orbPos = _ahri.Position + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * OrbRadius;
            
            
            List<CastTarget> targets = new List<CastTarget>();
            targets.Add(new CastTarget(_ahri, HitResult.HIT_Normal));
            SpellCast(_ahri, 2, SpellSlotType.ExtraSlots, orbPos, orbPos, true, _ahri.Position, targets,
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
        var unitsInRange = EnumerateValidUnitsInRange(_ahri, missile.Position, 750f, true, 
                SpellDataFlags.AffectEnemies | SpellDataFlags.AffectNeutral | SpellDataFlags.AffectHeroes | SpellDataFlags.AffectMinions)
            .OrderByDescending(unit => unit is Champion).ThenBy(unit => Vector2.DistanceSquared(missile.Position, unit.Position)).ToList();
        if (unitsInRange.Count == 0) return;
        
        SpellCast(_ahri, 3, SpellSlotType.ExtraSlots, true, unitsInRange.First(), missile.Position);
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
    
    private void OnSpellHit(Spell spell, AttackableUnit target, SpellMissile missile, SpellSector sector) {
        if (!spell.SpellData.IsValidTarget(_ahri, target)) return;
        var mainSpell = _ahri.GetSpell("AhriFoxFire");
        var ap = _ahri.Stats.AbilityPower.Total * mainSpell.SpellData.Coefficient;
        var damage = mainSpell.SpellData.EffectLevelAmount[1][mainSpell.CastInfo.SpellLevel] + ap;

        AddParticleTarget(_ahri, target, "Ahri_FoxFire_tar", target);
        target.TakeDamage(_ahri, damage, DamageType.DAMAGE_TYPE_MAGICAL, DamageSource.DAMAGE_SOURCE_SPELL, DamageResultType.RESULT_NORMAL);
    }
}
