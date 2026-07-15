using GameServerCore.Scripting.CSharp;
using GameServerLib.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace CharScripts;

// Turret perception bubble (vision 800 + true sight) — script-side like the Map1 turret char
// scripts (Riot's model; the old BaseTurret.OnAdded hardcode was removed as a duplicate).
public class CharScriptTT_ChaosTurret4 : ICharScript
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
