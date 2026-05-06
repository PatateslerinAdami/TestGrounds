using System.Collections.Generic;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;

namespace ItemPassives;

public static class SpellbladeManager
{
    private static readonly int[] SpellbladeItemIds = { 3057, 3078, 3100, 3025 };
    private static readonly Dictionary<uint, List<int>> OrderByOwner = new();

    public static void Register(ObjAIBase owner, int itemId)
    {
        if (owner == null)
        {
            return;
        }

        var key = GetOwnerKey(owner);
        if (!OrderByOwner.TryGetValue(key, out var order))
        {
            order = new List<int>();
            OrderByOwner[key] = order;
        }

        if (!order.Contains(itemId))
        {
            order.Add(itemId);
        }

        Prune(owner, order);
    }

    public static void Unregister(ObjAIBase owner, int itemId)
    {
        if (owner == null)
        {
            return;
        }

        var key = GetOwnerKey(owner);
        if (!OrderByOwner.TryGetValue(key, out var order))
        {
            return;
        }

        order.Remove(itemId);
        Prune(owner, order);

        if (order.Count == 0)
        {
            OrderByOwner.Remove(key);
        }
    }

    public static bool IsActive(ObjAIBase owner, int itemId)
    {
        return GetActiveSpellbladeItemId(owner) == itemId;
    }

    public static int GetActiveSpellbladeItemId(ObjAIBase owner)
    {
        if (owner == null)
        {
            return 0;
        }

        var key = GetOwnerKey(owner);
        if (!OrderByOwner.TryGetValue(key, out var order))
        {
            return GetFirstSpellbladeFromInventory(owner);
        }

        Prune(owner, order);
        return order.Count > 0 ? order[0] : GetFirstSpellbladeFromInventory(owner);
    }

    public static bool HasAnySpellbladeProc(ObjAIBase owner)
    {
        if (owner == null)
        {
            return false;
        }

        return owner.HasBuff("Sheen")
            || owner.HasBuff("LichBane")
            || owner.HasBuff("ItemFrozenFist")
            || owner.HasBuff("IcebornGauntletProc");
    }

    private static void Prune(ObjAIBase owner, List<int> order)
    {
        order.RemoveAll(itemId => !OwnerHasItem(owner, itemId));
    }

    private static bool OwnerHasItem(ObjAIBase owner, int itemId)
    {
        for (byte i = 0; i < 7; i++)
        {
            var item = owner.Inventory.GetItem(i);
            if (item != null && item.ItemData.ItemId == itemId)
            {
                return true;
            }
        }

        return false;
    }

    private static int GetFirstSpellbladeFromInventory(ObjAIBase owner)
    {
        for (byte i = 0; i < 7; i++)
        {
            var item = owner.Inventory.GetItem(i);
            if (item != null && IsSpellbladeItemId(item.ItemData.ItemId))
            {
                return item.ItemData.ItemId;
            }
        }

        return 0;
    }

    private static bool IsSpellbladeItemId(int itemId)
    {
        for (var i = 0; i < SpellbladeItemIds.Length; i++)
        {
            if (SpellbladeItemIds[i] == itemId)
            {
                return true;
            }
        }

        return false;
    }

    private static uint GetOwnerKey(ObjAIBase owner)
    {
        return owner.NetId;
    }
}
