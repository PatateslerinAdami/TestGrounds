using GameServerCore.Scripting.CSharp;
using GameServerLib.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.StatsNS;

namespace Talents
{
    internal class Talent_4211 : ITalentScript
    {
        private byte _rank;
        
        public void OnActivate(ObjAIBase owner, byte rank)
        {
            _rank = rank;
            ApiEventManager.OnPreTakeDamage.AddListener(this, owner, OnPreTakeDamage);
        }

        private void OnPreTakeDamage(DamageData data)
        {
            data.PostMitigationDamage -= _rank == 2 ? 2 : 1;
        }
    }
}