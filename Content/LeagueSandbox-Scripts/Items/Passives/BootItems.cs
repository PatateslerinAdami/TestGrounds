using System.Collections.Generic;
using System.Linq;
using GameServerCore.Scripting.CSharp;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.StatsNS;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace ItemPassives
{
    public abstract class BootItem : IItemScript
    {
        public StatsModifier StatsModifier { get; } = new StatsModifier();

        public void OnActivate(ObjAIBase owner)
        {
            if (!owner.HasBuff("ItemBootEffects"))
            {
                AddBuff("ItemBootEffects", 0, 1, null, owner, owner, true);
            }
            else if (owner.GetBuffWithName("ItemBootEffects").BuffScript is Buffs.ItemBootEffects boots)
            {
                boots.Refresh();
            }

            if (BootInfo.HasTenacity(owner) && !owner.HasBuff("ItemTenacity"))
            {
                AddBuff("ItemTenacity", 0, 1, null, owner, owner, true);
            }
        }

        public void OnDeactivate(ObjAIBase owner)
        {
            if (!owner.Inventory.GetAllItems().Any(i => BootInfo.Bases.ContainsKey(i.ItemData.ItemId)))
            {
                RemoveBuff(owner, "ItemBootEffects");
            }

            if (owner.GetBuffWithName("ItemBootEffects")?.BuffScript is Buffs.ItemBootEffects boots)
            {
                boots.Refresh();
            }

            if (!BootInfo.HasTenacity(owner))
            {
                RemoveBuff(owner, "ItemTenacity");
            }
        }
    }

    public static class BootInfo
    {
        public static readonly Dictionary<int, int> Bases = new Dictionary<int, int>
        {
            { 1001, 1001 },
            { 3006, 3006 },
            { 3009, 3009 },
            { 3020, 3020 },
            { 3047, 3047 },
            { 3111, 3111 },
            { 3117, 3117 },
            { 3158, 3158 },
            { 3251, 3006 },
            { 3252, 3006 },
            { 3253, 3006 },
            { 3254, 3006 },
            { 3256, 3020 },
            { 3257, 3020 },
            { 3258, 3020 },
            { 3259, 3020 },
            { 3261, 3047 },
            { 3262, 3047 },
            { 3263, 3047 },
            { 3264, 3047 },
            { 3266, 3111 },
            { 3267, 3111 },
            { 3268, 3111 },
            { 3269, 3111 },
            { 3271, 3117 },
            { 3272, 3117 },
            { 3273, 3117 },
            { 3274, 3117 },
            { 3276, 3158 },
            { 3277, 3158 },
            { 3278, 3158 },
            { 3279, 3158 },
            { 3281, 3009 },
            { 3282, 3009 },
            { 3283, 3009 },
            { 3284, 3009 },
        };
        public static int Enchantment(int id)
        {
            return id >= 3251 && id <= 3284 ? (id - 3251) % 5 + 1 : 0;
        }

        public static bool HasTenacity(ObjAIBase owner)
        {
            return owner.Inventory.GetAllItems().Any(i => i.ItemData.ItemId == 3170 || i.ItemData.ItemId == 3172
                || (Bases.TryGetValue(i.ItemData.ItemId, out var b)
                && b == 3111));
        }
    }

    public class ItemID_1001 : BootItem
    {
    }

    public class ItemID_3006 : BootItem
    {
    }

    public class ItemID_3009 : BootItem
    {
    }

    public class ItemID_3020 : BootItem
    {
    }

    public class ItemID_3047 : BootItem
    {
    }

    public class ItemID_3111 : BootItem
    {
    }

    public class ItemID_3117 : BootItem
    {
    }

    public class ItemID_3158 : BootItem
    {
    }

    public class ItemID_3251 : BootItem
    {
    }

    public class ItemID_3252 : BootItem
    {
    }

    public class ItemID_3253 : BootItem
    {
    }

    public class ItemID_3254 : BootItem
    {
    }

    public class ItemID_3256 : BootItem
    {
    }

    public class ItemID_3257 : BootItem
    {
    }

    public class ItemID_3258 : BootItem
    {
    }

    public class ItemID_3259 : BootItem
    {
    }

    public class ItemID_3261 : BootItem
    {
    }

    public class ItemID_3262 : BootItem
    {
    }

    public class ItemID_3263 : BootItem
    {
    }

    public class ItemID_3264 : BootItem
    {
    }

    public class ItemID_3266 : BootItem
    {
    }

    public class ItemID_3267 : BootItem
    {
    }

    public class ItemID_3268 : BootItem
    {
    }

    public class ItemID_3269 : BootItem
    {
    }

    public class ItemID_3271 : BootItem
    {
    }

    public class ItemID_3272 : BootItem
    {
    }

    public class ItemID_3273 : BootItem
    {
    }

    public class ItemID_3274 : BootItem
    {
    }

    public class ItemID_3276 : BootItem
    {
    }

    public class ItemID_3277 : BootItem
    {
    }

    public class ItemID_3278 : BootItem
    {
    }

    public class ItemID_3279 : BootItem
    {
    }

    public class ItemID_3281 : BootItem
    {
    }

    public class ItemID_3282 : BootItem
    {
    }

    public class ItemID_3283 : BootItem
    {
    }

    public class ItemID_3284 : BootItem
    {
    }

    public class ItemID_3170 : IItemScript
    {
        public StatsModifier StatsModifier { get; } = new StatsModifier();

        public virtual void OnActivate(ObjAIBase owner)
        {
            if (!owner.HasBuff("ItemTenacity"))
            {
                AddBuff("ItemTenacity", 0, 1, null, owner, owner, true);
            }
        }

        public void OnDeactivate(ObjAIBase owner)
        {
            if (!BootInfo.HasTenacity(owner))
            {
                RemoveBuff(owner, "ItemTenacity");
            }
        }
    }

    public class ItemID_3172 : ItemID_3170
    {
    }
}
