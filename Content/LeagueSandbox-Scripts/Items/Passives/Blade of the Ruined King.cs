using System;
using GameServerCore.Enums;
using GameServerLib.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;

namespace ItemPassives
{
    public class ItemID_3153 : OnHitItem
    {
        protected override void OnHit(DamageData data)
        {
            float damage = Math.Max(10, data.Target.Stats.CurrentHealth * 0.08f);
            if (data.Target is not Champion)
            {
                damage = Math.Min(60, damage);
            }

            var result = data.Target.TakeDamage(
                Owner,
                damage,
                DamageType.DAMAGE_TYPE_PHYSICAL,
                DamageSource.DAMAGE_SOURCE_PROC,
                false
            );
            Owner.TakeHeal(Owner, result.PostMitigationDamage * Owner.Stats.LifeSteal.Total, HealType.LifeSteal);
        }
    }
}
