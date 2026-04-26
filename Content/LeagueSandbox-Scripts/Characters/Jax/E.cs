using System.Numerics;
using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using GameServerLib.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.SpellNS.Missile;
using LeagueSandbox.GameServer.GameObjects.SpellNS.Sector;
using LeagueSandbox.GameServer.Scripting.CSharp;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;
using System;

namespace Spells;

public class JaxCounterStrike : ISpellScript {
    private ObjAIBase      _jax;
    private Spell          _spell;
    private AttackableUnit _target;

    public SpellScriptMetadata ScriptMetadata => new() {
        TriggersSpellCasts   = true,
        CastingBreaksStealth = true
    };

    public void OnActivate(ObjAIBase owner, Spell spell) {
        _jax = owner;
        _spell = spell;
        ApiEventManager.OnUpdateStats.AddListener(this, _jax, OnUpdateStats);
    }

    public void OnSpellPreCast(ObjAIBase owner, Spell spell, AttackableUnit target, Vector2 start, Vector2 end) {
        _target = target;
        if (!_jax.HasBuff("JaxEvasion")) {
            PlayAnimation(_jax, "Spell3", 1.9f);
        }
    }

    public void OnSpellCast(Spell spell) {
    }

    public void OnSpellPostCast(Spell spell) {
        if (_jax.HasBuff("JaxEvasion")) {
            RemoveBuff(_jax, "JaxEvasion");
        } else {
            AddBuff("JaxEvasion", 2f, 1, spell, _jax, _jax);
        }
    }

    private void OnUpdateStats(AttackableUnit owner, float diff) {
        var value = 0f;
        SetSpellToolTipVar(_jax, 0, value, SpellbookType.SPELLBOOK_CHAMPION, 0, SpellSlotType.SpellSlots);
    }
}

public class JaxCounterStrikeAttack : ISpellScript {
    private ObjAIBase _jax; 
    Spell             _counterStrikeAttack;
    private float     _attacksDodged = 0f;
    public SpellScriptMetadata ScriptMetadata => new() {
        TriggersSpellCasts   = false,
        IsDamagingSpell = true
    };

    public void OnActivate(ObjAIBase owner, Spell spell) {
        _counterStrikeAttack = spell;
        _jax                 = owner;
    }

    public void OnSpellPreCast(ObjAIBase owner, Spell spell, AttackableUnit target, Vector2 start, Vector2 end) {
        PlayAnimation(_jax, "Spell3b", 0.5f);
        AddParticleTarget(_jax, _jax, "Counterstrike_cas", _jax, 1.5f);
        var enemiesInRange = GetUnitsInRange(_jax, _jax.Position, 375f, true,
                                             SpellDataFlags.AffectEnemies | SpellDataFlags.AffectHeroes |
                                             SpellDataFlags.AffectMinions | SpellDataFlags.AffectNeutral);
        var ad  = _jax.Stats.AttackDamage.FlatBonus * spell.SpellData.Coefficient;
        var dmg = 50f + 25f * (_jax.GetSpell("JaxCounterStrike").CastInfo.SpellLevel - 1) + ad;
        dmg += dmg * Math.Min(1f, _attacksDodged);
        foreach (var enemy in enemiesInRange) {
            AddParticleTarget(_jax, enemy, spell.SpellData.HitEffectName, _jax, bone: spell.SpellData.HitBoneName);
            AddBuff("Stun", 1f, 1, _jax.GetSpell("JaxCounterStrike"), enemy, _jax);
            enemy.TakeDamage(_jax, dmg, DamageType.DAMAGE_TYPE_MAGICAL, DamageSource.DAMAGE_SOURCE_SPELLAOE,
                             DamageResultType.RESULT_NORMAL);
        }
        SetAttacksDodged(0f);
    }
    
    internal void SetAttacksDodged(float amount) {
        _attacksDodged = amount;
    }
}