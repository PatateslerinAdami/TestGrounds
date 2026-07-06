using GameServerCore.Scripting.CSharp;
using GameServerLib.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.StatsNS;

namespace Talents
{
    internal class Talent_4221 : ITalentScript
    {
        private ObjAIBase _owner;
        
        public void OnActivate(ObjAIBase owner, byte rank)
        {
            _owner = owner;
            ApiEventManager.OnPreTakeDamage.AddListener(this, owner, OnPreTakeDamage);
        }

        private void OnPreTakeDamage(DamageData data)
        {
            data.PostMitigationDamage -= _owner.IsMelee ? 2 : 1;
        }
    }
}