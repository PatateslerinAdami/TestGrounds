using GameServerCore.Scripting.CSharp;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.StatsNS;

namespace ItemPassives;

public class ItemID_1001 : IItemScript {
    public StatsModifier StatsModifier { get; } = new();

    public void OnActivate(ObjAIBase owner) {
        StatsModifier.MoveSpeed.PercentBonus = 0.05f;
        owner.AddStatModifier(StatsModifier);
    }
}