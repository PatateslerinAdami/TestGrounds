using GameServerCore.Scripting.CSharp;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.StatsNS;

namespace ItemPassives;

public class ItemID_3111 : IItemScript {
    public StatsModifier StatsModifier { get; } = new();

    public void OnActivate(ObjAIBase owner) {
        StatsModifier.Tenacity.FlatBonus = 0.35f;
        owner.AddStatModifier(StatsModifier);
    }
}