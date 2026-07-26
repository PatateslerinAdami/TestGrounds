using System;
using GameServerCore.Scripting.CSharp;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.API;
using GameServerLib.GameObjects.AttackableUnits;

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

        private void OnPreTakeDamage(DamageData damageData)
        {
            if (damageData.Target == _owner && damageData.Attacker is Champion)
            {
                float reduction = _owner.CharData.IsMelee ? 2.0f : 1.0f;
                damageData.Damage = Math.Max(0.0f, damageData.Damage - reduction);
                damageData.PostMitigationDamage = Math.Max(0.0f, damageData.PostMitigationDamage - reduction);
            }
        }
    }
}