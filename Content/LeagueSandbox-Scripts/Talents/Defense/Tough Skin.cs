using GameServerCore.Scripting.CSharp;
using GameServerLib.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using System;

namespace Talents
{
    internal class Talent_4222 : ITalentScript
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
            if (damageData.Target == _owner && damageData.Attacker is Monster)
            {
                float reduction = 1.0f * _rank;
                damageData.Damage = Math.Max(0.0f, damageData.Damage - reduction);
                damageData.PostMitigationDamage = Math.Max(0.0f, damageData.PostMitigationDamage - reduction);
            }
        }
    }
}