using GameServerCore.Enums;
using GameServerLib.GameObjects.AttackableUnits;
using ItemHelpers;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;

namespace ItemPassives
{
    public class ItemID_3146 : UniqueStatItem
    {
        protected override void Configure()
        {
            StatsModifier.SpellVamp.FlatBonus = 0.2f;
        }

        private ObjAIBase _owner;
        public override void OnActivate(ObjAIBase owner)
        {
            _owner = owner;
            base.OnActivate(owner);
            ApiEventManager.OnHitUnit.AddListener(this, owner, Hit);
        }

        private void Hit(DamageData data)
        {
            if (!ItemEffectHelpers.IsSuccessfulBasicDamage(_owner, data))
            {
                return;
            }

            foreach (var item in _owner.Inventory.GetAllItems())
            {
                if (item.ItemData.ItemId == 3146)
                {
                    var spell = _owner.Spells[(short)(_owner.Inventory.GetItemSlot(item) + 6)];
                    if (spell.CurrentCooldown > 0)
                    {
                        spell.SetCooldown(System.Math.Max(0, spell.CurrentCooldown - 3), true);
                    }
                }
            }
        }

        public override void OnUpdate(float diff)
        {
            ItemEffectHelpers.UpdateTooltip(_owner, 3146, 0, 0.4f * _owner.Stats.AbilityPower.Total);
        }

        public override void OnDeactivate(ObjAIBase owner)
        {
            ApiEventManager.RemoveAllListenersForOwner(this);
        }
    }
}
