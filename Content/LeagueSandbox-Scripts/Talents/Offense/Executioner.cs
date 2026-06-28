using GameServerCore.Scripting.CSharp;
using GameServerLib.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.StatsNS;

namespace Talents
{
    internal class Talent_4134 : ITalentScript
    {
        private byte _rank;
        public void OnActivate(ObjAIBase owner, byte rank)
        {
            _rank = rank;
            ApiEventManager.OnPreDealDamage.AddListener(this, owner, OnPreDealDamage);
        }
        
        private void OnPreDealDamage(DamageData data)
        {
            var modifier = _rank switch
            {
                1 => 0.2f,
                2 => 0.35f,
                3 => 0.5f,
                _ => 0f
            };
            
            if (data.Target.Stats.CurrentHealth <= data.Target.Stats.HealthPoints.Total/modifier)
            {
                data.PostMitigationDamage += data.PostMitigationDamage * 0.05f;
            }
            
        }
    }
}