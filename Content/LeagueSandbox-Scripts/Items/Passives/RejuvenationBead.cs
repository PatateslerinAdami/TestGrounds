using System;
using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.StatsNS;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace ItemPassives;

public class ItemID_1006 : IItemScript {
    public  StatsModifier StatsModifier { get; } = new();

    public void OnActivate(ObjAIBase owner) {
        
    }

    public void OnDeactivate(ObjAIBase owner) {
    }
}