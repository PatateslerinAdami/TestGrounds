using GameServerCore.Enums;
using GameServerLib.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;

namespace ItemHelpers
{
    internal static class ItemEffectHelpers
    {
        public static void UpdateTooltip(ObjAIBase owner, int itemId, int index, float value)
        {
            foreach (var item in owner.Inventory.GetAllItems())
            {
                if (item.ItemData.ItemId == itemId)
                {
                    UpdateTooltip(owner, item, index, value);
                }
            }
        }

        public static void UpdateTooltip(
            ObjAIBase owner,
            LeagueSandbox.GameServer.Inventory.Item item,
            int index,
            float value
        )
        {
            short slot = (short)(owner.Inventory.GetItemSlot(item) + (byte)SpellSlotType.InventorySlots);
            if (owner.Spells.TryGetValue(slot, out var spell) && spell.ToolTipData.Values[index].Value != value)
            {
                spell.SetToolTipVar(index, value);
            }
        }

        public static void TrackGold(ObjAIBase owner, int itemId, string stateKey, float amount, int tooltipIndex)
        {
            foreach (var item in owner.Inventory.GetAllItems())
            {
                if (item.ItemData.ItemId != itemId)
                {
                    continue;
                }

                var state = item.GetState();
                float total = state.GetFloat(stateKey) + amount;
                state.Set(stateKey, total);
                UpdateTooltip(owner, item, tooltipIndex, total);
            }
        }

        public static void ShowCharges(ObjAIBase owner, int itemId, int charges)
        {
            foreach (var item in owner.Inventory.GetAllItems())
            {
                if (item.ItemData.ItemId != itemId || item.SpellCharges == charges)
                {
                    continue;
                }

                LeagueSandbox.GameServer.API.ApiFunctionManager.SetItemCharges(
                    owner,
                    owner.Inventory.GetItemSlot(item),
                    charges
                );
            }
        }

        public static void UpdateAbilityPowerTooltip(ObjAIBase owner, int itemId, float multiplier)
        {
            var power = owner.Stats.AbilityPower;
            var bonus = ((power.BaseValue + power.BaseBonus) * (1.0f + power.PercentBaseBonus) + power.FlatBonus) * multiplier;
            foreach (var item in owner.Inventory.GetAllItems())
            {
                if (item.ItemData.ItemId != itemId)
                {
                    continue;
                }

                var slot = (short)(owner.Inventory.GetItemSlot(item) + (byte)SpellSlotType.InventorySlots);
                if (!owner.Spells.TryGetValue(slot, out var spell))
                {
                    continue;
                }

                if (spell.ToolTipData.Values[0].Value != bonus)
                {
                    spell.SetToolTipVar(0, bonus);
                }
            }
        }

        public static bool IsSuccessfulBasicAttack(ObjAIBase owner, DamageData data)
        {
            return data.IsAutoAttack && IsSuccessfulBasicDamage(owner, data);
        }

        public static bool IsSuccessfulBasicDamage(ObjAIBase owner, DamageData data)
            => data.DamageSource == DamageSource.DAMAGE_SOURCE_ATTACK && IsSuccessfulOnHit(owner, data);

        public static bool IsSuccessfulOnHit(ObjAIBase owner, DamageData data)
        {
            return !owner.IsDead && data.TriggersOnHitEffects && !data.IsHitBlocked && data.Attacker == owner
                && (data.DamageResultType == DamageResultType.RESULT_NORMAL
                || data.DamageResultType == DamageResultType.RESULT_CRITICAL)
                && !data.Target.IsDead
                && data.Target.Team != owner.Team
                && data.Target.CanTakeDamage(data.DamageType);
        }

        public static bool CanUseActive(ObjAIBase owner, Spell spell, int itemId)
        {
            var slot = spell.CastInfo.SpellSlot - (byte)SpellSlotType.InventorySlots;
            return !owner.IsDead && spell.CurrentCooldown <= 0.0f && slot >= 0
                && spell.CastInfo.SpellSlot < (byte)SpellSlotType.BluePillSlot
                && owner.Inventory.GetItem((byte)slot)?.ItemData.ItemId == itemId;
        }

        public static bool CanUseSharedActive(ObjAIBase owner, Spell spell, int itemId)
        {
            if (!CanUseActive(owner, spell, itemId))
            {
                return false;
            }

            foreach (var item in owner.Inventory.GetAllItems())
            {
                if (item.ItemData.ItemId != itemId)
                {
                    continue;
                }

                var spellSlot = (short)(owner.Inventory.GetItemSlot(item) + (byte)SpellSlotType.InventorySlots);
                if (owner.Spells.TryGetValue(spellSlot, out var other) && other != spell
                    && other.CurrentCooldown > 0.0f)
                {
                    return false;
                }
            }

            return true;
        }

        public static void ShareActiveCooldown(ObjAIBase owner, Spell spell, int itemId, float cooldown)
        {
            foreach (var item in owner.Inventory.GetAllItems())
            {
                if (item.ItemData.ItemId != itemId)
                {
                    continue;
                }

                var slot = (short)(owner.Inventory.GetItemSlot(item) + (byte)SpellSlotType.InventorySlots);
                if (owner.Spells.TryGetValue(slot, out var other) && other != spell)
                {
                    other.SetCooldown(cooldown, true);
                }
            }
        }

        public static void RestoreSharedActiveCooldown(ObjAIBase owner, Spell spell, int itemId)
        {
            var remaining = spell.CurrentCooldown;
            foreach (var item in owner.Inventory.GetAllItems())
            {
                if (item.ItemData.ItemId != itemId)
                {
                    continue;
                }

                var slot = (short)(owner.Inventory.GetItemSlot(item) + (byte)SpellSlotType.InventorySlots);
                if (owner.Spells.TryGetValue(slot, out var other))
                {
                    remaining = System.Math.Max(remaining, other.CurrentCooldown);
                }
            }

            if (remaining > spell.CurrentCooldown)
            {
                spell.SetCooldown(remaining, true);
            }
        }
    }
}
