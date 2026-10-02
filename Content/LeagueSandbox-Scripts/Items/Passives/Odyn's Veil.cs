using GameServerCore.Scripting.CSharp;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.StatsNS;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace ItemPassives
{
    public class ItemID_3180 : IItemScript
    {
        public StatsModifier StatsModifier { get; } = new StatsModifier();

        public void OnActivate(ObjAIBase owner)
        {
            AddBuff("OdynsVeil", 0.0f, 1, null, owner, owner, true);
        }

        public void OnDeactivate(ObjAIBase owner)
        {
            RemoveBuff(owner, "OdynsVeil");
        }
    }
}
