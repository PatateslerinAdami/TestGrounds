using System.Linq;
using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using GameServerLib.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.StatsNS;
using LeagueSandbox.GameServer.Scripting.CSharp;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;


namespace ItemPassives;

public class ItemID_1503 : IItemScript
{
    private ObjAIBase _turret;
    private Region _bubble;
    public StatsModifier StatsModifier { get; } = new();

    public void OnActivate(ObjAIBase owner)
    {
        _turret = owner;
        ApiEventManager.OnDeath.AddListener(this, _turret, OnDeath);
        AddUnitPerceptionBubble(_turret, 1000f, -1f, _turret.Team, true);
    }

    private void OnDeath(DeathData data)
    {
        _bubble.SetToRemove();
    }

    public void OnDeactivate(ObjAIBase owner)
    {
        _bubble.SetToRemove();
    }
}