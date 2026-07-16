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

namespace Spells;

public class JaxRelentlessAssault : ISpellScript
{
    private ObjAIBase _jax;
    private Spell _spell;
    private int _hitCount = 1;

    public SpellScriptMetadata ScriptMetadata => new()
    {
        TriggersSpellCasts = true,
        CastingBreaksStealth = true
    };

    public void OnActivate(ObjAIBase owner, Spell spell)
    {
        _jax = owner;
        _spell = spell;
        ApiEventManager.OnUpdateStats.AddListener(this, _jax, OnUpdateStats);
        ApiEventManager.OnLevelUpSpell.AddListener(this, spell, OnLevelUpSpell);
    }

    private void OnLevelUpSpell(Spell spell)
    {
        _spell = spell;
        if (spell.CastInfo.SpellLevel == 1)
        {
            ApiEventManager.OnHitUnit.AddListener(this, _jax, OnHit);
        }
    }

    private void OnHit(DamageData data)
    {
        switch (_hitCount)
        {
            case 2:
            {
                if (!_jax.HasBuff("JaxEmpowerTwo"))
                {
                    OverrideAutoAttack(_jax, "JaxRelentlessAttack", false);
                }

                break;
            }
            case 3:
            {
                if (!IsValidTarget(_jax, data.Target,
                        SpellDataFlags.AffectEnemies | SpellDataFlags.AffectHeroes |
                        SpellDataFlags.AffectMinions | SpellDataFlags.AffectNeutral)) return;


                SpellEffectCreate("RelentlessAssault_tar.troy", _jax, data.Target, data.Target,
                    flags: FXFlags.SimulateWhileOffScreen, keywordObject: _jax);

                var rSpell = _jax.Spells[3];
                var ap = _jax.Stats.AbilityPower.Total * _spell.SpellData.Coefficient;
                var dmg = rSpell.SpellData.EffectLevelAmount[1][rSpell.CastInfo.SpellLevel] + ap;
                data.Target.TakeDamage(_jax, dmg, DamageType.DAMAGE_TYPE_MAGICAL, DamageSource.DAMAGE_SOURCE_PROC,
                    DamageResultType.RESULT_NORMAL);
                if (!_jax.HasBuff("JaxEmpowerTwo"))
                {
                    RemoveOverrideAutoAttack(_jax);
                }

                _hitCount = 0;
                break;
            }
        }

        _hitCount++;
    }

    public void OnSpellPostCast(Spell spell)
    {
        AddBuff("JaxRelentlessAssaultSpeed", 8f, 1, spell, _jax, _jax);
    }

    private void OnUpdateStats(AttackableUnit owner, float diff)
    {
        var armorPerBonusAd = _jax.Stats.AttackDamage.FlatBonus * 0.3f;
        var magicResistPerBonusAp = _jax.Stats.AbilityPower.Total * 0.2f;
        var bonusArmor = _spell.SpellData.EffectLevelAmount[3][_spell.CastInfo.SpellLevel] + armorPerBonusAd;
        var bonusMagicResist = _spell.SpellData.EffectLevelAmount[3][_spell.CastInfo.SpellLevel] + magicResistPerBonusAp;
        SetSpellToolTipVar(_jax, 1, bonusArmor, SpellbookType.SPELLBOOK_CHAMPION, 3, SpellSlotType.SpellSlots);
        SetSpellToolTipVar(_jax, 0, bonusMagicResist, SpellbookType.SPELLBOOK_CHAMPION, 3, SpellSlotType.SpellSlots);
    }
}

public class JaxRelentlessAttack : ISpellScript
{
    public SpellScriptMetadata ScriptMetadata => new()
    {
    };
}