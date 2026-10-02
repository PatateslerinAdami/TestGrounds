using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace ItemPassives
{
    public class ItemID_3046 : UniqueStatItem
    {
        protected override void Configure()
        {
        }

        public override void OnActivate(ObjAIBase owner)
        {
            AddBuff("ItemPhantomDancer", 0, 1, null, owner, owner, true);
        }

        public override void OnDeactivate(ObjAIBase owner)
        {
            RemoveBuff(owner, "ItemPhantomDancer");
        }
    }
}
