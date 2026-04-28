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
using LeagueSandbox.GameServer.Scripting.CSharp;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace Spells;

public class TalonRake : ISpellScript {
    private ObjAIBase _talon;
    private Vector2   _pos1, _pos2, _pos3;

    public SpellScriptMetadata ScriptMetadata { get; private set; } = new SpellScriptMetadata() {
        TriggersSpellCasts = true,
        IsDamagingSpell    = true,
        CastingBreaksStealth = true
    };

    public void OnActivate(ObjAIBase owner, Spell spell) {
        _talon = owner;
        ApiEventManager.OnUpdateStats.AddListener(this, _talon, OnUpdateStats);
    }

    public void OnSpellCast(Spell spell) {
        _pos1 = GetPointFromUnit(_talon, 750, 20);
        SpellCast(_talon, 1, SpellSlotType.ExtraSlots, _pos1, _pos1, true, Vector2.Zero);
        _pos2 = GetPointFromUnit(_talon, 750, 0);
        SpellCast(_talon, 1, SpellSlotType.ExtraSlots, _pos2, _pos2, true, Vector2.Zero);
        _pos3 = GetPointFromUnit(_talon, 750, -20);
        SpellCast(_talon, 1, SpellSlotType.ExtraSlots, _pos3, _pos3, true, Vector2.Zero);
    }

    private void OnUpdateStats(AttackableUnit owner, float diff) {
        SetSpellToolTipVar(owner, 0, _talon.Stats.AttackDamage.FlatBonus * 0.6f, SpellbookType.SPELLBOOK_CHAMPION, 1,
                           SpellSlotType.SpellSlots);
    }
}

public class TalonRakeMissileOne : ISpellScript {
    private ObjAIBase _talon;

    public SpellScriptMetadata ScriptMetadata { get; private set; } = new SpellScriptMetadata() {
        MissileParameters = new MissileParameters {
            Type = MissileType.Circle,
        },
        IsDamagingSpell = true,
    };

    public void OnActivate(ObjAIBase owner, Spell spell) {
        _talon = owner;
        ApiEventManager.OnSpellHit.AddListener(this, spell, OnSpellHit);
    }

    public void OnSpellPreCast(ObjAIBase owner, Spell spell, AttackableUnit target, Vector2 start, Vector2 end) {
        ApiEventManager.OnSpellMissileEnd.AddListener(this, spell.CreateSpellMissile(), OnMissileEnd);
    }

    private void OnSpellHit(Spell spell, AttackableUnit target, SpellMissile missile, SpellSector sector) {
        if (target.HasBuff("TalonRake")) return;
        var dmg = 30 + 25                             * (_talon.GetSpell("TalonRake").CastInfo.SpellLevel - 1) +
                  _talon.Stats.AttackDamage.FlatBonus * _talon.GetSpell("TalonRakeMissileTwo").SpellData.Coefficient;

        target.TakeDamage(_talon, dmg, DamageType.DAMAGE_TYPE_PHYSICAL, DamageSource.DAMAGE_SOURCE_SPELLAOE,
                          DamageResultType.RESULT_NORMAL);
        AddParticleTarget(_talon, target, "talon_w_tar", target);
        AddBuff("TalonRake", 0.2f, 1, spell, target, _talon);
    }

    private void OnMissileEnd(SpellMissile missile) {
        SpellCast(_talon, 2, SpellSlotType.ExtraSlots, true, _talon, missile.Position);
        ApiEventManager.OnSpellMissileEnd.RemoveListener(this, missile, OnMissileEnd);
    }
}

public class TalonRakeMissileTwo : ISpellScript {
    private          ObjAIBase _talon;
    private          Spell     _spell;
    private SpellMissile               _missile;
    private bool               _isFinished = true;

    public SpellScriptMetadata ScriptMetadata { get; private set; } = new SpellScriptMetadata() {
        MissileParameters = new MissileParameters {
            Type = MissileType.Target,
        },
        IsDamagingSpell = true,
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
        var unitsInRange = GetUnitsInRange(_talon, _missile.Position, _missile.CollisionRadius *2, true, SpellDataFlags.AffectEnemies | SpellDataFlags.AffectNeutral | SpellDataFlags.AffectHeroes | SpellDataFlags.AffectMinions)
            .Where(unit => !unit.HasBuff("TalonRakeReturn"));
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

    private void DealDamage(AttackableUnit unit) {
        var dmg = 30f + 25f                           * (_talon.GetSpell("TalonRake").CastInfo.SpellLevel - 1) +
                  _talon.Stats.AttackDamage.FlatBonus * _spell.SpellData.Coefficient;
        var variables      = new BuffVariables();
        variables.Set("slowAmount", 0.2f);
        AddBuff("Slow", 2f, 1, _spell, unit, _talon, buffVariables: variables);
        AddParticleTarget(_talon, unit, "talon_w_tar", unit);
        unit.TakeDamage(_talon, dmg, DamageType.DAMAGE_TYPE_PHYSICAL, DamageSource.DAMAGE_SOURCE_SPELLAOE,
                        DamageResultType.RESULT_NORMAL);
        
        AddBuff("TalonRakeReturn", 0.3f, 1, _spell, unit, _talon);
    }

    private void OnSpellHit(Spell spell, AttackableUnit target, SpellMissile missile, SpellSector sector) {
        if (target != _talon) return;
        _missile.SetToRemove();
        _isFinished = true;
        ApiEventManager.OnSpellHit.RemoveListener(this, spell, OnSpellHit);
    }
}