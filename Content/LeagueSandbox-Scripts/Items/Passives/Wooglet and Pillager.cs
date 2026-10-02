using GameServerCore.Scripting.CSharp;
using ItemHelpers;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.StatsNS;

namespace ItemPassives
{
    public class ItemID_3090 : IItemScript
    {
        private ObjAIBase _owner;
        public StatsModifier StatsModifier { get; } = new StatsModifier();

        public void OnActivate(ObjAIBase owner)
        {
            _owner = owner;
            StatsModifier.AbilityPower.PercentBonus = 0.25f;
            owner.AddStatModifier(StatsModifier);
            UpdateTooltip();
        }

        private void UpdateTooltip()
        {
            ItemEffectHelpers.UpdateAbilityPowerTooltip(_owner, 3090, 0.25f);
        }

        public void OnUpdate(float diff)
        {
            UpdateTooltip();
        }
    }

    public class ItemID_3104 : IItemScript
    {
        public StatsModifier StatsModifier { get; } = new StatsModifier();

        public void OnActivate(ObjAIBase owner)
        {
            StatsModifier.CriticalDamage.FlatBonus = 0.5f;
            owner.AddStatModifier(StatsModifier);
        }
    }
}
