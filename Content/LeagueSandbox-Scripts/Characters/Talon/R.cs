using System;
using System.Collections.Generic;
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
    private readonly List<SpellMissile> _activeMissiles = new();
    private readonly Dictionary<SpellMissile, Vector2> _lastMissilePositions = new();
    private ObjAIBase _talon;
    private Spell     _spell;

    public SpellScriptMetadata ScriptMetadata { get; private set; } = new SpellScriptMetadata() {
        MissileParameters = new MissileParameters{
            Type = MissileType.Target,
        },
        IsDamagingSpell    = true,
    };

    public void OnActivate(ObjAIBase owner, Spell spell) {
        _talon = owner;
        ApiEventManager.OnSpellHit.AddListener(this, spell, OnSpellHit);
        ApiEventManager.OnLaunchMissile.AddListener(this, spell, OnLaunchMissile);
    }

    public void OnSpellPreCast(ObjAIBase owner, Spell spell, AttackableUnit target, Vector2 start, Vector2 end) {
        _spell = spell;
    }

    public void OnUpdate(float diff) {
        for (var i = _activeMissiles.Count - 1; i >= 0; i--) {
            var missile = _activeMissiles[i];
            if (missile == null || missile.IsToRemove()) {
                RemoveMissileAt(i);
                continue;
            }

            var currentPosition = missile.Position;
            var lastPosition = _lastMissilePositions.TryGetValue(missile, out var savedPosition) ? savedPosition : currentPosition;

            ApplyDamageOnMissilePath(missile, lastPosition, currentPosition);

            if (HasReachedOwner(missile, currentPosition)) {
                missile.SetToRemove();
                RemoveMissileAt(i);
                continue;
            }

            _lastMissilePositions[missile] = currentPosition;
        }
    }

    private void OnLaunchMissile(Spell spell, SpellMissile missile) {
        if (missile == null) return;
        _activeMissiles.Add(missile);
        _lastMissilePositions[missile] = missile.Position;
    }

    private void ApplyDamageOnMissilePath(SpellMissile missile, Vector2 start, Vector2 end) {
        var segment = end - start;
        var segmentLength = segment.Length();
        var hitRadius = MathF.Max(60f, missile.CollisionRadius * 0.5f);
        var queryRadius = (segmentLength * 0.5f) + hitRadius;
        var queryCenter = start + (segment * 0.5f);

        var unitsInRange = GetUnitsInRange(
            _talon,
            queryCenter,
            queryRadius,
            true,
            SpellDataFlags.AffectEnemies | SpellDataFlags.AffectNeutral | SpellDataFlags.AffectHeroes | SpellDataFlags.AffectMinions
        );

        foreach (var unit in unitsInRange) {
            if (unit == null || unit.Team == _talon.Team || unit.HasBuff("TalonShadowAssaultHitReturn")) continue;

            var combinedRadius = hitRadius + unit.CollisionRadius;
            if (DistanceSquaredPointToSegment(unit.Position, start, end) > combinedRadius * combinedRadius) continue;

            DealDamage(unit);
        }
    }

    private bool HasReachedOwner(SpellMissile missile, Vector2 missilePosition) {
        var endRadius = _talon.CollisionRadius + MathF.Max(35f, missile.CollisionRadius * 0.5f);
        return Vector2.DistanceSquared(missilePosition, _talon.Position) <= endRadius * endRadius;
    }

    private static float DistanceSquaredPointToSegment(Vector2 point, Vector2 start, Vector2 end) {
        var segment = end - start;
        var lengthSquared = segment.LengthSquared();
        if (lengthSquared <= float.Epsilon) {
            return Vector2.DistanceSquared(point, start);
        }

        var t = Vector2.Dot(point - start, segment) / lengthSquared;
        t = Math.Clamp(t, 0f, 1f);
        var projection = start + segment * t;
        return Vector2.DistanceSquared(point, projection);
    }

    private void RemoveMissileAt(int index) {
        var missile = _activeMissiles[index];
        _activeMissiles.RemoveAt(index);
        if (missile != null) {
            _lastMissilePositions.Remove(missile);
        }
    }

    private void RemoveMissile(SpellMissile missile) {
        if (missile == null) return;
        _activeMissiles.Remove(missile);
        _lastMissilePositions.Remove(missile);
    }

    private void DealDamage(AttackableUnit target) {
        var dmg = 120f + 50f * (_talon.GetSpell("TalonShadowAssault").CastInfo.SpellLevel - 1) + _talon.Stats.AttackDamage.FlatBonus * _spell.SpellData.Coefficient;
        
        AddParticleTarget(_talon, target, "talon_ult_tar", target);
        target.TakeDamage(_talon, dmg, DamageType.DAMAGE_TYPE_PHYSICAL, DamageSource.DAMAGE_SOURCE_SPELLAOE, DamageResultType.RESULT_NORMAL);
        AddBuff("TalonShadowAssaultHitReturn", 1f, 1, _spell, target, _talon);
    }

    private void OnSpellHit(Spell spell, AttackableUnit target, SpellMissile missile, SpellSector sector) {
        if (target != _talon || missile == null) return;

        if (_lastMissilePositions.TryGetValue(missile, out var lastPosition)) {
            ApplyDamageOnMissilePath(missile, lastPosition, missile.Position);
        }

        missile.SetToRemove();
        RemoveMissile(missile);
    }
}
