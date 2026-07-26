using System;
using GameServerCore.Scripting.CSharp;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.API;
using GameServerLib.GameObjects.AttackableUnits;

namespace Talents
{
    internal class Talent_4211 : ITalentScript
    {
        private ObjAIBase _owner;
        private byte _rank;

        public void OnActivate(ObjAIBase owner, byte rank)
        {
            _owner = owner;
            _rank = rank;
            ApiEventManager.OnPreTakeDamage.AddListener(this, owner, OnPreTakeDamage);
        }

        private void OnPreTakeDamage(DamageData damageData)
        {
            if (damageData.Target == _owner && damageData.Attacker is Champion && damageData.IsAutoAttack)
            {
                float reduction = 1.0f * _rank;
                damageData.Damage = Math.Max(0.0f, damageData.Damage - reduction);
                damageData.PostMitigationDamage = Math.Max(0.0f, damageData.PostMitigationDamage - reduction);
            }
        }
    }
}