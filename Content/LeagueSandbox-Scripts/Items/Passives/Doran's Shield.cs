using System;
using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using GameServerLib.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.StatsNS;

namespace ItemPassives
{
    public class ItemID_1054 : IItemScript
    {
        public StatsModifier StatsModifier { get; } = new StatsModifier();

        public void OnActivate(ObjAIBase owner)
        {
            ApiEventManager.OnPreTakeDamage.AddListener(this, owner, BlockDamage);
        }

        private void BlockDamage(DamageData data)
        {
            if (data.Attacker is Champion && data.DamageType != DamageType.DAMAGE_TYPE_TRUE && (data.IsAutoAttack
                || data.DamageSource == DamageSource.DAMAGE_SOURCE_SPELL))
            {
                data.PostMitigationDamage = Math.Max(0, data.PostMitigationDamage - 8);
            }
        }

        public void OnDeactivate(ObjAIBase owner)
        {
            ApiEventManager.RemoveAllListenersForOwner(this);
        }
    }
}
