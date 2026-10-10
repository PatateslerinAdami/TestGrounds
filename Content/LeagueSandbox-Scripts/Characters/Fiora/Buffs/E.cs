using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using LeagueSandbox.GameServer.GameObjects;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.StatsNS;
using LeagueSandbox.GameServer.Scripting.CSharp;

namespace Buffs
{
    public class FioraFlurry : IBuffGameScript
    {
        public BuffScriptMetaData BuffMetaData { get; } = new BuffScriptMetaData()
        {
            BuffType = BuffType.COMBAT_ENCHANCER,
            BuffAddType = BuffAddType.REPLACE_EXISTING
        };

        public StatsModifier StatsModifier { get; } = new StatsModifier();

        private Spell _spell;

        public void OnActivate(AttackableUnit unit, Buff buff, Spell spell)
        {
            _spell = spell;
            StatsModifier.AttackSpeed.PercentBaseBonus = spell.SpellData.GetEffectAmount(1, buff.Variables.GetInt("Rank")) / 100f;
            unit.AddStatModifier(StatsModifier);
            spell.SetSpellToggle(true);
        }

        public void OnDeactivate(AttackableUnit unit, Buff buff, Spell spell)
        {
            _spell.SetSpellToggle(false);
        }
    }

    public class FioraFlurryDummy : IBuffGameScript
    {
        public BuffScriptMetaData BuffMetaData { get; } = new BuffScriptMetaData()
        {
            BuffType = BuffType.COMBAT_ENCHANCER,
            MaxStacks = 3,
            BuffAddType = BuffAddType.STACKS_AND_RENEWS
        };

        public StatsModifier StatsModifier { get; } = new StatsModifier();

        private AttackableUnit _owner;
        private Buff _buff;
        private bool _installed;

        public void OnActivate(AttackableUnit unit, Buff buff, Spell spell)
        {
            _owner = unit;
            _buff = buff;
            SyncSpeed();
        }

        public void SyncSpeed()
        {
            if (_buff == null || _buff.Remove)
            {
                return;
            }

            float amount = _buff.Variables.GetFloat("Speed") * _buff.StackCount;
            if (_installed && amount == StatsModifier.MoveSpeed.PercentBonus)
            {
                return;
            }

            if (_installed)
            {
                _owner.RemoveStatModifier(StatsModifier);
            }

            StatsModifier.MoveSpeed.PercentBonus = amount;
            _owner.AddStatModifier(StatsModifier);
            _installed = true;
        }

        public void OnUpdate(float diff)
        {
            SyncSpeed();
        }
    }

    public class FioraFlurryMS : FioraFlurryDummy
    {
    }
}
