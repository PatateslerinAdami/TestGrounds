using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Buffs;
using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using GameServerLib.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.SpellNS.Missile;
using LeagueSandbox.GameServer.GameObjects.SpellNS.Sector;
using LeagueSandbox.GameServer.GameObjects.StatsNS;
using LeagueSandbox.GameServer.Scripting.CSharp;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace ItemPassives;

public class ItemID_1080 : IItemScript {
    private const int       ItemId = 1080;
    private       ObjAIBase _owner;

    public StatsModifier StatsModifier { get; } = new();

    public void OnActivate(ObjAIBase owner) {
        _owner = owner;
        ApiEventManager.OnUpdateStats.AddListener(this, owner, OnUpdateStats);
    }

    private void OnUpdateStats(AttackableUnit unit, float diff) {
        
        const float duration  = 180f;
        int         wardCount = 0;
        if (_owner.HasBuff("YellowTrinketTracker")) {
            var buff = _owner.GetBuffWithName("YellowTrinketTracker").BuffScript as YellowTrinketTracker;
            wardCount = buff.GetWardCount();
        } else {
            var buff = AddBuff("YellowTrinketTracker", 25000f, 1, _owner.AutoAttackSpell, _owner, _owner, true).BuffScript as YellowTrinketTracker;
            wardCount = buff.GetWardCount();
        }
        
        foreach (var slot in from item in _owner.Inventory.GetAllItems() where item?.ItemData.ItemId == ItemId select _owner.Inventory.GetItemSlot(item)) {
            SetSpellToolTipVar(_owner, 2, duration, SpellbookType.SPELLBOOK_CHAMPION, slot, SpellSlotType.InventorySlots);
            SetSpellToolTipVar(_owner, 0, wardCount, SpellbookType.SPELLBOOK_CHAMPION, slot, SpellSlotType.InventorySlots);
            SetSpellToolTipVar(_owner, 1, 3, SpellbookType.SPELLBOOK_CHAMPION, slot, SpellSlotType.InventorySlots);
            return;
        }
    }
}