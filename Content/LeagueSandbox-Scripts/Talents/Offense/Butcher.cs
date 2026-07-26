using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using GameServerLib.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;

namespace Talents
{
    internal class Talent_4114 : ITalentScript
    {
        private ObjAIBase _owner;

        public void OnActivate(ObjAIBase owner, byte rank)
        {
            _owner = owner;
            ApiEventManager.OnPreDealDamage.AddListener(this, owner, OnPreDealDamage);
        }

        private void OnPreDealDamage(DamageData damageData)
        {
            if (damageData.Attacker == _owner && (damageData.Target is Minion || damageData.Target is Monster))
            {
                if (damageData.IsAutoAttack || damageData.DamageSource == DamageSource.DAMAGE_SOURCE_SPELL)
                {
                    damageData.Damage += 2.0f;
                    damageData.PostMitigationDamage = damageData.Target.Stats.GetPostMitigationDamage(damageData.Damage, damageData.DamageType, _owner);
                }
            }
        }
    }
}