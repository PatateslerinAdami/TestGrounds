using System;
using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using GameServerLib.GameObjects.AttackableUnits;
using ItemHelpers;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.StatsNS;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace ItemPassives
{
    public abstract class OnHitItem : IItemScript
    {
        public StatsModifier StatsModifier { get; } = new StatsModifier();

        protected ObjAIBase Owner;
        public virtual void OnActivate(ObjAIBase owner)
        {
            Owner = owner;
            ApiEventManager.OnHitUnit.AddListener(this, owner, Hit);
        }

        private void Hit(DamageData data)
        {
            if (ItemEffectHelpers.IsSuccessfulBasicAttack(Owner, data) && data.Target is ObjAIBase
                && data.Target is not BaseTurret
                && data.Target is not Minion { IsWard: true })
            {
                OnHit(data);
            }
        }

        protected abstract void OnHit(DamageData data);
        public virtual void OnUpdate(float diff)
        {
        }

        public virtual void OnDeactivate(ObjAIBase owner)
        {
            ApiEventManager.RemoveAllListenersForOwner(this);
        }
    }

    public class ItemID_3022 : OnHitItem
    {
        protected override void OnHit(DamageData data)
        {
            var variables = new BuffVariables();
            variables.Set("slow", Owner.CharData.IsMelee ? 0.4f : 0.3f);
            AddBuff("ItemFrozenMalletSlow", 1.5f, 1, null, data.Target, Owner, buffVariables: variables);
        }
    }

    public class ItemID_3115 : OnHitItem
    {
        public override void OnActivate(ObjAIBase owner)
        {
            base.OnActivate(owner);
            StatsModifier.CooldownReduction.FlatBonus = -0.2f;
            owner.AddStatModifier(StatsModifier);
        }

        public override void OnUpdate(float diff)
        {
            ItemEffectHelpers.UpdateTooltip(Owner, 3115, 0, 0.15f * Owner.Stats.AbilityPower.Total);
        }

        protected override void OnHit(DamageData data)
        {
            data.Target.TakeDamage(
                Owner,
                15 + 0.15f * Owner.Stats.AbilityPower.Total,
                DamageType.DAMAGE_TYPE_MAGICAL,
                DamageSource.DAMAGE_SOURCE_PROC,
                false
            );
        }
    }

    public class ItemID_3091 : OnHitItem
    {
        protected override void OnHit(DamageData data)
        {
            data.Target.TakeDamage(Owner, 42, DamageType.DAMAGE_TYPE_MAGICAL, DamageSource.DAMAGE_SOURCE_PROC, false);
            if (data.Target.IsDead)
            {
                return;
            }

            AddBuff("ItemWitsEndDebuff", 5, 1, null, data.Target, Owner);
            AddBuff("ItemWitsEndBuff", 5, 1, null, Owner, Owner);
        }

        public override void OnDeactivate(ObjAIBase owner)
        {
            base.OnDeactivate(owner);
            RemoveBuff(owner, "ItemWitsEndBuff");
        }
    }

    public class ItemID_3123 : OnHitItem
    {
        protected override void OnHit(DamageData data)
        {
            if (data.Target is Champion)
            {
                AddBuff("GrievousWounds", 1.5f, 1, null, data.Target, Owner);
            }
        }
    }

    public abstract class CriticalBleedItem : OnHitItem
    {
        protected abstract bool Reveals { get; }

        protected override void OnHit(DamageData data)
        {
            if (data.DamageResultType != DamageResultType.RESULT_CRITICAL || (!Reveals
                && Owner.Inventory.HasItemWithID(3185)))
            {
                return;
            }

            var variables = new BuffVariables();
            variables.Set("damage", Math.Max(0, Owner.Stats.AttackDamage.Total - Owner.Stats.AttackDamage.BaseValue) * (Reveals ? 0.9f : 0.6f));
            variables.Set("reveal", Reveals);
            AddBuff(
                Reveals ? "ItemLightBringerBleed" : "ItemWickedHatchetBleed",
                3,
                1,
                null,
                data.Target,
                Owner,
                buffVariables: variables
            );
        }
    }

    public class ItemID_3122 : CriticalBleedItem
    {
        protected override bool Reveals => false;
    }

    public class ItemID_3185 : CriticalBleedItem
    {
        protected override bool Reveals => true;

        private float _elapsed;
        public override void OnUpdate(float diff)
        {
            _elapsed += diff;
            if (_elapsed < 250 || Owner.IsDead)
            {
                return;
            }

            _elapsed %= 250;
            ItemVisionHelpers.Detect(Owner, 1000, true);
        }
    }
}
