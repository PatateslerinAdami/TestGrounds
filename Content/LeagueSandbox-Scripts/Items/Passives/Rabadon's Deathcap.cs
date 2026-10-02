using GameServerCore.Scripting.CSharp;
using ItemHelpers;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.StatsNS;

namespace ItemPassives
{
    public class ItemID_3089 : IItemScript
    {
        ObjAIBase owner;
        public StatsModifier StatsModifier { get; private set; } = new StatsModifier();

        public void OnActivate(ObjAIBase owner)
        {
            this.owner = owner;
            StatsModifier.AbilityPower.PercentBonus = 0.3f;
            owner.AddStatModifier(StatsModifier);
            UpdateTooltip();
        }

        private void UpdateTooltip()
        {
            ItemEffectHelpers.UpdateAbilityPowerTooltip(owner, 3089, 0.3f);
        }

        public void OnUpdate(float diff)
        {
            UpdateTooltip();
        }
    }
}
