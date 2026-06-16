using System;
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

/// <summary>
/// Soraka W — Astral Infusion (4.17 rework / 4.20).
/// Heals target ally at the cost of 10% of Soraka's max HP.
/// Cannot cast if Soraka is below 5% max HP.
/// </summary>
public class SorakaW : ISpellScript
{
    private const float HpCostPercent = 0.10f;
    private const float MinHpPercent = 0.05f;

    private ObjAIBase _soraka;
    private AttackableUnit _target;

    public SpellScriptMetadata ScriptMetadata => new()
    {
        TriggersSpellCasts = true,
        IsDamagingSpell = false,
        NotSingleTargetSpell = false,
    };

    public void OnActivate(ObjAIBase owner, Spell spell)
    {
        _soraka = owner;
    }

    public void OnDeactivate(ObjAIBase owner, Spell spell) { }

    public void OnSpellPreCast(ObjAIBase owner, Spell spell, AttackableUnit target, Vector2 start, Vector2 end)
    {
        _target = target;
    }

    public void OnSpellCast(Spell spell)
    {
        // Cast VFX on Soraka — hand glow
        AddParticleTarget(_soraka, _soraka, "soraka_base_w_eff.troy", _soraka, 1f);

        // Beam from Soraka to target ally
        if (_target != null)
        {
            AddParticlePos(_soraka, "Soraka_base_W_Beam.troy",
                _soraka.Position, _target.Position,
                lifetime: 0.8f, direction: _soraka.Direction);
        }
    }

    public void OnSpellPostCast(Spell spell)
    {
        if (_target == null || _target.IsDead) return;

        // Can't cast below 5% max HP
        float minHp = _soraka.Stats.HealthPoints.Total * MinHpPercent;
        if (_soraka.Stats.CurrentHealth <= minHp) return;

        // HP cost: 10% of max HP
        float maxHpCost = _soraka.Stats.HealthPoints.Total * HpCostPercent;
        float hpCost = Math.Min(maxHpCost, _soraka.Stats.CurrentHealth - 1f);
        if (hpCost <= 0) return;

        _soraka.TakeDamage(_soraka, hpCost, DamageType.DAMAGE_TYPE_TRUE,
            DamageSource.DAMAGE_SOURCE_SPELL, false);

        // Heal
        float rank = spell.CastInfo.SpellLevel;
        float ap = _soraka.Stats.AbilityPower.Total * 0.6f;
        float heal = rank switch
        {
            1 => 120f, 2 => 150f, 3 => 180f, 4 => 210f, 5 => 240f, _ => 120f
        } + ap;

        _target.TakeHeal(_soraka, heal, HealType.SelfHeal);

        // Heal VFX on ally
        AddParticleTarget(_soraka, _target, "Global_Heal.troy", _target, 1f);

        // Buff glow on ally
        AddParticleTarget(_soraka, _target, "soraka_base_w_buf.troy", _target, 1.5f);

        // Missile flash at ally position
        AddParticle(_soraka, null, "soraka_base_w_mis.troy", _target.Position, 0.5f);
    }

    public void OnUpdate(float diff) { }
}
