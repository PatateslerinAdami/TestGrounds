using GameServerCore.Scripting.CSharp;
using GameServerLib.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.StatsNS;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace Talents
{
    internal class Talent_4214 : ITalentScript
    {
        private byte _rank;

        public void OnActivate(ObjAIBase owner, byte rank)
        {
            _rank = rank;
            ApiEventManager.OnPreTakeDamage.AddListener(this, owner, OnPreTakeDamage);
        }

        private void OnPreTakeDamage(DamageData data)
        {
            if (data.Target is Monster)
            {
                data.PostMitigationDamage -= _rank == 2 ? 2 : 1;
            }
        }
    }
}