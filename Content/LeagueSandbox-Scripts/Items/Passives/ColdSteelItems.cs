using GameServerCore.Enums;
using GameServerLib.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace ItemPassives
{
    public class ItemID_3082 : UniqueStatItem
    {
        protected override void Configure()
        {
        }

        public override void OnActivate(ObjAIBase owner)
        {
            ApiEventManager.OnTakeDamage.AddListener(this, owner, Hit);
        }

        private void Hit(DamageData data)
        {
            if (data.IsAutoAttack && data.DamageSource == DamageSource.DAMAGE_SOURCE_ATTACK
                && data.Attacker is ObjAIBase attacker
                && !attacker.IsDead
                && attacker.Team != data.Target.Team
                && data.DamageResultType != DamageResultType.RESULT_DODGE
                && data.DamageResultType != DamageResultType.RESULT_MISS)
            {
                AddBuff("Cripple", 1, 1, null, attacker, (ObjAIBase)data.Target);
            }
        }

        public override void OnDeactivate(ObjAIBase owner)
        {
            ApiEventManager.RemoveAllListenersForOwner(this);
        }
    }

    public class ItemID_3143 : ItemID_3082
    {
        private ObjAIBase _owner;
        public override void OnActivate(ObjAIBase owner)
        {
            _owner = owner;
            base.OnActivate(owner);
            UpdateTooltip();
        }

        public override void OnUpdate(float diff)
        {
            UpdateTooltip();
        }

        private void UpdateTooltip()
        {
            float armorDuration = _owner.Stats.Armor.Total / 200f;
            float magicResistDuration = _owner.Stats.MagicResist.Total / 200f;
            foreach (var item in _owner.Inventory.GetAllItems())
            {
                if (item.ItemData.ItemId != 3143)
                {
                    continue;
                }

                var slot = (short)(_owner.Inventory.GetItemSlot(item) + (byte)SpellSlotType.InventorySlots);
                if (!_owner.Spells.TryGetValue(slot, out var spell))
                {
                    continue;
                }

                if (spell.ToolTipData.Values[1].Value != armorDuration)
                {
                    spell.SetToolTipVar(1, armorDuration);
                }

                if (spell.ToolTipData.Values[2].Value != magicResistDuration)
                {
                    spell.SetToolTipVar(2, magicResistDuration);
                }
            }
        }
    }

    public class ItemID_3222 : ItemID_3028
    {
        protected override void Configure()
        {
            StatsModifier.CooldownReduction.FlatBonus = -0.1f;
        }
    }
}
