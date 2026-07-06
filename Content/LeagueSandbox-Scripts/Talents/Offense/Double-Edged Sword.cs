using GameServerCore.Scripting.CSharp;
using GameServerLib.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.StatsNS;

namespace Talents
{
    internal class Talent_4111 : ITalentScript
    {
        public void OnActivate(ObjAIBase owner, byte rank)
        {
            ApiEventManager.OnPreDealDamage.AddListener(this, owner, OnPreDealDamage);
            ApiEventManager.OnPreTakeDamage.AddListener(this, owner, OnPreTakeDamage);
        }
        
        private void OnPreDealDamage(DamageData data)
        {
            data.PostMitigationDamage += data.PostMitigationDamage * 0.03f;
        }
        
        private void OnPreTakeDamage(DamageData data)
        {
            data.PostMitigationDamage += data.PostMitigationDamage * 0.015f;
        }
    }
}