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
/// Poppy Paragon visible buff (COMBAT_ENCHANCER, STACKS_AND_RENEWS).
/// ICON: Client reads InventoryIcon = PoppyDefenseOfDemacia.dds from PoppyParagonOfDemacia.inibin
/// STATS: Each stack adds perStack armor + AD via StatsModifier.
/// 
/// STACKS_AND_RENEWS fires OnActivate for each AddBuff call.
/// Each call adds delta × perStack (delta is always 1 in practice).
/// OnDeactivate does NOT remove — base system removes all modifiers added by this buff.
/// </summary>
internal class PoppyParagonOfDemacia : IBuffGameScript
{
    public BuffScriptMetaData BuffMetaData { get; set; } = new()
    {
        BuffType = BuffType.COMBAT_ENCHANCER,
        BuffAddType = BuffAddType.STACKS_AND_RENEWS,
        MaxStacks = 10
    };

    public StatsModifier StatsModifier { get; private set; } = new();
    private bool _inited;
    private float _perStack;
    private int _appliedStacks;
    private AttackableUnit _unit;
    private Buff _buff;

    public void OnActivate(AttackableUnit unit, Buff buff, Spell ownerSpell)
    {
        _unit = unit;
        _buff = buff;

        if (!_inited)
        {
            int rank = 1;
            if (ownerSpell?.CastInfo?.Owner is ObjAIBase caster && caster.Spells.Count > 1)
            {
                caster.Spells.TryGetValue(1, out var w);
                if (w != null) rank = w.CastInfo.SpellLevel;
            }
            _perStack = 1f + rank * 0.5f; // 1.5 / 2 / 2.5 / 3 / 3.5
            _inited = true;
        }

        // Each OnActivate call adds stats for the NEW stacks only (delta).
        // With STACKS_AND_RENEWS, each AddBuff in a loop fires OnActivate separately.
        // StackCount at this point reflects the total AFTER this activation.
        int stacksNow = Math.Min(buff.StackCount, 10);
        int delta = stacksNow - _appliedStacks;

        if (delta > 0)
        {
            StatsModifier.Armor.FlatBonus = _perStack * delta;
            StatsModifier.AttackDamage.FlatBonus = _perStack * delta;
            unit.AddStatModifier(StatsModifier);
            _appliedStacks = stacksNow;
        }
    }

    public void OnDeactivate(AttackableUnit unit, Buff buff, Spell ownerSpell)
    {
        // Base system removes all Stat Modifiers that were added by this buff.
        // Don't remove extra here — would cause double-removal → negative stats.
    }

    public void OnUpdate(float diff)
    {
        // Safety net: if the internal stack tracker desyncs from actual StackCount,
        // correct it here.
        if (_unit == null || _buff == null) return;

        int expected = Math.Min(_buff.StackCount, 10);
        if (expected == _appliedStacks) return;

        int delta = expected - _appliedStacks;
        if (delta > 0)
        {
            StatsModifier.Armor.FlatBonus = _perStack * delta;
            StatsModifier.AttackDamage.FlatBonus = _perStack * delta;
            _unit.AddStatModifier(StatsModifier);
        }
        // Don't handle negative delta here — OnDeactivate covers removal.
        _appliedStacks = expected;
    }
}
