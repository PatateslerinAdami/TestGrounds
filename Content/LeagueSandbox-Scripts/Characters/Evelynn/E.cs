using System.Linq;
using System.Numerics;
using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.Scripting.CSharp;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace Spells;

public class EvelynnE : ISpellScript {
    private const float SecondaryHitRange = 225f;
    private const SpellDataFlags RavageTargetFlags = SpellDataFlags.AffectEnemies
                                                     | SpellDataFlags.AffectHeroes
                                                     | SpellDataFlags.AffectMinions
                                                     | SpellDataFlags.AffectNeutral;

    private ObjAIBase _evelynn;
    private AttackableUnit _target;
    private bool _hadVisionAtCastStart;
    private bool _firstHitKilledPrimaryTarget;

    public SpellScriptMetadata ScriptMetadata { get; } = new() {
        TriggersSpellCasts = true
    };

    public void OnActivate(ObjAIBase owner, Spell spell) {
        _evelynn        = owner;
        ApiEventManager.OnUpdateStats.AddListener(this, _evelynn, OnUpdateStats);
    }

    public void OnSpellPreCast(ObjAIBase owner, Spell spell, AttackableUnit target, Vector2 start, Vector2 end) {
        _target = target;
        _hadVisionAtCastStart        = _target != null && TeamHasVision(_evelynn.Team, _target);
        _firstHitKilledPrimaryTarget = false;

        if (_target == null || _target.IsDead) {
            return;
        }

        // First slash.
        DealRavageHit(_target, spell);
        _firstHitKilledPrimaryTarget = _target.IsDead;
    }

    public void OnSpellPostCast(Spell spell) {
        if (_target == null) {
            return;
        }

        if (_target.IsDead) {
            if (_firstHitKilledPrimaryTarget) {
                // First slash killed the target, second slash seeks another nearby target.
                var secondaryTarget = FindSecondaryTarget(_target);
                if (secondaryTarget != null) {
                    DealRavageHit(secondaryTarget, spell);
                }
                AddBuff("EvelynnE", 3f, 1, spell, _evelynn, _evelynn);
            } else {
                // Target died during cast -> no second hit, refund cooldown, still grant AS buff.
                spell.SetCooldown(0f, true);
                AddBuff("EvelynnE", 3f, 1, spell, _evelynn, _evelynn);
            }

            _target = null;
            return;
        }

        if (_hadVisionAtCastStart && !TeamHasVision(_evelynn.Team, _target)) {
            // Lost sight during cast -> cancel second slash and refund cooldown.
            spell.SetCooldown(0f, true);
            _target = null;
            return;
        }

        // Second slash on the original target.
        DealRavageHit(_target, spell);
        AddBuff("EvelynnE", 3f, 1, spell, _evelynn, _evelynn);
        _target = null;
    }

    private void DealRavageHit(AttackableUnit hitTarget, Spell spell) {
        if (hitTarget == null || hitTarget.IsDead) {
            return;
        }

        var ad  = _evelynn.Stats.AttackDamage.FlatBonus * 0.5f;
        var ap  = _evelynn.Stats.AbilityPower.Total     * 0.5f;
        var dmg = 70f + 40f * (spell.CastInfo.SpellLevel - 1) + ap + ad;

        AddParticleTarget(_evelynn, hitTarget, "Evelynn_E_tar", hitTarget);
        hitTarget.TakeDamage(_evelynn, dmg, DamageType.DAMAGE_TYPE_PHYSICAL, DamageSource.DAMAGE_SOURCE_ATTACK, DamageResultType.RESULT_NORMAL);
        ApplyRavageOnHit(spell);
    }

    private void ApplyRavageOnHit(Spell spell) {
        if (_evelynn.GetSpell("EvelynnW").CastInfo.SpellLevel > 0) {
            AddBuff("EvelynnWPassive", 3f, 1, spell, _evelynn, _evelynn);
        }
    }

    private AttackableUnit FindSecondaryTarget(AttackableUnit primaryTarget) {
        return GetUnitsInRange(_evelynn, _evelynn.Position, SecondaryHitRange, true, RavageTargetFlags)
            .Where(unit => unit != null
                           && unit != primaryTarget
                           && IsValidTarget(_evelynn, unit, RavageTargetFlags)
                           && TeamHasVision(_evelynn.Team, unit))
            .OrderBy(unit => Vector2.DistanceSquared(_evelynn.Position, unit.Position))
            .FirstOrDefault();
    }

    private void OnUpdateStats(AttackableUnit target, float diff) {
        SetSpellToolTipVar(_evelynn, 0, _evelynn.Stats.AttackDamage.FlatBonus * 0.5f, SpellbookType.SPELLBOOK_CHAMPION, 2, SpellSlotType.SpellSlots);
    }
}
