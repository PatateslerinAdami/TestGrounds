using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.StatsNS;

namespace ItemPassives
{
    public class ItemID_3046 : IItemScript
    {
        public StatsModifier StatsModifier { get; private set; } = new StatsModifier();

        public void OnActivate(ObjAIBase owner)
        {
            owner.SetStatus(StatusFlags.Ghosted, true);
            owner.AddStatModifier(StatsModifier);
        }
        public void OnDeactivate(ObjAIBase owner)
        {
            owner.SetStatus(StatusFlags.Ghosted, false);
        }
    }
}
