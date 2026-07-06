using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using GameServerLib.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.StatsNS;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace Talents
{
    internal class Talent_4124 : ITalentScript
    {
        public void OnActivate(ObjAIBase owner, byte rank)
        {
            ApiEventManager.OnKill.AddListener(this,  owner, OnKill);
        }

        private void OnKill(DeathData data)
        {
            data.Killer.TakeHeal(data.Killer, 2f, HealType.HealthRegeneration);
            data.Killer.IncreasePAR(data.Killer, 1f);
        }
    }
}