using GameServerCore.Scripting.CSharp;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.StatsNS;

namespace ItemPassives;

public class ItemID_3136 : IItemScript {
    public StatsModifier StatsModifier { get; } = new();

    public void OnActivate(ObjAIBase owner) {
        StatsModifier.MagicPenetration.FlatBonus = 15f;
        owner.AddStatModifier(StatsModifier);
    }
}