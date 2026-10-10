using System.Linq;
using GameServerCore.Scripting.CSharp;
using GameServerLib.GameObjects.AttackableUnits;
using ItemHelpers;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.StatsNS;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace ItemPassives
{
    public abstract class RageItem : IItemScript
    {
        public StatsModifier StatsModifier { get; } = new StatsModifier();

        protected ObjAIBase Owner;
        protected abstract int ItemId { get; }

        private bool Preferred
        {
            get
            {
                return new[]
                {
                    3078,
                    3184,
                    3044
                }.FirstOrDefault(Owner.Inventory.HasItemWithID) == ItemId;
            }
        }

        public virtual void OnActivate(ObjAIBase owner)
        {
            Owner = owner;
            ApiEventManager.OnHitUnit.AddListener(this, owner, Hit);
            ApiEventManager.OnKillUnit.AddListener(this, owner, Kill);
        }

        private void Hit(DamageData data)
        {
            if (!Preferred || Owner.HasBuff("ItemPhageSpeed")
                || !ItemEffectHelpers.IsSuccessfulBasicDamage(Owner, data))
            {
                return;
            }

            AddBuff("ItemPhageMiniSpeed", 2, 1, null, Owner, Owner);
        }

        private void Kill(DeathData data)
        {
            if (Preferred && !Owner.IsDead)
            {
                AddBuff("ItemPhageSpeed", 2, 1, null, Owner, Owner);
            }
        }

        public virtual void OnDeactivate(ObjAIBase owner)
        {
            ApiEventManager.RemoveAllListenersForOwner(this);
            if (!new[]
            {
                3078,
                3184,
                3044
            }.Any(owner.Inventory.HasItemWithID))
            {
                RemoveBuff(owner, "ItemPhageSpeed");
                RemoveBuff(owner, "ItemPhageMiniSpeed");
            }
        }
    }

    public class ItemID_3044 : RageItem
    {
        protected override int ItemId => 3044;
    }

    public class ItemID_3184 : RageItem
    {
        protected override int ItemId => 3184;
    }
}
