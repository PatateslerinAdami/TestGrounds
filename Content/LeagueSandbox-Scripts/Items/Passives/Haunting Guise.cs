using GameServerCore.Scripting.CSharp;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.StatsNS;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace ItemPassives
{
    public class ItemID_3136 : IItemScript
    {
        public StatsModifier StatsModifier { get; } = new StatsModifier();

        public void OnActivate(ObjAIBase owner)
        {
            if (!owner.HasBuff("EyesOfPain"))
            {
                AddBuff("EyesOfPain", 0, 1, null, owner, owner, true);
            }
        }

        public void OnDeactivate(ObjAIBase owner)
        {
            if (!owner.Inventory.HasItemWithID(3151))
            {
                RemoveBuff(owner, "EyesOfPain");
            }
        }
    }
}
