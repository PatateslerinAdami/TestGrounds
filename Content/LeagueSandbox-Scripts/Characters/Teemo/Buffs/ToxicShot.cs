using System.Threading;
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

public class ToxicShot : IBuffGameScript
{
    private ObjAIBase _teemo;
    private Spell _spell;

    public BuffScriptMetaData BuffMetaData { get; set; } = new()
    {
        BuffType = BuffType.AURA,
        BuffAddType = BuffAddType.REPLACE_EXISTING,
        MaxStacks = 1
    };

    public StatsModifier StatsModifier { get; } = new();

    public void OnActivate(AttackableUnit unit, Buff buff, Spell ownerSpell)
    {
        _teemo = ownerSpell.CastInfo.Owner;
        _spell = ownerSpell;
        SealSpellSlot(_teemo, SpellSlotType.SpellSlots, 2, SpellbookType.SPELLBOOK_CHAMPION, true);
        ownerSpell.SetSpellToggle(true);
        _teemo.SetAutoAttackSpell("ToxicShotAttack", false);
        ApiEventManager.OnHitUnit.AddListener(this, _teemo, TargetExecute);
    }

    private void TargetExecute(DamageData data)
    {
        if (!IsValidTarget(_teemo, data.Target,
                SpellDataFlags.AffectEnemies | SpellDataFlags.AffectHeroes |
                SpellDataFlags.AffectMinions | SpellDataFlags.AffectNeutral)) return;
        var ap = _teemo.Stats.AbilityPower.Total * 0.3f;
        var dmg = 10f + 10f * (_spell.CastInfo.SpellLevel - 1) + ap;
        data.Target.TakeDamage(data.Attacker, dmg, DamageType.DAMAGE_TYPE_MAGICAL, DamageSource.DAMAGE_SOURCE_PROC,
            DamageResultType.RESULT_NORMAL);
        AddBuff("ToxicShotParticle", 4f, 1, _spell, data.Target, _teemo);
    }

    public void OnDeactivate(AttackableUnit unit, Buff buff, Spell ownerSpell)
    {
        _teemo.ResetAutoAttackSpell();
    }
}