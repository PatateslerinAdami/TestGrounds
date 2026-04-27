using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Buffs;
using CharScripts;
using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using GameServerLib.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.Buildings;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.SpellNS.Missile;
using LeagueSandbox.GameServer.GameObjects.SpellNS.Sector;
using LeagueSandbox.GameServer.Logging;
using LeagueSandbox.GameServer.Scripting.CSharp;
using log4net.Repository.Hierarchy;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace Spells;

public class TalonShadowAssault : ISpellScript {
    private ObjAIBase _talon;
    private bool      _isCast, _isCast2;
    private float     _returnTimer = 0;
    private float     _bladeTimer  = 0;
    
    private const int BladeCount = 9;
    private readonly Vector2[] _positions = new Vector2[BladeCount];
    private readonly Particle[] _neutral = new Particle[BladeCount];
    private readonly Particle[] _teamBased = new Particle[BladeCount];
    

    public SpellScriptMetadata ScriptMetadata { get; private set; } = new SpellScriptMetadata() {
        IsDamagingSpell     = true,
        TriggersSpellCasts = true
    };

    public void OnActivate(ObjAIBase owner, Spell spell) {
        _talon = owner;
        ApiEventManager.OnUpdateStats.AddListener(this, _talon, OnUpdateStats);
        ApiEventManager.OnSpellCast.AddListener(this, _talon.GetSpell("TalonRake"),            OnBreakCast);
        ApiEventManager.OnSpellCast.AddListener(this, _talon.GetSpell("TalonCutthroat"),       OnBreakCast);
        ApiEventManager.OnLaunchAttack.AddListener(this, _talon, OnBreakCast);
    }

    public void OnSpellPreCast(ObjAIBase owner, Spell spell, AttackableUnit target, Vector2 start, Vector2 end) {
        _bladeTimer = 200f;
        _returnTimer = 2500f;
        _isCast      = true;
        _isCast2    = false;
        AddBuff("TalonShadowAssaultAnimBuff", 0.5f, 1, spell, _talon, _talon);
        AddBuff("TalonShadowAssaultBuff", 2.5f, 1, spell, _talon, _talon);
        AddParticleTarget(_talon, _talon, "talon_ult_sound", _talon);
        AddParticleTarget(_talon, _talon, "talon_ult_cas",   _talon, bone: "root");
        float[] angles = { 0, 180, 135, 90, 45, -45, -90, -135, -180 };
        for (var i = 0; i < BladeCount; i++) {
            _positions[i] = GetPointFromUnit(_talon, 500, angles[i]);
            SpellCast(_talon, 3, SpellSlotType.ExtraSlots, _positions[i], _positions[i], true, Vector2.Zero);
        }
    }

    public void OnUpdate(float diff) {
        if (!_isCast)return;
        _returnTimer -= diff;
        _bladeTimer -= diff;
        if (_bladeTimer <= 0 && !_isCast2) {
            //neutral
            for (var i = 0; i < BladeCount; i++) {
                _neutral[i] = AddParticlePos(_talon, "talon_ult_blade_hold", _positions[i], _positions[i], 2.3f, bone: "root");
                _teamBased[i] = AddParticlePos(_talon, "talon_ult_blade_hold_team_ID_green", _positions[i], _positions[i], 2.3f, enemyParticle: "talon_ult_blade_hold_team_ID_red");
            }
            _isCast2 = true;
        }
        
        if (!(_returnTimer <= 0)) return;
        for (var i = 0; i < BladeCount; i++) {
            SpellCast(_talon, 4, SpellSlotType.ExtraSlots, true, _talon, _positions[i]);
        }
        _isCast  = false;
    }

    private void OnBreakCast(Spell spell) {
        RemoveBuff(_talon,"TalonShadowAssaultBuff");
        _bladeTimer  = 0f;
        _returnTimer = 0f;
        for (var i = 0; i < BladeCount; i++) {
            RemoveParticle(_neutral[i]);
            RemoveParticle(_teamBased[i]);
        }
    }

    private void OnUpdateStats(AttackableUnit owner,float diff) {
        SetSpellToolTipVar(owner, 0, _talon.Stats.AttackDamage.FlatBonus * 0.6f, SpellbookType.SPELLBOOK_CHAMPION, 3, SpellSlotType.SpellSlots);
    }
}


public class TalonShadowAssaultMisOne : ISpellScript {
    private ObjAIBase _talon;

    public SpellScriptMetadata ScriptMetadata { get; private set; } = new SpellScriptMetadata() {
        MissileParameters = new MissileParameters{
            Type = MissileType.Circle,
        },
        IsDamagingSpell    = true,
    };

    public void OnActivate(ObjAIBase owner, Spell spell) {
        _talon = owner;
        ApiEventManager.OnSpellHit.AddListener(this, spell, OnSpellHit);
    }

    private void OnSpellHit(Spell spell, AttackableUnit target, SpellMissile missile, SpellSector sector) {
        if (target.Team == _talon.Team) return;
        if (target.HasBuff("TalonShadowAssaultHit")) return;
        var dmg = 120f + 50f * (_talon.GetSpell("TalonShadowAssault").CastInfo.SpellLevel - 1) + _talon.Stats.AttackDamage.FlatBonus * _talon.GetSpell("TalonShadowAssaultMisTwo").SpellData.Coefficient;
        
        AddParticleTarget(_talon, target, "talon_ult_tar", target);
        target.TakeDamage(_talon, dmg, DamageType.DAMAGE_TYPE_PHYSICAL, DamageSource.DAMAGE_SOURCE_SPELLAOE, DamageResultType.RESULT_NORMAL);
        AddBuff("TalonShadowAssaultHit", 0.5f, 1, spell, target, _talon);
    }
}

public class TalonShadowAssaultMisOneHalf : ISpellScript {

    public SpellScriptMetadata ScriptMetadata { get; private set; } = new SpellScriptMetadata() {
        MissileParameters = new MissileParameters {
            Type = MissileType.Circle,
        },
        IsDamagingSpell = false,
    };
}

public class TalonShadowAssaultMisTwo : ISpellScript {
    private          ObjAIBase _talon;
    private          Spell     _spell;
    private SpellMissile               _missile;
    private bool               _isFinished = true;

    public SpellScriptMetadata ScriptMetadata { get; private set; } = new SpellScriptMetadata() {
        MissileParameters = new MissileParameters{
            Type = MissileType.Target,
        },
        IsDamagingSpell    = true,
    };

    public void OnActivate(ObjAIBase owner, Spell spell) {
        _talon = owner;
        ApiEventManager.OnSpellHit.AddListener(this, spell, OnSpellHit);
    }

    public void OnSpellPreCast(ObjAIBase owner, Spell spell, AttackableUnit target, Vector2 start, Vector2 end) {
        _isFinished = false;
        _spell      = spell;
        _missile    = spell.CreateSpellMissile();
    }

    public void OnUpdate(float diff) {
        if (_isFinished) return;
        var unitsInRange = GetUnitsInRange(_talon, _missile.Position, _missile.CollisionRadius * 2, true, SpellDataFlags.AffectEnemies | SpellDataFlags.AffectNeutral | SpellDataFlags.AffectHeroes | SpellDataFlags.AffectMinions)
            .Where(unit => !unit.HasBuff("TalonShadowAssaultHitReturn"));
        foreach (var unit in unitsInRange) {
            if (unit == _talon) {
                _isFinished = true;
                _missile.SetToRemove();
                continue;
            }

            if (unit.Team == _talon.Team) continue;
            DealDamage(unit);
        }
    }

    private void DealDamage(AttackableUnit target) {
        var dmg = 120f + 50f * (_talon.GetSpell("TalonShadowAssault").CastInfo.SpellLevel - 1) + _talon.Stats.AttackDamage.FlatBonus * _spell.SpellData.Coefficient;
        
        AddParticleTarget(_talon, target, "talon_ult_tar", target);
        target.TakeDamage(_talon, dmg, DamageType.DAMAGE_TYPE_PHYSICAL, DamageSource.DAMAGE_SOURCE_SPELLAOE, DamageResultType.RESULT_NORMAL);
        AddBuff("TalonShadowAssaultHitReturn", 1f, 1, _spell, target, _talon);
    }

    private void OnSpellHit(Spell spell, AttackableUnit target, SpellMissile missile, SpellSector sector) {
        if (target != _talon) return;
        _missile.SetToRemove();
        _isFinished = true;
        ApiEventManager.OnSpellHit.RemoveListener(this, spell, OnSpellHit);
    }
}