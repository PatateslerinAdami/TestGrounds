using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using GameServerLib.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.StatsNS;
using LeagueSandbox.GameServer.Scripting.CSharp;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace Buffs;

// Fishbones toggle buff
internal class JinxQ : IBuffGameScript {
    private const    string    AttackSpell1 = "JinxQAttack";
    private const    string    AttackSpell2 = "JinxQAttack2";
    private const    string    CritAttackSpell = "JinxQCritAttack";
    private          ObjAIBase _jinx;
    private          Spell     _spell;
    private          Buff      _buff;

    public BuffScriptMetaData BuffMetaData { get; set; } = new() {
        BuffType    = BuffType.AURA,
        BuffAddType = BuffAddType.REPLACE_EXISTING
    };

    public StatsModifier StatsModifier { get; } = new();

    public void OnActivate(AttackableUnit unit, Buff buff, Spell ownerSpell) {
        _jinx  = ownerSpell.CastInfo.Owner;
        _spell = ownerSpell;
        _buff  = buff;
        ownerSpell.SetSpellToggle(true);

        // SetAutoAttackSpells with Crit and BasicAttack variants
        _jinx.SetAutoAttackSpellsWithCrit(true, CritAttackSpell, AttackSpell1, AttackSpell2);
        
        // Set correct animation states for Jinx with Fishbones
        _jinx.SetAnimStates(new Dictionary<string, string> {
            { "idle1_base", "R_idle1_BASE" },
            { "idle2_base", "R_idle2_BASE" },
            { "idle3_base", "R_idle3_BASE" },
            { "idle1", "R_idle1" },
            { "run", "R_Run" },
            { "run_base", "R_Run_BASE" },
            { "attack1", "R_Attack1" },
            { "attack2", "R_Attack2" }
        });

        // Q rank range bonus: +75 then +25 per additional rank.
        StatsModifier.Range.FlatBonus = 75 + 25f * (ownerSpell.CastInfo.SpellLevel - 1);
        unit.AddStatModifier(StatsModifier);

        
        ApiEventManager.OnPreDealDamage.AddListener(this, _jinx, OnPreDealDamage);
        ApiEventManager.OnHitUnit.AddListener(this, _jinx, OnHit);
    }

    public void OnDeactivate(AttackableUnit unit, Buff buff, Spell ownerSpell) {
        ApiEventManager.RemoveAllListenersForOwner(this);
    }

    private void OnPreDealDamage(DamageData data) {
        if (data.DamageSource != DamageSource.DAMAGE_SOURCE_ATTACK) return;
        // +10% physical damage to the primary target.
        data.PostMitigationDamage =
            data.Target.Stats.GetPostMitigationDamage(data.Damage + data.Damage * 0.1f, DamageType.DAMAGE_TYPE_PHYSICAL,
                                                      _jinx);
    }

    private void OnHit(DamageData data) {
        if (_jinx.Stats.CurrentMana < 20f) {
            // When out of mana: set minigun and drop Q by removing this buff.
            AddBuff("JinxQIcon", 25000f, 1, _spell, _jinx, _jinx, true);
            _buff.DeactivateBuff();
            return;
        }

        // Fishbones mana cost per Rocket.
        _jinx.Stats.CurrentMana -= 20f;
        AddParticleTarget(_jinx, data.Target, "Jinx_Q_Rocket_tar.troy", data.Target);

        // Splash damage around the primary target.
        var units = GetUnitsInRange(_jinx, data.Target.Position, 250f, true,
                                    SpellDataFlags.AffectEnemies | SpellDataFlags.AffectHeroes |
                                    SpellDataFlags.AffectMinions | SpellDataFlags.AffectNeutral)
            .Where(unit => unit != data.Target);
        foreach (var unit in units) {
            unit.TakeDamage(_jinx, data.Damage + data.Damage * 0.1f, DamageType.DAMAGE_TYPE_PHYSICAL,
                            DamageSource.DAMAGE_SOURCE_PROC, DamageResultType.RESULT_NORMAL);
        }
    }
}
