using GameServerCore.Scripting.CSharp;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.StatsNS;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace ItemPassives
{
    // Blade and Ring share the named health passive, even when bought together.
    public abstract class ProspectorItem : IItemScript
    {
        public StatsModifier StatsModifier { get; } = new StatsModifier();

        public void OnActivate(ObjAIBase owner)
        {
            if (!owner.HasBuff("Prospectors"))
            {
                AddBuff("Prospectors", 0, 1, null, owner, owner, true);
            }
        }

        public void OnDeactivate(ObjAIBase owner)
        {
            if (!owner.Inventory.HasItemWithID(1062) && !owner.Inventory.HasItemWithID(1063))
            {
                RemoveBuff(owner, "Prospectors");
            }
        }
    }

    public class ItemID_1062 : ProspectorItem
    {
    }

    public class ItemID_1063 : ProspectorItem
    {
    }
}
