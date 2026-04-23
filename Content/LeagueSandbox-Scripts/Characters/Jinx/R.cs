using System;
using System.Linq;
using System.Numerics;
using Buffs;
using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using GameServerLib.GameObjects.AttackableUnits;
using LeaguePackets.Game.Events;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.Buildings;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.SpellNS.Missile;
using LeagueSandbox.GameServer.GameObjects.SpellNS.Sector;
using LeagueSandbox.GameServer.Logging;
using LeagueSandbox.GameServer.Scripting.CSharp;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace Spells;

public class JinxR : ISpellScript {
    private ObjAIBase _jinx;
    private Spell _spell;
    private bool _boosted = false;
    public SpellScriptMetadata ScriptMetadata { get; }  = new () {
        MissileParameters = new MissileParameters() {
            Type = MissileType.Circle
        },
        TriggersSpellCasts = true,
    };

    public void OnActivate(ObjAIBase owner, Spell spell) {
        _jinx = owner;
        _spell = spell;
        ApiEventManager.OnSpellHit.AddListener(this, spell, OnSpellHit);
        ApiEventManager.OnLaunchMissile.AddListener(this, spell, OnLaunchMissile);
    }

    public void OnSpellPreCast(ObjAIBase owner, Spell spell, AttackableUnit target, Vector2 start, Vector2 end) {
        _boosted = false;
        //AddParticleTarget(_jinx, _jinx, "Jinx_R_Cas",          _jinx);
        //AddParticleTarget(_jinx, _jinx, "Jinx_R_Booster",      _jinx);
        //AddParticleTarget(_jinx, _jinx, "Jinx_R_Beam",         _jinx);
        //AddParticleTarget(_jinx, _jinx, "Jinx_R_Rocket_Child", _jinx);
        //AddParticleTarget(_jinx, _jinx, "Jinx_R_Special_vo",   _jinx);
    }

    public void OnSpellCast(Spell spell) {
        if (_jinx.IsDead) {
            spell.SetCooldown(0.5f, true);
        }
    }

    public void OnLaunchMissile(Spell spell, SpellMissile missile) {
        ApiEventManager.OnSpellMissileUpdate.AddListener(this, missile, OnMissileUpdate);
    }

    public void OnUpdate(float diff) {
        
    }

    public void OnMissileUpdate(SpellMissile missile, float diff) {
        LogInfo("Distance: ");
        if(_boosted) return;
        var castPosition = new Vector2(_spell.CastInfo.SpellCastLaunchPosition.X, _spell.CastInfo.SpellCastLaunchPosition.Z);
        var distance = Vector2.Distance(missile.Position, castPosition);
        LogInfo("Distance: " + distance);
        if (distance < 1350f) return;
        AddParticleTarget(_jinx, missile, "Jinx_R_Booster", missile, 25f);
        missile.SetSpeed(2200f);
        _boosted = true;
    }

    private void OnSpellHit(Spell spell, AttackableUnit target, SpellMissile missile, SpellSector sector) {
        if (target is not Champion) return;
        var castPosition = new Vector2(spell.CastInfo.SpellCastLaunchPosition.X, spell.CastInfo.SpellCastLaunchPosition.Z);
        var distance     = Vector2.Distance(target.Position, castPosition);
        LogInfo("Distance: " + distance);

            
        var bonusAd   = _jinx.Stats.AttackDamage.FlatBonus;
        var minDamage = 125f + 0.5f * bonusAd;
        var maxDamage = 250f + 1.0f * bonusAd;

        // Damage over flight time
        var travelSeconds       = missile != null ? missile.GetTimeSinceCreation() / 1000f : 0f;
        var ramp                = Math.Clamp(travelSeconds, 0f, 1f);
        var baseDamage          = minDamage + (maxDamage - minDamage) * ramp;
        var missingHealthDamage = (target.Stats.HealthPoints.Total - target.Stats.CurrentHealth) * 0.25f;
        var primaryDamage       = baseDamage + missingHealthDamage;
        var splashDamage        = primaryDamage * 0.8f;
        LogInfo($"R ramp={ramp}, primaryDamage={primaryDamage}");

        switch (distance) {
            case >= 1500f:
                //AddParticlePos(_jinx, "Jinx_R_Tar_Super", target.Position, target.Position);
                AddParticlePos(_jinx, "Jinx_R_Tar",       target.Position, target.Position);
                break;
            case >= 1000f and < 1500f:
                AddParticlePos(_jinx, "Jinx_R_Tar",       target.Position, target.Position);
                break;
            default:
                AddParticleTarget(_jinx, null, "Jinx_R_Tar_Weak",     target);
                break;
        }
            
        var unitsInArea = GetUnitsInRange(target.Position, 1000f, true).Where(unit => unit.Team != _jinx.Team && unit is not ObjBuilding).ToList();
        foreach (var unit in unitsInArea) {
            if (unit == target) {
                unit.TakeDamage(_jinx, primaryDamage, DamageType.DAMAGE_TYPE_PHYSICAL, DamageSource.DAMAGE_SOURCE_SPELLAOE, false);
            } else {
                unit.TakeDamage(_jinx, splashDamage, DamageType.DAMAGE_TYPE_PHYSICAL, DamageSource.DAMAGE_SOURCE_SPELLAOE, false);
            }
                
        }
        missile.SetToRemove();
    }
}
