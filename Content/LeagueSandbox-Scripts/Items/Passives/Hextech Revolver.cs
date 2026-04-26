using GameServerCore.Scripting.CSharp;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.StatsNS;

namespace ItemPassives;

public class ItemID_3145 : IItemScript {
    public StatsModifier StatsModifier { get; } = new();

    public void OnActivate(ObjAIBase owner) {
        StatsModifier.SpellVamp.FlatBonus = 0.12f;
        owner.AddStatModifier(StatsModifier);
    }
}