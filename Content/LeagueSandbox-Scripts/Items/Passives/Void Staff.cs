using GameServerCore.Scripting.CSharp;
using LeagueSandbox.GameServer.GameObjects.StatsNS;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;

namespace ItemPassives
{
    public class ItemID_3135 : IItemScript
    {
        public StatsModifier StatsModifier { get; private set; } = new StatsModifier();

        public void OnActivate(ObjAIBase owner)
        {
            StatsModifier.MagicPenetration.PercentBaseBonus = 0.35f;
            owner.AddStatModifier(StatsModifier);
        }
    }
}
