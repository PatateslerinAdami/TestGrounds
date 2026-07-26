using GameServerCore.Scripting.CSharp;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.API;
using GameServerLib.GameObjects.AttackableUnits;

namespace Talents
{
    internal class Talent_4162 : ITalentScript
    {
        private ObjAIBase _owner;

        public void OnActivate(ObjAIBase owner, byte rank)
        {
            _owner = owner;
            ApiEventManager.OnPreDealDamage.AddListener(this, owner, OnPreDealDamage);
        }

        private void OnPreDealDamage(DamageData damageData)
        {
            if (damageData.Attacker == _owner)
            {
                damageData.Damage *= 1.03f;
                damageData.PostMitigationDamage *= 1.03f;
            }
        }
    }
}