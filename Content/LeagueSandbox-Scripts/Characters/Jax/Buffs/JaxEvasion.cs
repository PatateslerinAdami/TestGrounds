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
        // Bonus fraction for the recast: 0 at start, +0.2 per dodged attack, capped at +100% (5 dodges)
        // by Math.Min(1f, ...) in E.cs. Must start at 0 so a recast with no dodges adds +0%.
        _attacksDodged = 0f;
        SealSpellSlot(_jax, SpellSlotType.SpellSlots, 2, SpellbookType.SPELLBOOK_CHAMPION, true);
        ownerspell.SetCooldown(0f);
        // Grant 100% dodge for the duration (Riot's mDodge=1.0 via JaxEvasion). Basic attacks against Jax
        // now resolve as HIT_Dodge engine-side (0 damage + client "Dodge!" text), instead of post-hoc
        // zeroing the damage. Each dodged attack fires OnDodge → we build Counter Strike's recast damage.
        StatsModifier.Dodge.FlatBonus = 1.0f;
        _jax.AddStatModifier(StatsModifier);
        ApiEventManager.OnDodge.AddListener(this, _jax, OnJaxDodge);
        ApiEventManager.OnPreTakeDamage.AddListener(this, _jax, OnPreTakeDamage);
        _p1 = AddParticleTarget(_jax, _jax, "JaxDodger", _jax, 1.6f);
        _p2 = AddParticleTarget(_jax, _jax, "CounterStrike_ready", _jax, 1.9f);
    }

    private void OnJaxDodge(AttackableUnit dodger, AttackableUnit attacker) {
        _attacksDodged += 0.2f;
        AddParticleTarget(_jax, _jax, "CounterStrike_dodged", _jax);
    }
    
    public void OnUpdate(float diff) {
        if (_step >= MaxSteps) return;

        var ticks = _periodicTicker.ConsumeTicks(diff, MaxTicks, maxTicksPerUpdate: 1);
        if (ticks == 0) return;

        SealSpellSlot(_jax, SpellSlotType.SpellSlots, 2, SpellbookType.SPELLBOOK_CHAMPION, false);
        _step += (short) ticks;
    }

    private void OnPreTakeDamage(DamageData data) {
        if (!IsValidTarget(_jax, data.Attacker,
                           SpellDataFlags.AffectEnemies | SpellDataFlags.AffectHeroes |
                           SpellDataFlags.AffectMinions | SpellDataFlags.AffectNeutral)) return;
        switch (data.DamageSource) {
            // Basic attacks (and their on-hit procs) are dodged engine-side now: a dodged/missed attack no
            // longer fires OnHitUnit, so no PROC leaks here to negate. Only the AoE reduction remains.
            case DamageSource.DAMAGE_SOURCE_SPELLAOE:
                data.PostMitigationDamage -= data.PostMitigationDamage * 0.25f;
                AddParticleTarget(_jax, _jax, "CounterStrike_dodged", _jax);
                break;
        }
    }

    public void OnDeactivate(AttackableUnit unit, Buff buff, Spell spell) {
        ApiEventManager.RemoveAllListenersForOwner(this);
        // NOTE: do NOT RemoveStatModifier here — Buff.DeactivateBuff auto-removes the script's StatsModifier
        // (Buff.cs:190). Removing it manually too was a double-remove → Dodge.FlatBonus went to -1.0, so the
        // next E cast's AddStatModifier only netted back to 0 (dodge worked once, then never again).
        RemoveParticle(_p1);
        RemoveParticle(_p2);
        // Key MUST match E.cs's GetFloat("attacksDodged") — was "attacksDodge" (typo) so the recast read 0.
        spell.CastInfo.Variables.Set("attacksDodged", _attacksDodged);
        SpellCast(_jax, 3, SpellSlotType.ExtraSlots, true, _jax, Vector2.Zero, inheritVariablesFrom: spell.CastInfo);
        spell.SetCooldown(spell.CastInfo.Cooldown, false);
    }
}
