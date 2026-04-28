using System;
using System.Numerics;
using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using GameServerLib.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.StatsNS;
using LeagueSandbox.GameServer.Logging;
using LeagueSandbox.GameServer.Scripting.CSharp;
using log4net.Repository.Hierarchy;
using Spells;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace Buffs;

public class JaxEvasion : IBuffGameScript {
    private       ObjAIBase              _jax;
    private       Spell                  _spell;
    private       Buff                   _buff;
    private       Particle               _p1, _p2;
    private       float                  _attacksDodged = 0f;
    private       JaxCounterStrikeAttack _counterAttack;
    private       PeriodicTicker         _periodicTicker;
    private       short                  _step    = 0;
    private const float                  MaxTicks = 1000f;
    private const short                  MaxSteps = 1;

    public BuffScriptMetaData BuffMetaData { get; set; } = new() {
        BuffType    = BuffType.COMBAT_ENCHANCER,
        BuffAddType = BuffAddType.REPLACE_EXISTING,
        MaxStacks   = 1
    };

    public StatsModifier StatsModifier { get; } = new();

    public void OnActivate(AttackableUnit unit, Buff buff, Spell ownerspell) {
        _buff          = buff;
        _jax           = ownerspell.CastInfo.Owner;
        _spell         = ownerspell;
        _step          = 0;
        _periodicTicker.Reset();
        _attacksDodged = 0f;
        SealSpellSlot(_jax, SpellSlotType.SpellSlots, 2, SpellbookType.SPELLBOOK_CHAMPION, true);
        ownerspell.SetCooldown(0f);
        ApiEventManager.OnPreTakeDamage.AddListener(this, _jax, OnPreTakeDamage);
        _p1 = AddParticleTarget(_jax, _jax, "JaxDodger", _jax, 1.6f);
        _p2 = AddParticleTarget(_jax, _jax, "CounterStrike_ready", _jax, 1.9f);
    }
    
    public void OnUpdate(float diff) {
        if (_step >= MaxSteps) return;

        var ticks = _periodicTicker.ConsumeTicks(diff, MaxTicks, maxTicksPerUpdate: 1);
        if (ticks == 0) return;

        SealSpellSlot(_jax, SpellSlotType.SpellSlots, 2, SpellbookType.SPELLBOOK_CHAMPION, false);
        _step += (short) ticks;
    }

    private void OnPreTakeDamage(DamageData data) {
        if (!IsValidTarget(_jax, data.Target,
                           SpellDataFlags.AffectEnemies | SpellDataFlags.AffectHeroes |
                           SpellDataFlags.AffectMinions | SpellDataFlags.AffectNeutral)) return;
        switch (data.DamageSource) {
            case DamageSource.DAMAGE_SOURCE_ATTACK or DamageSource.DAMAGE_SOURCE_PROC:
                data.DamageResultType     = DamageResultType.RESULT_DODGE;
                data.PostMitigationDamage = 0f;
                AddParticleTarget(_jax, _jax, "CounterStrike_dodged", _jax);
                _attacksDodged += 0.2f;
                break;
            case DamageSource.DAMAGE_SOURCE_SPELLAOE:
                data.PostMitigationDamage -= data.PostMitigationDamage * 0.25f;
                AddParticleTarget(_jax, _jax, "CounterStrike_dodged", _jax);
                break;
        }
    }

    public void OnDeactivate(AttackableUnit unit, Buff buff, Spell spell) {
        ApiEventManager.RemoveAllListenersForOwner(this);
        RemoveParticle(_p1);
        RemoveParticle(_p2);
        SpellCast(_jax, 3, SpellSlotType.ExtraSlots, false, _jax, Vector2.Zero);
        var counterAttackSpell = _jax.GetSpell("JaxCounterStrikeAttack");
        _counterAttack = counterAttackSpell.Script as JaxCounterStrikeAttack;
        _counterAttack?.SetAttacksDodged(_attacksDodged);
        spell.SetCooldown(spell.CastInfo.Cooldown, false);
    }
}
