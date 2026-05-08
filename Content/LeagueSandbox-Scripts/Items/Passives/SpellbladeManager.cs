using System.Collections.Generic;
using System.Linq;
using GameServerCore.Enums;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace ItemPassives;

public static class SpellbladeManager
{
    private const int SheenItemId = 3057;
    private const int TrinityForceItemId = 3078;
    private const int LichBaneItemId = 3100;
    private const int IcebornGauntletItemId = 3025;

    private const float SpellbladeCooldown = 1.5f;
    private const float ProcBuffDuration = 10.0f;
    private const float IcebornZoneDuration = 2.0f;
    private const float IcebornSlowPercent = 0.30f;

    private static readonly int[] SpellbladeItemIds = { SheenItemId, TrinityForceItemId, LichBaneItemId, IcebornGauntletItemId };
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
        return TryGetBestSpellbladeProc(owner, out var proc) ? proc.ItemId : 0;
    }

    public static bool TryArmSpellblade(ObjAIBase owner, Spell spell)
    {
        if (owner == null || spell == null)
        {
            return false;
        }

        if (!spell.Script.ScriptMetadata.TriggersSpellCasts)
        {
            return false;
        }

        if (owner.HasBuff("SheenDelay"))
        {
            return false;
        }

        if (HasAnySpellbladeProc(owner))
        {
            return false;
        }

        if (!TryGetBestSpellbladeProc(owner, out var proc))
        {
            return false;
        }

        if (proc.ItemId == IcebornGauntletItemId)
        {
            AddIcebornProc(owner, spell, true, proc.DamageAmount);
            return true;
        }

        AddDamageProc(owner, spell, proc);

        if (IsItemReady(owner, IcebornGauntletItemId))
        {
            AddIcebornProc(owner, spell, false, 0.0f);
        }

        return true;
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

    private static bool TryGetBestSpellbladeProc(ObjAIBase owner, out SpellbladeProc bestProc)
    {
        bestProc = default;

        if (owner == null)
        {
            return false;
        }

        var hasBest = false;
        TryConsider(owner, SheenItemId, owner.Stats.AttackDamage.BaseValue, ref bestProc, ref hasBest);
        TryConsider(owner, TrinityForceItemId, owner.Stats.AttackDamage.BaseValue * 2.0f, ref bestProc, ref hasBest);
        TryConsider(owner, LichBaneItemId, (owner.Stats.AttackDamage.BaseValue + owner.Stats.AbilityPower.Total) * 0.75f, ref bestProc, ref hasBest);
        TryConsider(owner, IcebornGauntletItemId, owner.Stats.AttackDamage.BaseValue * 1.25f, ref bestProc, ref hasBest);

        return hasBest;
    }

    private static void TryConsider(ObjAIBase owner, int itemId, float damageAmount, ref SpellbladeProc bestProc, ref bool hasBest)
    {
        if (!IsItemReady(owner, itemId))
        {
            return;
        }

        var tiePriority = GetTiePriority(owner, itemId);
        if (!hasBest || damageAmount > bestProc.DamageAmount || (damageAmount == bestProc.DamageAmount && tiePriority < bestProc.TiePriority))
        {
            bestProc = new SpellbladeProc(itemId, damageAmount, tiePriority);
            hasBest = true;
        }
    }

    private static void AddDamageProc(ObjAIBase owner, Spell spell, SpellbladeProc proc)
    {
        var variables = new BuffVariables();
        variables.Set("sourceItemId", proc.ItemId);
        variables.Set("damageAmount", proc.DamageAmount);

        if (proc.ItemId == LichBaneItemId)
        {
            AddBuff("LichBane", ProcBuffDuration, 1, spell, owner, owner, buffVariables: variables);
            return;
        }

        AddBuff("Sheen", ProcBuffDuration, 1, spell, owner, owner, buffVariables: variables);
    }

    private static void AddIcebornProc(ObjAIBase owner, Spell spell, bool dealDamage, float damageAmount)
    {
        var variables = new BuffVariables();
        variables.Set("dealDamage", dealDamage);
        variables.Set("damageAmount", damageAmount);
        variables.Set("damageMultiplier", 1.25f);
        variables.Set("zoneDuration", IcebornZoneDuration);
        variables.Set("slowPercent", IcebornSlowPercent);
        variables.Set("itemCooldown", SpellbladeCooldown);
        variables.Set("sourceItemId", IcebornGauntletItemId);

        AddBuff("ItemFrozenFist", ProcBuffDuration, 1, spell, owner, owner);
        AddBuff("IcebornGauntletProc", ProcBuffDuration, 1, spell, owner, owner, buffVariables: variables);
    }

    private static bool IsItemReady(ObjAIBase owner, int itemId)
    {
        return GetItemSpell(owner, itemId) is { CurrentCooldown: <= 0.0f };
    }

    public static Spell? GetItemSpell(ObjAIBase owner, int itemId)
    {
        if (owner == null)
        {
            return null;
        }

        var inventorySlot = GetInventorySlot(owner, itemId);
        if (inventorySlot < 0)
        {
            return null;
        }

        short spellSlot = (short)(inventorySlot + (byte)SpellSlotType.InventorySlots);
        return owner.Spells.ContainsKey(spellSlot) ? owner.Spells[spellSlot] : null;
    }

    {
        order.RemoveAll(itemId => !OwnerHasItem(owner, itemId));
    }

    private static bool OwnerHasItem(ObjAIBase owner, int itemId)
    {
        return GetInventorySlot(owner, itemId) >= 0;
    }

    private static int GetFirstSpellbladeFromInventory(ObjAIBase owner)
    {
        return Enumerable.Range(0, 7)
            .Select(slot => owner.Inventory.GetItem((byte)slot))
            .FirstOrDefault(item => item != null && IsSpellbladeItemId(item.ItemData.ItemId))
            ?.ItemData.ItemId ?? 0;
    }

    private static bool IsSpellbladeItemId(int itemId)
    {
        return SpellbladeItemIds.Contains(itemId);
    }

    private static uint GetOwnerKey(ObjAIBase owner)
    {
        return owner.NetId;
    }

    private static int GetTiePriority(ObjAIBase owner, int itemId)
    {
        if (owner == null)
        {
            return int.MaxValue;
        }

        var key = GetOwnerKey(owner);
        if (OrderByOwner.TryGetValue(key, out var order))
        {
            Prune(owner, order);
            var index = order.IndexOf(itemId);
            if (index >= 0)
            {
                return index;
            }
        }

        var inventorySlot = GetInventorySlot(owner, itemId);
        return inventorySlot >= 0 ? inventorySlot : int.MaxValue;
    }

    private static int GetInventorySlot(ObjAIBase owner, int itemId)
    {
        return Enumerable.Range(0, 7)
            .Where(slot => owner.Inventory.GetItem((byte)slot)?.ItemData.ItemId == itemId)
            .DefaultIfEmpty(-1)
            .First();
    }

    private readonly struct SpellbladeProc
    {
        public SpellbladeProc(int itemId, float damageAmount, int tiePriority)
        {
            ItemId = itemId;
            DamageAmount = damageAmount;
            TiePriority = tiePriority;
        }

        public int ItemId { get; }
        public float DamageAmount { get; }
        public int TiePriority { get; }
    }
}
