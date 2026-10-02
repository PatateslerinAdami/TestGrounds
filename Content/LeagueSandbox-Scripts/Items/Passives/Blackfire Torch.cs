using GameServerCore.Scripting.CSharp;
using LeagueSandbox.GameServer.GameObjects.StatsNS;

namespace ItemPassives
{
    public class ItemID_3188 : IItemScript
    {
        public StatsModifier StatsModifier { get; } = new StatsModifier();
    }
}
