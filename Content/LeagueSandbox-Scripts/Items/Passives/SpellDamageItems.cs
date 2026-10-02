using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using GameServerLib.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.StatsNS;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace ItemPassives
{
    public abstract class SpellDamageItem : IItemScript
    {
        public StatsModifier StatsModifier { get; } = new StatsModifier();

        protected ObjAIBase Owner;
        public virtual void OnActivate(ObjAIBase owner)
        {
            Owner = owner;
            ApiEventManager.OnDealDamage.AddListener(this, owner, Hit);
        }

        private void Hit(DamageData data)
        {
            if (data.Target.IsDead || data.Target.Team == Owner.Team || data.PostMitigationDamage <= 0
                || data.Target is not ObjAIBase
                || data.Target is BaseTurret
                || data.Target is Minion { IsWard: true })
            {
                return;
            }

            OnDamage(data);
        }

        public static bool IsSpell(DamageData data)
        {
            return data.DamageSource == DamageSource.DAMAGE_SOURCE_SPELL
                || data.DamageSource == DamageSource.DAMAGE_SOURCE_SPELLAOE
                || data.DamageSource == DamageSource.DAMAGE_SOURCE_SPELLPERSIST;
        }

        protected abstract void OnDamage(DamageData data);
        public virtual void OnDeactivate(ObjAIBase owner)
        {
            ApiEventManager.RemoveAllListenersForOwner(this);
        }
    }

    public class ItemID_3071 : SpellDamageItem
    {
        public override void OnActivate(ObjAIBase owner)
        {
            base.OnActivate(owner);
            StatsModifier.ArmorPenetration.FlatBonus = 10;
            StatsModifier.CooldownReduction.FlatBonus = -0.1f;
            owner.AddStatModifier(StatsModifier);
        }

        protected override void OnDamage(DamageData data)
        {
            if (data.Target is Champion && data.DamageType == DamageType.DAMAGE_TYPE_PHYSICAL)
            {
                AddBuff("BlackCleaverDebuff", 4, 1, null, data.Target, Owner);
            }
        }
    }

    public class ItemID_3116 : SpellDamageItem
    {
        protected override void OnDamage(DamageData data)
        {
            if (!IsSpell(data))
            {
                return;
            }

            var variables = new BuffVariables();
            variables.Set("slow", data.DamageSource == DamageSource.DAMAGE_SOURCE_SPELL ? 0.35f : 0.15f);
            AddBuff("ItemRylaiSlow", 1.5f, 1, null, data.Target, Owner, buffVariables: variables);
        }
    }

    public class ItemID_3151 : SpellDamageItem
    {
        public override void OnActivate(ObjAIBase owner)
        {
            base.OnActivate(owner);
            // Eyes of Pain is shared with Haunting Guise.
            if (!owner.HasBuff("EyesOfPain"))
            {
                AddBuff("EyesOfPain", 0, 1, null, owner, owner, true);
            }
        }

        protected override void OnDamage(DamageData data)
        {
            if (IsSpell(data))
            {
                AddBuff("LiandrysTormentBurn", 3, 1, null, data.Target, Owner);
            }
        }

        public override void OnDeactivate(ObjAIBase owner)
        {
            base.OnDeactivate(owner);
            if (!owner.Inventory.HasItemWithID(3136))
            {
                RemoveBuff(owner, "EyesOfPain");
            }
        }
    }

    public class ItemID_3165 : SpellDamageItem
    {
        public override void OnActivate(ObjAIBase owner)
        {
            base.OnActivate(owner);
            StatsModifier.CooldownReduction.FlatBonus = -0.2f;
            owner.AddStatModifier(StatsModifier);
        }

        protected override void OnDamage(DamageData data)
        {
            if (data.Target is Champion && data.DamageType == DamageType.DAMAGE_TYPE_MAGICAL
                && data.Target.Stats.CurrentHealth < data.Target.Stats.HealthPoints.Total * 0.4f)
            {
                AddBuff("GrievousWounds", 4, 1, null, data.Target, Owner);
            }
        }
    }
}
