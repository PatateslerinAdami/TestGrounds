using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using LeagueSandbox.GameServer.GameObjects;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.StatsNS;
using LeagueSandbox.GameServer.Scripting.CSharp;

namespace Buffs;

internal class ItemInternalSlow : IBuffGameScript
{
    public BuffScriptMetaData BuffMetaData { get; set; } = new()
    {
        BuffType = BuffType.SLOW,
        BuffAddType = BuffAddType.STACKS_AND_OVERLAPS,
        MaxStacks = 10,
        IsHidden = true
    };

    public StatsModifier StatsModifier { get; } = new();

    public void OnActivate(AttackableUnit unit, Buff buff, Spell ownerSpell)
    {
        var slowPercent = buff.BuffVars.GetFloat("slowPercent", 0.15f);
        if (slowPercent < 0.0f)
        {
            slowPercent = -slowPercent;
        }

        StatsModifier.MoveSpeed.PercentBonus -= slowPercent;
        unit.AddStatModifier(StatsModifier);
    }

    public void OnDeactivate(AttackableUnit unit, Buff buff, Spell ownerSpell)
    {
    }

    public void OnUpdate(float diff)
    {
    }
}
