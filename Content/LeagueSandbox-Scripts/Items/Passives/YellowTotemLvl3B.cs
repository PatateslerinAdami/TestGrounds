using System.Collections;
using System.Linq;
using Buffs;
using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using GameServerLib.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.StatsNS;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;
using static LeagueSandbox.GameServer.API.ApiMapFunctionManager;

namespace ItemPassives;

public class ItemID_3362 : IItemScript {
    private const    int       ItemId = 3362;
    private          ObjAIBase _owner;

    public StatsModifier StatsModifier { get; } = new();

    public void OnActivate(ObjAIBase owner) {
        _owner = owner;
        ApiEventManager.OnUpdateStats.AddListener(this, owner, OnUpdateStats);
        if (GameTime() < 30000) {
            _owner.GetSpell("TrinketTotemLvl3B").SetCooldown((30000 - GameTime())/1000, true);
        }
    }

    private void OnUpdateStats(AttackableUnit unit, float diff) {
        
        int wardCount = 0;
        if (_owner.HasBuff("VisionWardTracker")) {
            var buff = _owner.GetBuffWithName("VisionWardTracker").BuffScript as VisionWardTracker;
            wardCount = buff.GetWardCount();
        } else {
            var buff = AddBuff("VisionWardTracker", 25000f, 1, _owner.AutoAttackSpell, _owner, _owner, true).BuffScript as VisionWardTracker;
            wardCount = buff.GetWardCount();
        }
        
        foreach (var slot in from item in _owner.Inventory.GetAllItems() where item?.ItemData.ItemId == ItemId select _owner.Inventory.GetItemSlot(item)) {
            SetSpellToolTipVar(_owner, 0, wardCount, SpellbookType.SPELLBOOK_CHAMPION, slot, SpellSlotType.InventorySlots);
            SetSpellToolTipVar(_owner, 1, 1, SpellbookType.SPELLBOOK_CHAMPION, slot, SpellSlotType.InventorySlots);
            return;
        }
    }
}
