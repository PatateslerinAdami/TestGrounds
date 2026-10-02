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
public class ItemID_3093 : IItemScript
{
    public StatsModifier StatsModifier { get; } = new();

    private ObjAIBase _owner;
    public void OnActivate(ObjAIBase owner)
    {
        _owner = owner;
        StatsModifier.GoldPerSecond.FlatBonus = 0.3f;
        owner.AddStatModifier(StatsModifier);
        ApiEventManager.OnKillUnit.AddListener(this, owner, OnKill);
        if (owner is Champion champion)
        {
            ApiEventManager.OnAmbientGold.AddListener(
                this,
                champion,
                gold => ItemHelpers.ItemEffectHelpers.TrackGold(owner, 3093, "avariceGoldEarned", StatsModifier.GoldPerSecond.FlatBonus, 0)
            );
        }
    }

    private void OnKill(DeathData data)
    {
        if (data.Killer is Champion champion)
        {
            champion.AddGold(champion, 2f, true);
            ItemHelpers.ItemEffectHelpers.TrackGold(_owner, 3093, "avariceGoldEarned", 2, 0);
        }
    }

    public void OnUpdate(float diff)
    {
        ItemHelpers.ItemEffectHelpers.TrackGold(_owner, 3093, "avariceGoldEarned", 0, 0);
    }

    public void OnDeactivate(ObjAIBase owner)
    {
        ApiEventManager.RemoveAllListenersForOwner(this);
    }
}
