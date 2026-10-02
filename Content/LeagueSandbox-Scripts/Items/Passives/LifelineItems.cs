using System;
using GameServerCore.Enums;
using GameServerLib.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace ItemPassives
{
    public class ItemID_3155 : UniqueStatItem
    {
        protected ObjAIBase Owner;
        protected virtual float ShieldAmount => 250;

        protected override void Configure()
        {
        }

        public override void OnActivate(ObjAIBase owner)
        {
            Owner = owner;
            ApiEventManager.OnPreApplyDamage.AddListener(this, owner, Hit);
        }

        private void Hit(DamageData data)
        {
            if (Owner.IsDead || !Owner.CanTakeDamage(data.DamageType)
                || data.DamageType != DamageType.DAMAGE_TYPE_MAGICAL
                || data.PostMitigationDamage <= 0
                || Owner.HasBuff("ItemLifelineCooldown")
                || (ShieldAmount == 250
                && Owner.Inventory.HasItemWithID(3156)))
            {
                return;
            }

            var shields = Owner.GetCombinedShieldValues();
            float healthDamage = Math.Max(0, data.PostMitigationDamage - shields.Magical - shields.MagicalAndPhysical);
            if (healthDamage <= 0 || Owner.Stats.CurrentHealth - healthDamage >= Owner.Stats.HealthPoints.Total * 0.3f)
            {
                return;
            }

            var variables = new BuffVariables();
            variables.Set("shield", ShieldAmount);
            variables.Set("magicOnly", true);
            AddBuff("ItemLifelineShield", 5, 1, null, Owner, Owner, buffVariables: variables);
            AddBuff("ItemLifelineCooldown", 90, 1, null, Owner, Owner);
        }

        public override void OnDeactivate(ObjAIBase owner)
        {
            ApiEventManager.RemoveAllListenersForOwner(this);
        }
    }

    public class ItemID_3156 : ItemID_3155
    {
        protected override float ShieldAmount => 400;

        public override void OnUpdate(float diff)
        {
            float damage = Math.Min(35, Math.Max(0, (1 - Owner.Stats.CurrentHealth / Owner.Stats.HealthPoints.Total) / 0.02f));
            if (Math.Abs(damage - StatsModifier.AttackDamage.FlatBonus) < 0.0001f)
            {
                return;
            }

            Owner.RemoveStatModifier(StatsModifier);
            StatsModifier.AttackDamage.FlatBonus = damage;
            Owner.AddStatModifier(StatsModifier);
        }
    }
}
