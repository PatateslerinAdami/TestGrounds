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

public class DianaOrbs : ISpellScript {
    private  ObjAIBase _diana;
    private const int   OrbCount  = 3;
    private const float OrbRadius = 150.0f;

    public SpellScriptMetadata ScriptMetadata { get; } = new() {
        TriggersSpellCasts = true
    };

    public void OnActivate(ObjAIBase owner, Spell spell)
    {
        _diana = owner;
    }

    public void OnSpellPostCast(Spell spell) {
        var orbsBuff = AddBuff("DianaOrbs", 5f, 1, spell, _diana, _diana);
        orbsBuff.Variables.Set("orbsConsumed", 0);
        AddBuff("DianaShield", 5f, 1, spell, _diana, _diana);
        var owner = spell.CastInfo.Owner;
        var facing = new Vector2(owner.Direction.X, owner.Direction.Z);
        if (facing.LengthSquared() <= float.Epsilon) facing = new Vector2(1.0f, 0.0f);
        facing = Vector2.Normalize(facing);

        var baseAngle = MathF.Atan2(facing.Y, facing.X);
        var deltaAngle = MathF.Tau / OrbCount;
        AddParticleTarget(_diana, _diana, "Diana_Base_W_Cas", _diana);
        for (var i = 0; i < OrbCount; i++) {
            var angle = baseAngle + i * deltaAngle;
            var orbPos = owner.Position + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * OrbRadius;

            // Self-orbit cast: the orb orbits the cast TARGET (Diana, tracked live) and spawns at
            // the override-cast point. The circle-missile engine reads the orbit center from
            // Targets[0] and the radius/phase from the launch (= override-cast) position.
            SpellCast(owner, 3, SpellSlotType.ExtraSlots, true, owner, orbPos,
                      overrideForceLevel: spell.CastInfo.SpellLevel);
        }
    }
}

public class DianaOrbsMissile : ISpellScript {
    private ObjAIBase _diana;

    public SpellScriptMetadata ScriptMetadata { get; } = new() {
        IsDamagingSpell   = true,
        MissileParameters = new MissileParameters {
            Type = MissileType.Circle,
            CollisionRadius = 150,
        },
    };

    public void OnActivate(ObjAIBase owner, Spell spell) {
        _diana = owner;
        ApiEventManager.OnSpellHit.AddListener(this, spell, OnSpellHit);
    }
    
    private void OnSpellHit(Spell spell, AttackableUnit target, SpellMissile missile) {
        if (!spell.SpellData.IsValidTarget(_diana, target)) return;

        var spellLevel = Math.Clamp((int) spell.CastInfo.SpellLevel, 1, 5);
        var damage = 22.0f + 12.0f * (spellLevel - 1) + _diana.Stats.AbilityPower.Total * _diana.GetSpell("DianaOrbs").SpellData.Coefficient;

        AddParticleTarget(_diana, target, "Diana_Base_W_Tar.troy", target);
        target.TakeDamage(_diana, damage, DamageType.DAMAGE_TYPE_MAGICAL, DamageSource.DAMAGE_SOURCE_SPELLAOE,
                          DamageResultType.RESULT_NORMAL);
        var unitsInRange = GetUnitsInRange(_diana, missile.Position, 175f, true,
            SpellDataFlags.AffectEnemies | SpellDataFlags.AffectNeutral | SpellDataFlags.AffectMinions |
            SpellDataFlags.AffectHeroes).Where(unit => unit != target);

        foreach (var unit in unitsInRange)
        {
            AddParticleTarget(_diana, unit, "Diana_Base_W_Tar.troy", unit);
            unit.TakeDamage(_diana, damage, DamageType.DAMAGE_TYPE_MAGICAL, DamageSource.DAMAGE_SOURCE_SPELLAOE,
                DamageResultType.RESULT_NORMAL);
        }
        AddParticlePos(_diana, "Diana_Base_W_Orb_End.troy", missile.Position, missile.Position);
        
        var orbsBuff = _diana.GetBuffWithName("DianaOrbs");
        if (orbsBuff != null) {
            var consumed = orbsBuff.Variables.GetInt("orbsConsumed") + 1;
            orbsBuff.Variables.Set("orbsConsumed", consumed);
            if (consumed >= 3) {
                var oldShield = _diana.GetBuffWithName("DianaShield");
                if (oldShield != null) _diana.RemoveBuff(oldShield);
                var refreshVars = new BuffVariables();
                refreshVars.Set("isRefresh", true);
                AddBuff("DianaShield", 5f, 1, spell, _diana, _diana, buffVariables: refreshVars);
            }
        }

        missile.SetToRemove();
    }
}
