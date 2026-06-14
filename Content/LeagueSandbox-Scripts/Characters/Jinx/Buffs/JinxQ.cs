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
            PersistsThroughDeath = true,
        BuffType    = BuffType.AURA,
        BuffAddType = BuffAddType.REPLACE_EXISTING
    };

    public StatsModifier StatsModifier { get; } = new();

    public void OnActivate(AttackableUnit unit, Buff buff, Spell ownerSpell) {
        _jinx  = ownerSpell.CastInfo.Owner;
        _spell = ownerSpell;
        _buff  = buff;
        // Switch the Q HUD icon to Fishbones (IconIndex 0 = InventoryIcon / Jinx_Q1.dds;
        _spell.ChangeSpellData(ChangeSlotSpellDataType.IconIndex, newIconIndex: 0);
        
        // SetAutoAttackSpells with Crit and BasicAttack variants
        _jinx.SetAutoAttackSpellsWithCrit(true, CritAttackSpell, AttackSpell1, AttackSpell2);
        
        // Set correct animation states for Jinx with Fishbones
        _jinx.SetAnimStates(new Dictionary<string, string> {
            { "RUN", "R_RUN" },
            { "RUN2", "R_RUN2" },
            { "RUN_FAST", "R_RUN_FAST" },
            { "IDLE1", "R_IDLE1" },
            { "IDLE2", "R_IDLE2" },
            { "IDLE3", "R_IDLE3" },
            { "DEATH", "R_DEATH" },
            { "ATTACK1", "R_ATTACK1" },
            { "ATTACK2", "R_ATTACK2" },
            { "SPELL1", "R_SPELL1" },
            { "SPELL2", "R_SPELL2" },
            { "SPELL3", "R_SPELL3" },
            { "SPELL3_RUN", "R_SPELL3_RUN" },
            { "SPELL4", "R_SPELL4" },
            { "TAUNT", "R_TAUNT" },
            { "JOKE", "R_JOKE" },
            { "LAUGH", "R_LAUGH" }
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
        // Rocket impact FX — Riot spawns this server-side on every hit (replay: Jinx_Q_Rocket_tar.troy via
        // FX_Create_Group, 220/221 hits). AA-override hit FX lives in the script (OnHitUnit), not the engine
        // HitEffect path, which is gated off for auto-attacks (matches Kayle/Ashe/Jax/Malphite convention).
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
