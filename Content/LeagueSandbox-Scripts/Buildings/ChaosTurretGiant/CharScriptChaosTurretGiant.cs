using GameServerCore.Scripting.CSharp;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;
using LeagueSandbox.GameServer.GameObjects;
using GameServerLib.GameObjects.AttackableUnits;

namespace CharScripts;
//Purple Base Turrets
public class CharScriptChaosTurretGiant : ICharScript
{
    private ObjAIBase _turret;
    private Region _bubbleRegion;
    
    public void OnActivate(ObjAIBase owner, Spell spell)
    {
        _turret = owner;
        ApiEventManager.OnDeath.AddListener(this, _turret, OnDeath);
        _bubbleRegion = AddPosPerceptionBubble(_turret.Position, 800f, -1, _turret.Team, true);
    }

    private void OnDeath(DeathData data)
    {
        _bubbleRegion.SetToRemove();
        ApiEventManager.OnDeath.RemoveListener(this, _turret, OnDeath);
    }
}