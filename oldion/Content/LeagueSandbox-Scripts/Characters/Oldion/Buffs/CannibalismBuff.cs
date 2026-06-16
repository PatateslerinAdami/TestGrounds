using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using LeagueSandbox.GameServer.GameObjects;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.StatsNS;
using LeagueSandbox.GameServer.Scripting.CSharp;

namespace Buffs;

public class CannibalismBuff : IBuffGameScript
{
    public BuffScriptMetaData BuffMetaData { get; set; } = new()
    {
        BuffType = BuffType.COMBAT_ENCHANCER,
        BuffAddType = BuffAddType.REPLACE_EXISTING,
        MaxStacks = 1,
    };

    public StatsModifier StatsModifier { get; } = new();

    public void OnActivate(AttackableUnit unit, Buff buff, Spell ownerSpell)
    {
        int level = ownerSpell.CastInfo.SpellLevel;
        float[] lifesteal = { 0, 0.50f, 0.75f, 1.00f };
        float ls = lifesteal[Math.Clamp(level - 1, 0, 2)];
        StatsModifier.LifeSteal.PercentBonus = ls;
        StatsModifier.AttackSpeed.PercentBonus = 0.50f;
        unit.AddStatModifier(StatsModifier);
    }

    public void OnDeactivate(AttackableUnit unit, Buff buff, Spell ownerSpell)
    {
        unit.RemoveStatModifier(StatsModifier);
    }

    public void OnUpdate(float diff) { }
}
