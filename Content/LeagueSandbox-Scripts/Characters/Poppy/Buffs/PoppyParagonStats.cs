using System;
using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using LeagueSandbox.GameServer.GameObjects;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.StatsNS;
using LeagueSandbox.GameServer.Scripting.CSharp;

namespace Buffs;

/// <summary>
/// Poppy Paragon — ONE BUFF INSTANCE = ONE STACK.
/// Each AddBuff("PoppyParagonStats", ...) creates a SEPARATE buff instance.
/// Each instance adds EXACTLY _perStack armor + AD (not multiplied by StackCount).
/// 10× AddBuff = 10 separate instances × perStack each = total 15.0 at rank 1.
/// When one expires, its ONE contribution is removed by the base system.
/// </summary>
internal class PoppyParagonStats : IBuffGameScript
{
    public BuffScriptMetaData BuffMetaData { get; set; } = new()
    {
        BuffType = BuffType.COMBAT_ENCHANCER,
        BuffAddType = BuffAddType.STACKS_AND_RENEWS,
        MaxStacks = 10
    };

    public StatsModifier StatsModifier { get; private set; } = new();

    public void OnActivate(AttackableUnit unit, Buff buff, Spell ownerSpell)
    {
        int rank = 1;
        if (ownerSpell?.CastInfo?.Owner is ObjAIBase caster && caster.Spells.Count > 1)
        {
            caster.Spells.TryGetValue(1, out var w);
            if (w != null) rank = w.CastInfo.SpellLevel;
        }
        float perStack = 1f + rank * 0.5f; // 1.5 / 2 / 2.5 / 3 / 3.5

        // This instance = 1 stack. Always add EXACTLY perStack.
        StatsModifier.Armor.FlatBonus = perStack;
        StatsModifier.AttackDamage.FlatBonus = perStack;
        unit.AddStatModifier(StatsModifier);
    }

    public void OnDeactivate(AttackableUnit unit, Buff buff, Spell ownerSpell)
    {
        // Base system removes this one instance's StatsModifier.
    }

    public void OnUpdate(float diff) { }
}
