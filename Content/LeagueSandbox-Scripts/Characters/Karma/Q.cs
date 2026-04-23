using System.Linq;
using System.Numerics;
using Buffs;
using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.SpellNS.Missile;
using LeagueSandbox.GameServer.GameObjects.SpellNS.Sector;
using LeagueSandbox.GameServer.Logging;
using LeagueSandbox.GameServer.Scripting.CSharp;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace Spells;

public class KarmaQ : ISpellScript {
    private ObjAIBase _karma;
    private Vector2   _targetPosition;

    public SpellScriptMetadata ScriptMetadata { get; } = new() {
        TriggersSpellCasts = true,
    };

    public void OnActivate(ObjAIBase owner, Spell spell) {
        _karma = owner; 
        ApiEventManager.OnUpdateStats.AddListener(this, _karma, OnUpdateStats);
    }

    public void OnSpellPreCast(ObjAIBase owner, Spell spell, AttackableUnit target, Vector2 start, Vector2 end) {
        _targetPosition = end;
    }

    public void OnSpellPostCast(Spell spell) {
        SpellCast(_karma, _karma.HasBuff("KarmaMantra") ? 3 : 2, SpellSlotType.ExtraSlots, _targetPosition, _targetPosition,
                  true,   Vector2.Zero);
        //2 is QMissile
        //3 is QMissileMantra
    }

    private void OnUpdateStats(AttackableUnit target, float diff) {
        var bonusDmg = 25f + 50f * (_karma.GetSpell("KarmaMantra").CastInfo.SpellLevel - 1);
        SetSpellToolTipVar(_karma, 0, bonusDmg, SpellbookType.SPELLBOOK_CHAMPION, 0, SpellSlotType.SpellSlots);
    }
}

public class KarmaQMissile : ISpellScript {
    private ObjAIBase _karma;

    public SpellScriptMetadata ScriptMetadata { get; } = new() {
        MissileParameters = new MissileParameters() {
            Type = MissileType.Circle
        },
        TriggersSpellCasts = true,
    };

    public void OnActivate(ObjAIBase owner, Spell spell) {
        _karma = owner;
        ApiEventManager.OnSpellHit.AddListener(this, spell, OnSpellHit);
    }

    public void OnSpellPostCast(Spell spell) { }

    public void OnSpellHit(Spell spell, AttackableUnit target, SpellMissile missile, SpellSector sector) {
        if (!IsValidTarget(_karma, target, SpellDataFlags.AffectMinions | SpellDataFlags.AffectHeroes | SpellDataFlags.AffectMinions | SpellDataFlags.AffectNeutral)) return;
            var ap       = _karma.Stats.AbilityPower.Total * 0.6f;
            var dmg      = 80 + 45f * (_karma.GetSpell("KarmaQ").CastInfo.SpellLevel -1) + ap;

            AddParticlePos(_karma, "Karma_Base_Q_impact", target.Position, target.Position);
            var units = GetUnitsInRange(_karma, target.Position, 280f, true,
                                        SpellDataFlags.AffectEnemies | SpellDataFlags.AffectHeroes |
                                        SpellDataFlags.AffectMinions | SpellDataFlags.AffectNeutral);
            foreach (var unit in units) {
                AddParticleTarget(_karma, unit , "Karma_Base_Q_unit_tar", unit);
                AddBuff("KarmaQMissileSlow", 1.5f, 1, _karma.GetSpell("KarmaQ"), unit, _karma);
                unit.TakeDamage(_karma, dmg, DamageType.DAMAGE_TYPE_MAGICAL, DamageSource.DAMAGE_SOURCE_SPELL,
                                  DamageResultType.RESULT_NORMAL);
                var reductionAmount = 0.5f + 0.25f * (_karma.Spells[3].CastInfo.SpellLevel -1);
                _karma.Spells[3].LowerCooldown(reductionAmount);
            }
            missile.SetToRemove();
    }
}

public class KarmaQMissileMantra : ISpellScript {
    private ObjAIBase _karma;
    private Vector2   _endPos;
    private float     _fieldTimer = 0f;
    private bool      _hasExploded = true;

    public SpellScriptMetadata ScriptMetadata { get; } = new() {
        MissileParameters = new MissileParameters() {
            Type = MissileType.Circle
        },
        TriggersSpellCasts = true,
    };

    public void OnActivate(ObjAIBase owner, Spell spell) {
        _karma = owner; 
        ApiEventManager.OnSpellHit.AddListener(this, spell, OnSpellHit);
        ApiEventManager.OnLaunchMissile.AddListener(this, spell, OnLaunchMissile);
    }

    public void OnSpellPreCast(ObjAIBase owner, Spell spell, AttackableUnit target, Vector2 start, Vector2 end) {
        _endPos = end;
    }

    public void OnSpellPostCast(Spell spell) { }

    private void OnLaunchMissile(Spell spell, SpellMissile missile) {
        ApiEventManager.OnSpellMissileEnd.AddListener(this, missile, OnMissileEnd);
    }
 
    private void OnSpellHit(Spell spell, AttackableUnit target, SpellMissile missile, SpellSector sector) {
        if (!IsValidTarget(_karma, target, SpellDataFlags.AffectEnemies | SpellDataFlags.AffectHeroes | SpellDataFlags.AffectMinions | SpellDataFlags.AffectNeutral)) return;
        _endPos      = target.Position;
        var bonusDmg = 25f + 50f * (_karma.GetSpell("KarmaMantra").CastInfo.SpellLevel -1);
        var bonusAp  = _karma.Stats.AbilityPower.Total * 0.3f;
        var ap       = _karma.Stats.AbilityPower.Total * 0.6f;
        var dmg      = 80 + 45f * (_karma.GetSpell("KarmaQ").CastInfo.SpellLevel -1) + ap + bonusDmg + bonusAp;

        AddParticlePos(_karma, "Karma_Base_Q_impact", target.Position, target.Position);
        var units = GetUnitsInRange(_karma, target.Position, 280f, true,
                                    SpellDataFlags.AffectEnemies | SpellDataFlags.AffectHeroes |
                                    SpellDataFlags.AffectMinions | SpellDataFlags.AffectNeutral);
        foreach (var unit in units) {
            AddParticleTarget(_karma, unit , "Karma_Base_Q_unit_tar", unit);
            unit.TakeDamage(_karma, dmg, DamageType.DAMAGE_TYPE_MAGICAL, DamageSource.DAMAGE_SOURCE_SPELL,
                            DamageResultType.RESULT_NORMAL);
            var reductionAmount = 0.5f + 0.25f * (_karma.Spells[3].CastInfo.SpellLevel -1);
            _karma.Spells[3].LowerCooldown(reductionAmount);
        }
        //AddParticlePos(_karma, "Karma_Base_Q_impact_R_02.troybin",      _endPos, _endPos, 1.5f);
        //_fieldTimer  = 0f;
        //_hasExploded = false;
        missile.SetToRemove();
    }

    private void OnMissileEnd(SpellMissile missile) {
        _endPos      = missile.Position;
        AddParticlePos(_karma, "Karma_Base_Q_impact_R_01",      _endPos, _endPos, 1.5f, enemyParticle: "Karma_Base_Q_impact_red_R_01");
        _fieldTimer  = 0f;
        _hasExploded = false;
    }

    public void OnUpdate(float diff) {
        if (_hasExploded) return;
        _fieldTimer += diff;
        if (_fieldTimer >= 1500f) {
            var ap  = _karma.Stats.AbilityPower.Total * 0.6f;
            var dmg = 50 + 100f * (_karma.GetSpell("KarmaQ").CastInfo.SpellLevel -1) + ap;
            AddParticlePos(_karma, "Karma_Base_Q_impact_R_02", _endPos, _endPos, 1.5f);
            var units = GetUnitsInRange(_karma, _endPos, 280f, true,
                            SpellDataFlags.AffectEnemies | SpellDataFlags.AffectHeroes |
                            SpellDataFlags.AffectMinions | SpellDataFlags.AffectNeutral);
            foreach (var unit in units) {
                AddParticleTarget(_karma, unit , "Karma_Base_Q_unit_tar", unit);
                unit.TakeDamage(_karma, dmg, DamageType.DAMAGE_TYPE_MAGICAL, DamageSource.DAMAGE_SOURCE_SPELL,
                                DamageResultType.RESULT_NORMAL);
                var reductionAmount = 0.5f + 0.25f * (_karma.Spells[3].CastInfo.SpellLevel -1);
                _karma.Spells[3].LowerCooldown(reductionAmount);
            }
            _hasExploded = true;
            return;
        }
        var remainingDuration = 1.5f - _fieldTimer / 1000f;
        if (remainingDuration <= 0f) return;
        var unitsInField = GetUnitsInRange(_karma, _endPos, 280f, true,
                        SpellDataFlags.AffectEnemies | SpellDataFlags.AffectHeroes |
                        SpellDataFlags.AffectMinions | SpellDataFlags.AffectNeutral);
        if (unitsInField.Count == 0) return;
        foreach (var unit in unitsInField.Where(unit => !unit.HasBuff("KarmaQMissileMantraSlow"))) {
            AddBuff("KarmaQMissileMantraSlow", remainingDuration, 1, _karma.GetSpell("KarmaQ"), unit, _karma);
        }
    }
}
