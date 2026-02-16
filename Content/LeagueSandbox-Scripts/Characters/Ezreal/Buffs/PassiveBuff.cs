using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using LeaguePackets.Game.Events;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.StatsNS;
using LeagueSandbox.GameServer.Scripting.CSharp;

namespace Buffs
{
    internal class EzrealRisingSpellForce : IBuffGameScript
    {
        public BuffScriptMetaData BuffMetaData { get; set; } = new BuffScriptMetaData
        {
            BuffType = BuffType.COMBAT_ENCHANCER,
            BuffAddType = BuffAddType.STACKS_AND_RENEWS,
            IsHidden = false,
            MaxStacks = 5
        };
        int count = 0;
        Buff _buff;
        public StatsModifier StatsModifier { get; private set; } = new StatsModifier();
        public StatsModifier StatsModifier2 { get; private set; } = new StatsModifier();


        public void OnActivate(AttackableUnit unit, Buff buff, Spell ownerSpell)
        {
            _buff = buff;
            StatsModifier2.AttackSpeed.PercentBonus = 0.10f;
            unit.AddStatModifier(StatsModifier2);
            UpdateTooltip();
        }

        public void OnDeactivate(AttackableUnit unit, Buff buff, Spell ownerSpell)
        {
            for (int i = 0; i < buff.StackCount; i++)
            {
                unit.RemoveStatModifier(StatsModifier2);
            }
        }
        private void UpdateTooltip()
        {
            if (_buff != null)
            {
                float value = 10f * _buff.StackCount;
                _buff.SetToolTipVar(0, value);
            }
        }
    }
}