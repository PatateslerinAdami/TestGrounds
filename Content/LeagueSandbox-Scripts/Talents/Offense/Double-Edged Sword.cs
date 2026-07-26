using GameServerCore.Scripting.CSharp;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.API;
using GameServerLib.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;

namespace Talents
{
    internal class Talent_4111 : ITalentScript
    {
        private ObjAIBase _owner;

        public void OnActivate(ObjAIBase owner, byte rank)
        {
            _owner = owner;
            ApiEventManager.OnPreDealDamage.AddListener(this, owner, OnPreDealDamage);
            ApiEventManager.OnPreTakeDamage.AddListener(this, owner, OnPreTakeDamage);
        }

        private void OnPreDealDamage(DamageData damageData)
        {
            if (damageData.Attacker == _owner)
            {
                damageData.Damage *= 1.015f;
                damageData.PostMitigationDamage *= 1.015f;
            }
        }

        private void OnPreTakeDamage(DamageData damageData)
        {
            if (damageData.Target == _owner)
            {
                float factor = _owner.CharData.IsMelee ? 1.01f : 1.015f;
                damageData.Damage *= factor;
                damageData.PostMitigationDamage *= factor;
            }
        }
    }
}