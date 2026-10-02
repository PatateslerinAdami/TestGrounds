using System;
using LeagueSandbox.GameServer.GameObjects;
using GameServerCore.Scripting.CSharp;
using LeagueSandbox.GameServer.GameObjects.Other;
using static GameServerCore.Content.HashFunctions;

namespace LeagueSandbox.GameServer.Inventory
{
    public class Item : Stackable
    {
        public int TotalPrice => ItemData.TotalPrice;
        public ItemData ItemData { get; }
        public int SpellCharges { get; private set; }

        private readonly BuffVariables _state;
        public BuffVariables GetState() => _state;

        private Item(ItemData data, BuffVariables state = null)
        {
            ItemData = data;
            _state = state?.Clone() ?? new BuffVariables();
            StackCount = 1;
            SpellCharges = 0;
        }

        public override bool IncrementStackCount()
        {
            if (StackCount >= ItemData.MaxStacks)
            {
                return false;
            }

            StackCount++;
            return true;
        }

        public override void SetStacks(int newStacks)
        {
            if (newStacks < 1 || newStacks > ItemData.MaxStacks)
                throw new Exception($"Cannot set stack size out of bounds (max is {ItemData.MaxStacks})");

            StackCount = newStacks;
        }

        public void SetSpellCharges(int newCharges)
        {
            if (newCharges < 0 || newCharges > byte.MaxValue)
                throw new Exception($"Cannot set spell charges out of bounds (0..{byte.MaxValue})");

            SpellCharges = newCharges;
        }

        public static Item CreateFromType(ItemData item, BuffVariables state = null)
        {
            return new Item(item, state);
        }
    }
}
