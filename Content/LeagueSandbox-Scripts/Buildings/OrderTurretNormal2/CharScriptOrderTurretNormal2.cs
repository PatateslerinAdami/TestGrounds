using GameServerCore.Scripting.CSharp;
using GameServerLib.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.StatsNS;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace CharScripts;
// Blue Team Turret Lane Turret 2nd
public class CharScriptOrderTurretNormal2 : ICharScript
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