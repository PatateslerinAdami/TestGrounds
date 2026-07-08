using AIScripts;
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

public class DianaPassive : IBuffGameScript
{
    private ObjAIBase _diana;
    private Buff _buff;

    public BuffScriptMetaData BuffMetaData { get; set; } = new()
    {
        BuffType = BuffType.COMBAT_ENCHANCER,
        BuffAddType = BuffAddType.REPLACE_EXISTING,
        MaxStacks = 1,
        IsHidden = true
    };

    public StatsModifier StatsModifier { get; } = new();

    public void OnActivate(AttackableUnit unit, Buff buff, Spell ownerSpell)
    {
        _diana = buff.SourceUnit;
        _buff = buff;
        ApiEventManager.OnHitUnit.AddListener(this, _diana, OnHit);
        _diana.SetAutoAttackSpell("DianaBasicAttack3", false);
    }

    private void OnHit(DamageData data)
    {
        var ap = _diana.Stats.AbilityPower.Total * 0.8f;
        var dmg = 20f + 5 * (_diana.Stats.Level - 1) + ap;
        foreach (var unit in GetUnitsInCone(_diana, _diana.Position, data.Target.Position - _diana.Position, 175f, 180f,
                     true,
                     SpellDataFlags.AffectEnemies | SpellDataFlags.AffectNeutral | SpellDataFlags.AffectMinions |
                     SpellDataFlags.AffectHeroes))
        {
            unit.TakeDamage(_diana, dmg, DamageType.DAMAGE_TYPE_MAGICAL, DamageSource.DAMAGE_SOURCE_SPELLAOE,
                DamageResultType.RESULT_NORMAL);
        }
        RemoveBuff(_buff);
    }

    public void OnDeactivate(AttackableUnit unit, Buff buff, Spell ownerSpell)
    {
        _diana.ResetAutoAttackSpell();
    }
}