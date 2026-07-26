using GameServerCore.Scripting.CSharp;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.API;
using GameServerLib.GameObjects.AttackableUnits;

namespace Talents
{
    internal class Talent_4134 : ITalentScript
    {
        private ObjAIBase _owner;
        private byte _rank;

        public void OnActivate(ObjAIBase owner, byte rank)
        {
            _owner = owner;
            _rank = rank;
            ApiEventManager.OnPreDealDamage.AddListener(this, owner, OnPreDealDamage);
        }

        private void OnPreDealDamage(DamageData damageData)
        {
            if (damageData.Attacker == _owner && damageData.Target is Champion targetChamp)
            {
                if (targetChamp.Stats.CurrentHealth / targetChamp.Stats.HealthPoints.Total < 0.5f)
                {
                    float bonusFactor = 1.0f + (0.01f * _rank);
                    damageData.Damage *= bonusFactor;
                    damageData.PostMitigationDamage *= bonusFactor;
                }
            }
        }
    }
}