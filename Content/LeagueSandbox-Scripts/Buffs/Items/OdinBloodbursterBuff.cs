using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using LeagueSandbox.GameServer.GameObjects;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.StatsNS;
using LeagueSandbox.GameServer.Scripting.CSharp;

namespace Buffs
{
    public class OdinBloodbursterBuff : IBuffGameScript
    {
        public BuffScriptMetaData BuffMetaData { get; } = new BuffScriptMetaData
        {
            BuffType = BuffType.COMBAT_ENCHANCER,
            BuffAddType = BuffAddType.STACKS_AND_RENEWS,
            MaxStacks = 5
        };
        public StatsModifier StatsModifier { get; } = new StatsModifier();

        private bool _statsApplied;
        public void OnActivate(AttackableUnit unit, Buff buff, Spell ownerSpell)
        {
            if (_statsApplied)
            {
                unit.RemoveStatModifier(StatsModifier);
            }

            StatsModifier.AttackDamage.FlatBonus = 6.0f * buff.StackCount;
            StatsModifier.LifeSteal.FlatBonus = 0.01f * buff.StackCount;
            unit.AddStatModifier(StatsModifier);
            _statsApplied = true;
        }
    }
}
