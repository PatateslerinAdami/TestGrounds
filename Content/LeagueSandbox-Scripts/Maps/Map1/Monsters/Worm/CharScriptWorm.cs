using GameServerCore.Scripting.CSharp;
using GameServerLib.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;

namespace CharScripts;

internal class CharScriptWorm : ICharScript
{
    public void OnActivate(ObjAIBase owner, Spell spell = null)
    {
        if (owner is Monster)
        {
            ApiEventManager.OnDeath.AddListener(this, owner, OnDeath, true);
        }
    }

    public void OnDeath(DeathData deathData)
    {
        // 4.20 NeutralMinionSpawn.lua PredefinedCampKillEvents[NASHOR] = EVENT_ON_KILL_WORM:
        // fire the world kill event so the client plays the Baron-kill announcement.
        ApiGameEvents.AnnounceKillWorm(deathData);
    }
}
