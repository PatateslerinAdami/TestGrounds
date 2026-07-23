using System.Numerics;
using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using GameServerLib.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.SpellNS.Missile;
using LeagueSandbox.GameServer.Scripting.CSharp;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;
using System;

namespace Spells;

public class JaxCounterStrike : ISpellScript
{
    private ObjAIBase _jax;
    private Spell _spell;

    public SpellScriptMetadata ScriptMetadata => new()
    {
        NotSingleTargetSpell = true,
        DoesntBreakShields = true,
        TriggersSpellCasts = true,
        CastingBreaksStealth = true,
        IsDamagingSpell = false
    };

    public void OnActivate(ObjAIBase owner, Spell spell)
    {
        _jax = owner;
        _spell = spell;
        ApiEventManager.OnUpdateStats.AddListener(this, _jax, OnUpdateStats);
    }

    public void OnSpellPreCast(ObjAIBase owner, Spell spell, AttackableUnit target, Vector2 start, Vector2 end)
    {
        if (!_jax.HasBuff("JaxCounterStrike"))
        {
            PlayAnimation(_jax, "Spell3", 2.25f, 0f, 1f,
                AnimationFlags.NoBlend | AnimationFlags.Junk6 | AnimationFlags.Junk7);
        }
    }

    public void OnSpellPostCast(Spell spell)
    {
        if (_jax.HasBuff("JaxCounterStrike"))
        {
            RemoveBuff(_jax, "JaxCounterStrike");
        }
        else
        {
            AddBuff("JaxCounterStrike", 2f, 1, spell, _jax, _jax);
        }
    }

    private void OnUpdateStats(AttackableUnit owner, float diff)
    {
        var ad = _jax.Stats.AttackDamage.FlatBonus * _spell.SpellData.Coefficient;
        SetSpellToolTipVar(_jax, 1, ad, SpellbookType.SPELLBOOK_CHAMPION, 2, SpellSlotType.SpellSlots);
    }
}

public class JaxCounterStrikeAttack : ISpellScript
{
    private ObjAIBase _jax;
    private Spell _counterStrikeAttack;

    public SpellScriptMetadata ScriptMetadata => new()
    {
        NotSingleTargetSpell = true,
        DoesntBreakShields = true,
        TriggersSpellCasts = false,
        IsDamagingSpell = true
    };

    public void OnActivate(ObjAIBase owner, Spell spell)
    {
        _counterStrikeAttack = spell;
        _jax = owner;
        ApiEventManager.OnSpellHit.AddListener(this, spell, OnSpellHit);
    }

    private void OnSpellHit(Spell spell, AttackableUnit target, SpellMissile missile)
    {
        var mainSpell = _jax.GetSpell("JaxCounterStrike");
        
        AddBuff("Stun", 1f, 1, mainSpell, target, _jax);
        
        
        var ad = _jax.Stats.AttackDamage.FlatBonus * mainSpell.SpellData.Coefficient;
        var dmg = mainSpell.SpellData.EffectLevelAmount[1][mainSpell.CastInfo.SpellLevel] + ad;
        var attacksDodged = _jax.CharVars.GetFloat("attacksDodged");
        dmg += dmg * Math.Min(1f, attacksDodged);
        target.TakeDamage(_jax, dmg, DamageType.DAMAGE_TYPE_PHYSICAL, DamageSource.DAMAGE_SOURCE_SPELLAOE,
            DamageResultType.RESULT_NORMAL);

        _jax.CharVars.Set("attacksDodged", 0f);
    }

    public void OnSpellPreCast(ObjAIBase owner, Spell spell, AttackableUnit target, Vector2 start, Vector2 end)
    {
        PlayAnimation(_jax, "Spell3b", 0f, 0f, 1f,
            AnimationFlags.NoBlend | AnimationFlags.Junk6 | AnimationFlags.Junk7);
        SpellEffectCreate("Counterstrike_cas.troy", _jax, _jax, _jax, flags: FXFlags.SimulateWhileOffScreen);
        
        var enemiesInRange = ForEachUnitInTargetArea(_jax, _jax.Position, 375f,
            SpellDataFlags.AffectEnemies | SpellDataFlags.AffectNeutral | SpellDataFlags.AffectMinions |
            SpellDataFlags.AffectHeroes);
        foreach (var enemy in enemiesInRange)
        {
            spell.ApplyEffects(enemy);
        }
    }
}