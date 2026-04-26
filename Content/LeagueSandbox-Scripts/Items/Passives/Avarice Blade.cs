using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using GameServerLib.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.SpellNS.Missile;
using LeagueSandbox.GameServer.GameObjects.SpellNS.Sector;
using LeagueSandbox.GameServer.GameObjects.StatsNS;
using LeagueSandbox.GameServer.Scripting.CSharp;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace ItemPassives;

public class ItemID_3093 : IItemScript {
    public StatsModifier StatsModifier { get; } = new();

    public void OnActivate(ObjAIBase owner) {
        StatsModifier.GoldPerSecond.FlatBonus = 0.3f;
        owner.AddStatModifier(StatsModifier);
        ApiEventManager.OnKill.AddListener(this, owner, OnKill);
    }

    private void OnKill(DeathData data) {
        if (data.Killer is Champion champion) {
            champion.AddGold(champion, 2f, true);
        }
    }

    public void OnDeactivate(ObjAIBase owner) {
        ApiEventManager.OnKill.RemoveListener(this, owner, OnKill);
    }
}