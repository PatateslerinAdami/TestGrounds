using System.Linq;
using System.Numerics;
using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.SpellNS.Missile;
using LeagueSandbox.GameServer.GameObjects.SpellNS.Sector;
using LeagueSandbox.GameServer.GameObjects.StatsNS;
using LeagueSandbox.GameServer.Scripting.CSharp;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace Spells;

public class spellflux : ISpellScript {
    private ObjAIBase _ryze;

    public SpellScriptMetadata ScriptMetadata => new() {
        TriggersSpellCasts = true,
        IsDamagingSpell    = true,
        NotSingleTargetSpell = true,
        DoesntBreakShields = false,
        MissileParameters = new MissileParameters {
            Type = MissileType.Target
        },
    };

    public void OnActivate(ObjAIBase owner, Spell spell) {
        _ryze = owner;
        ApiEventManager.OnUpdateStats.AddListener(this, _ryze, OnUpdateStats);
        ApiEventManager.OnSpellHit.AddListener(this, spell, OnSpellHit);
    }

    private void OnSpellHit(Spell spell, AttackableUnit target, SpellMissile missile, SpellSector sector) {
        var ap   = _ryze.Stats.AbilityPower.Total * spell.SpellData.Coefficient;
        var mana = _ryze.Stats.ManaPoints.Total   * 0.01f;
        var dmg  = 50f + 20f * (spell.CastInfo.SpellLevel - 1) + ap + mana;

        AddParticleTarget(_ryze, target, "SpellFlux_tar2", target);
        target.TakeDamage(_ryze, dmg, DamageType.DAMAGE_TYPE_MAGICAL, DamageSource.DAMAGE_SOURCE_SPELLAOE,
                          DamageResultType.RESULT_NORMAL);
        AddBuff("SpellFlux", 5f, 1, spell, target, _ryze);
        if (_ryze.HasBuff("DesperatePower")) {
            AddParticle(_ryze, target, "DesperatePower_aoe.troy", target.Position);
            var unitsInRange = GetUnitsInRange(_ryze, target.Position, 200f, true,
                                               SpellDataFlags.AffectEnemies | SpellDataFlags.AffectHeroes |
                                               SpellDataFlags.AffectMinions |
                                               SpellDataFlags.AffectNeutral).Where(unit => unit != target);
            foreach (var unit in unitsInRange) {
                AddParticleTarget(_ryze, unit, "ManaLeach_tar", unit);
                unit.TakeDamage(_ryze, dmg * 0.5f, DamageType.DAMAGE_TYPE_MAGICAL, DamageSource.DAMAGE_SOURCE_SPELLAOE,
                                DamageResultType.RESULT_NORMAL);
                AddBuff("SpellFlux", 5f, 1, spell, target, _ryze);
            }
        }

        var unitInRange = GetUnitsInRange(_ryze, target.Position, 375f, true,
                                          SpellDataFlags.AffectEnemies | SpellDataFlags.AffectHeroes |
                                          SpellDataFlags.AffectMinions |
                                          SpellDataFlags.AffectNeutral | SpellDataFlags.AlwaysSelf).Where(unit => unit != target)
                          .OrderBy(x => Vector2.Distance(target.Position, x.Position)).FirstOrDefault();
        if (unitInRange == null) return;
        SpellCast(_ryze, 1, SpellSlotType.ExtraSlots, true, unitInRange, target.Position);
    }

    private void OnUpdateStats(AttackableUnit unit, float diff) {
        var mana = _ryze.Stats.ManaPoints.Total * 0.01f;
        SetSpellToolTipVar(unit, 0, mana, SpellbookType.SPELLBOOK_CHAMPION, 2, SpellSlotType.SpellSlots);
    }
}

public class spellfluxmissile : ISpellScript {
    private ObjAIBase     _ryze;

    public SpellScriptMetadata ScriptMetadata => new() {
        TriggersSpellCasts = true,
        IsDamagingSpell    = true,
        MissileParameters = new MissileParameters {
            Type                          = MissileType.Chained,
            MaximumHits                   = 5,
            CanHitSameTarget = true
        }
    };

    public void OnActivate(ObjAIBase owner, Spell spell) {
        _ryze = owner;
        ApiEventManager.OnSpellHit.AddListener(this, spell, TargetExecute);
    }

    private void TargetExecute(Spell spell, AttackableUnit target, SpellMissile missile, SpellSector sector) {
        if (target == _ryze) return;
        var ap   = _ryze.Stats.AbilityPower.Total * spell.SpellData.Coefficient;
        var mana = _ryze.Stats.ManaPoints.Total   * 0.01f;
        var dmg  = 50f + 20f * (_ryze.GetSpell("spellflux").CastInfo.SpellLevel - 1) + ap + mana;
        AddParticleTarget(_ryze, target, "SpellFlux_tar2", target);
        target.TakeDamage(_ryze, dmg, DamageType.DAMAGE_TYPE_MAGICAL, DamageSource.DAMAGE_SOURCE_SPELLAOE,
                          DamageResultType.RESULT_NORMAL);
        AddBuff("SpellFlux", 5f, 1, spell, target, _ryze);
        if (!_ryze.HasBuff("DesperatePower")) return;
        AddParticle(_ryze, target, "DesperatePower_aoe.troy", target.Position);
        var unitsInRange = GetUnitsInRange(_ryze, target.Position, 200f, true,
                                           SpellDataFlags.AffectEnemies | SpellDataFlags.AffectHeroes |
                                           SpellDataFlags.AffectMinions |
                                           SpellDataFlags.AffectNeutral).Where(unit => unit != target);
        foreach (var unit in unitsInRange) {
            AddParticleTarget(_ryze, unit, "ManaLeach_tar", unit);
            unit.TakeDamage(_ryze, dmg * 0.5f, DamageType.DAMAGE_TYPE_MAGICAL, DamageSource.DAMAGE_SOURCE_SPELLAOE,
                            DamageResultType.RESULT_NORMAL);
            AddBuff("SpellFlux", 5f, 1, spell, target, _ryze);
        }
    }
}