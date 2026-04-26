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

namespace Spells;

public class JaxRelentlessAssault : ISpellScript {
    private ObjAIBase      _jax;
    private Spell          _spell;
    private AttackableUnit _target;

    public SpellScriptMetadata ScriptMetadata => new() {
        TriggersSpellCasts   = true,
        CastingBreaksStealth = true
    };

    public void OnActivate(ObjAIBase owner, Spell spell) {
        _jax   = owner;
        _spell = spell;
        ApiEventManager.OnUpdateStats.AddListener(this, _jax, OnUpdateStats);
        ApiEventManager.OnLevelUpSpell.AddListener(this, spell, OnLevelUpSpell);
    }

    public void OnSpellPreCast(ObjAIBase owner, Spell spell, AttackableUnit target, Vector2 start, Vector2 end) {
        _target = target;
    }

    public void OnSpellCast(Spell spell) { }

    public void OnSpellPostCast(Spell spell) { AddBuff("JaxRelentlessAssault", 8f, 1, spell, _jax, _jax); }

    private void OnLevelUpSpell(Spell spell) {
        if (spell.CastInfo.SpellLevel != 1) return;
        ApiEventManager.OnHitUnit.AddListener(this, _jax, OnHit);
        ApiEventManager.OnLevelUpSpell.RemoveListener(this);
    }

    private void OnHit(DamageData data) {
        if (!IsValidTarget(_jax, data.Target,
                           SpellDataFlags.AffectEnemies | SpellDataFlags.AffectHeroes |
                           SpellDataFlags.AffectMinions | SpellDataFlags.AffectNeutral)) return;

        var relentlessAttackBuff = _jax.GetBuffWithName("JaxRelentlessAttack");
        if (relentlessAttackBuff != null) {
            var ap  = _jax.Stats.AbilityPower.Total * _spell.SpellData.Coefficient;
            var dmg = 100f + 60f * (_spell.CastInfo.SpellLevel - 1) + ap;
            AddParticleTarget(_jax, data.Target, "RelentlessAssault_tar", data.Target);
            data.Target.TakeDamage(_jax, dmg, DamageType.DAMAGE_TYPE_MAGICAL, DamageSource.DAMAGE_SOURCE_PROC,
                                   DamageResultType.RESULT_NORMAL);

            RemoveBuff(relentlessAttackBuff);
            RemoveBuff(_jax, "JaxPassive");
            return;
        }

        AddBuff("JaxPassive", 2.5f, 1, _spell, _jax, _jax);
        if (_jax.GetBuffsWithName("JaxPassive").Count >= 2)
            AddBuff("JaxRelentlessAttack", 2.5f, 1, _spell, _jax, _jax);
    }

    private void OnUpdateStats(AttackableUnit owner, float diff) {
        var armorPerBonusAd       = _jax.Stats.AttackDamage.FlatBonus * 0.3f;
        var magicResistPerBonusAp = _jax.Stats.AbilityPower.Total     * 0.2f;
        var bonusArmor            = 25f + 10f * (_spell.CastInfo.SpellLevel - 1) + armorPerBonusAd;
        var bonusMagicResist      = 25f + 10f * (_spell.CastInfo.SpellLevel - 1) + magicResistPerBonusAp;
        SetSpellToolTipVar(_jax, 0, bonusArmor,       SpellbookType.SPELLBOOK_CHAMPION, 3, SpellSlotType.SpellSlots);
        SetSpellToolTipVar(_jax, 1, bonusMagicResist, SpellbookType.SPELLBOOK_CHAMPION, 3, SpellSlotType.SpellSlots);
    }
}

public class JaxRelentlessAttack : ISpellScript {
    public SpellScriptMetadata ScriptMetadata => new() {
        TriggersSpellCasts = false,
        IsDamagingSpell    = true
    };
}
