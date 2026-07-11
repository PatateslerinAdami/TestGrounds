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

public class JaxCounterStrike : IBuffGameScript {
    private       ObjAIBase              _jax;
    private       float                  _attacksDodged = 0f;

    public BuffScriptMetaData BuffMetaData { get; set; } = new() {
        BuffType    = BuffType.COMBAT_ENCHANCER,
        BuffAddType = BuffAddType.REPLACE_EXISTING,
        MaxStacks   = 1
    };

    public StatsModifier StatsModifier { get; } = new();

    public void OnActivate(AttackableUnit unit, Buff buff, Spell ownerspell) {
        _jax           = buff.SourceUnit;
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
        ApiEventManager.OnDodge.AddListener(this, _jax, OnDodge);
        ApiEventManager.OnPreTakeDamage.AddListener(this, _jax, OnPreTakeDamage);
    }

    private void OnDodge(AttackableUnit dodger, AttackableUnit attacker) {
        _attacksDodged += 0.2f;
    }
    
    public void OnUpdate(Buff buff, float diff) {
        ExecutePeriodically(buff.BuffVars, "counterStrikeSeal", 1000f, false, 1, () =>
        {
            SealSpellSlot(_jax, SpellSlotType.SpellSlots, 2, SpellbookType.SPELLBOOK_CHAMPION, false);
        });
    }

    private void OnPreTakeDamage(DamageData data) {
        if (!IsValidTarget(_jax, data.Attacker,
                           SpellDataFlags.AffectEnemies | SpellDataFlags.AffectHeroes |
                           SpellDataFlags.AffectMinions | SpellDataFlags.AffectNeutral)) return;
        switch (data.DamageSource) {
            case DamageSource.DAMAGE_SOURCE_SPELLAOE:
                data.PostMitigationDamage -= data.PostMitigationDamage * 0.25f;
                break;
        }
    }

    public void OnDeactivate(AttackableUnit unit, Buff buff, Spell spell) {
        ApiEventManager.RemoveAllListenersForOwner(this);
        _jax.CharVars.Set("attacksDodged", _attacksDodged);
        SpellCast(_jax, 3, SpellSlotType.ExtraSlots, true, _jax, Vector2.Zero, inheritVariablesFrom: spell.CastInfo);
        spell.SetCooldown(spell.CastInfo.Cooldown, false);
    }
}
