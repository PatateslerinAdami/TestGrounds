using System;
using LeagueSandbox.GameServer.GameObjects;
using System.Collections.Generic;
using System.Linq;
using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using LeagueSandbox.GameServer.Scripting.CSharp;
using LeagueSandbox.GameServer.Logging;
using log4net;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;

namespace LeagueSandbox.GameServer.Inventory
{
    public class Inventory
    {
        private const byte TRINKET_SLOT = 6;
        private const byte BASE_INVENTORY_SIZE = 7; // Includes trinket
        private const byte EXTRA_INVENTORY_SIZE = 7;
        private const byte RUNE_INVENTORY_SIZE = 30;
        private InventoryManager _owner;
        private static ILog _logger = LoggerProvider.GetLogger();
        public Dictionary<int, IItemScript> ItemScripts = new Dictionary<int, IItemScript>();
        public Dictionary<string, float> SoldActiveCooldowns { get; } = new Dictionary<string, float>();
        public Item[] Items { get; }

        public Inventory(InventoryManager owner)
        {
            _owner = owner;
            Items = new Item[BASE_INVENTORY_SIZE + EXTRA_INVENTORY_SIZE + RUNE_INVENTORY_SIZE];
        }

        public Item[] GetBaseItems()
        {
            return Items.Take(BASE_INVENTORY_SIZE).ToArray();
        }

        public Item AddItem(ItemData item, ObjAIBase owner, BuffVariables state = null)
        {
            if (item.ItemGroup.ToLower().Equals("relicbase"))
            {
                return AddTrinketItem(item, owner);
            }

            var addedItem = item.MaxStacks > 1 ? AddStackingItem(item) : AddNewItem(item, state);
            if (addedItem != null && owner != null)
            {
                LoadOwnerStats(item, owner, GetItemSlot(addedItem));
            }

            return addedItem;
        }

        public Item SetItemToSlot(ItemData item, ObjAIBase owner, byte slot, BuffVariables state = null)
        {
            if (item.ItemGroup.ToLower().Equals("relicbase"))
            {
                return AddTrinketItem(item, owner);
            }

            var addedItem = SetItem(slot, item, state);
            if (owner != null)
            {
                LoadOwnerStats(item, owner, slot);
            }

            return addedItem;
        }

        private void LoadOwnerStats(ItemData item, ObjAIBase owner, byte slot)
        {
            owner.AddStatModifier(item);

            if (!string.IsNullOrEmpty(item.SpellName))
            {
                // A different purchased item must not inherit the previous slot occupant's cooldown.
                owner.Spells[(short)(slot + (byte)SpellSlotType.InventorySlots)].SetCooldown(0, true);
                owner.SetSpell(item.SpellName, (byte)(slot + (byte)SpellSlotType.InventorySlots), true, networked: false);
                if (SoldActiveCooldowns.TryGetValue(ItemCooldownGroups.Get(item.SpellName), out var remaining) && remaining > 0)
                    owner.Spells[(short)(slot + (byte)SpellSlotType.InventorySlots)].SetCooldown(remaining, true);
            }

            //Checks if the item's script was already loaded before
            if (!ItemScripts.ContainsKey(item.ItemId))
            {
                //Loads the Script
                ItemScripts.Add(item.ItemId, CSharpScriptEngine.CreateObjectStatic<IItemScript>("ItemPassives", $"ItemID_{item.ItemId}") ?? new ItemScriptEmpty());
                try
                {
                    ItemScripts[item.ItemId].OnActivate(owner);
                }
                catch(Exception e)
                {
                    _logger.Error(null, e);
                }
            }
        }

        public Item SetExtraItem(byte slot, ItemData item)
        {
            if (slot < BASE_INVENTORY_SIZE)
            {
                throw new Exception("Invalid extra item slot—must be greater than base inventory size!");
            }

            return SetItem(slot, item);
        }

        private Item SetItem(byte slot, ItemData item, BuffVariables state = null)
        {
            Items[slot] = Item.CreateFromType(item, state);
            return Items[slot];
        }

        public Item GetItem(byte slot)
        {
            return Items[slot];
        }

        public Item GetItem(string name, bool isItemName = false)
        {
            if (name != null)
            {
                for (byte i = 0; i < Items.Length; i++)
                {
                    if (Items[i] != null)
                    {
                        if (isItemName)
                        {
                            if (name == Items[i].ItemData.Name)
                            {
                                return Items[i];
                            };
                        }
                        else if (name == Items[i].ItemData.SpellName)
                        {
                            return Items[i];
                        }
                    }
                }
            }
            return null;
        }

        public void RemoveItem(byte slot, ObjAIBase owner, int stacksToRemove = 1, bool force = false)
        {
            if (stacksToRemove < 0)
            {
                throw new Exception("Stacks to be Removed can't be a negative number!");
            }

            var itemID = Items[slot].ItemData.ItemId;
            int finalStacks = Items[slot].StackCount - stacksToRemove;

            if (finalStacks <= 0 || force)
            {
                if (Items[slot] == null)
                    return;

                if (owner != null)
                {
                    owner.RemoveStatModifier(Items[slot].ItemData);
                    if (!string.IsNullOrEmpty(Items[slot].ItemData.SpellName))
                    {
                        var spellSlot = (byte)(slot + (byte)SpellSlotType.InventorySlots);
                        if (owner.Spells.TryGetValue(spellSlot, out var spell))
                        {
                            var remainingCooldown = spell.CurrentCooldown;
                            var key = ItemCooldownGroups.Get(Items[slot].ItemData.SpellName);
                            SoldActiveCooldowns[key] = Math.Max(remainingCooldown, SoldActiveCooldowns.TryGetValue(key, out var saved) ? saved : 0);
                            owner.RemoveSpell(spellSlot);
                            // Keep the slot's cooldown so selling and immediately rebuying cannot reset an active.
                            if (remainingCooldown > 0.0f)
                            {
                                owner.Spells[spellSlot].SetCooldown(remainingCooldown, true);
                            }
                        }
                    }
                }

                Items[slot] = null;

                if (!HasItemWithID(itemID) && ItemScripts.ContainsKey(itemID))
                {
                    if (owner != null)
                    {
                        try
                        {
                            ItemScripts[itemID].OnDeactivate(owner);
                        }
                        catch(Exception e)
                        {
                            _logger.Error(null, e);
                        }
                        if (ItemScripts[itemID].StatsModifier != null)
                        {
                            owner.RemoveStatModifier(ItemScripts[itemID].StatsModifier);
                        }
                    }
                    ItemScripts.Remove(itemID);
                }

            }
            else
            {
                Items[slot].SetStacks(finalStacks);
            }
        }

        public bool HasItemWithID(int ItemID)
        {
            foreach (var item in Items)
            {
                if (item != null && ItemID == item.ItemData.ItemId)
                {
                    return true;
                }
            }
            return false;
        }

        public byte GetItemSlot(Item item)
        {
            for (byte i = 0; i < Items.Length; i++)
            {
                if (Items[i] != item)
                {
                    continue;
                }

                return i;
            }

            throw new Exception("Specified item doesn't exist in the inventory!");
        }

        public void SwapItems(byte slot1, byte slot2)
        {
            if (slot1 == TRINKET_SLOT || slot2 == TRINKET_SLOT)
            {
                throw new Exception("Can't swap to or from the trinket slot");
            }

            var buffer = Items[slot1];
            Items[slot1] = Items[slot2];
            Items[slot2] = buffer;
        }

        private Item AddTrinketItem(ItemData item, ObjAIBase owner)
        {
            if (Items[TRINKET_SLOT] != null)
            {
                return null;
            }

            var itemResult = SetItem(TRINKET_SLOT, item);
            if (owner != null)
            {
                if (!string.IsNullOrEmpty(item.SpellName))
                {
                    owner.Spells[(short)(TRINKET_SLOT + (byte)SpellSlotType.InventorySlots)].SetCooldown(0, true);
                    owner.SetSpell(item.SpellName, TRINKET_SLOT + (byte)SpellSlotType.InventorySlots, true, networked: false);
                }
                //Checks if the item's script was already loaded before
                if (!ItemScripts.ContainsKey(item.ItemId))
                {
                    //Loads the Script
                    ItemScripts.Add(item.ItemId, CSharpScriptEngine.CreateObjectStatic<IItemScript>("ItemPassives", $"ItemID_{item.ItemId}") ?? new ItemScriptEmpty());
                    try
                    {
                        ItemScripts[item.ItemId].OnActivate(owner);
                    }
                    catch(Exception e)
                    {
                        _logger.Error(null, e);
                    }
                }
            }

            return itemResult;
        }

        private Item AddStackingItem(ItemData item)
        {
            for (var i = 0; i < BASE_INVENTORY_SIZE; i++)
            {
                if (Items[i] == null)
                {
                    continue;
                }

                if (item.ItemId != Items[i].ItemData.ItemId)
                {
                    continue;
                }

                if (Items[i].IncrementStackCount())
                {
                    return Items[i];
                }

                return null;
            }
            return AddNewItem(item);
        }
        private Item AddNewItem(ItemData item, BuffVariables state = null)
        {
            for (var i = 0; i < BASE_INVENTORY_SIZE; i++)
            {
                if (i == TRINKET_SLOT)
                {
                    continue;
                }

                if (Items[i] != null)
                {
                    continue;
                }

                return SetItem((byte)i, item, state);
            }

            return null;
        }
    }
}
