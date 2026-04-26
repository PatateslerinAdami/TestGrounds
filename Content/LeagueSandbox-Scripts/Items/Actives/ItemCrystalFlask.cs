using System;
using System.Numerics;
using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.StatsNS;
using LeagueSandbox.GameServer.Scripting.CSharp;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace ItemSpells;

public class ItemCrystalFlask : ISpellScript {
    private       ObjAIBase      _owner;
    private       Spell          _spell;
    private const float          IntervalMs = 250f;
    private       PeriodicTicker _periodicTicker;

    public SpellScriptMetadata ScriptMetadata => new() {
    };
    
    public StatsModifier StatsModifier { get; } = new();

    public void OnActivate(ObjAIBase owner, Spell spell) {
        _owner = owner;
        _spell = spell;
        for (var i = 0; i < 3; i++) {
            AddBuff("ItemCrystalFlaskCharge", 25000f, 1, spell, owner, owner);
        }

        SealSpellSlot(owner, SpellSlotType.InventorySlots, ToInventorySlotIndex(spell), SpellbookType.SPELLBOOK_CHAMPION, false);
    }

    public void OnSpellPreCast(ObjAIBase owner, Spell spell, AttackableUnit target, Vector2 start, Vector2 end) {
        var inventorySlot = ToInventorySlotIndex(spell);
        var chargeBuff = owner.GetBuffWithName("ItemCrystalFlaskCharge");
        if (chargeBuff != null) {
            SealSpellSlot(owner, SpellSlotType.InventorySlots, inventorySlot, SpellbookType.SPELLBOOK_CHAMPION, false);
            if (owner.GetBuffsWithName("ItemCrystalFlaskCharge").Count == 1) {
                SealSpellSlot(owner, SpellSlotType.InventorySlots, inventorySlot, SpellbookType.SPELLBOOK_CHAMPION, true);
            }
            AddBuff("ItemCrystalFlask", 12, 1, spell, owner, owner);
            RemoveBuff(chargeBuff);
        } else {
            SealSpellSlot(owner, SpellSlotType.InventorySlots, inventorySlot, SpellbookType.SPELLBOOK_CHAMPION, true);
        }
    }

    public void OnUpdate(float diff) {
        var ticks = _periodicTicker.ConsumeTicks(diff, IntervalMs, fireImmediately: true, maxTicksPerUpdate: 1);
        if (ticks != 1) return;
        var inventorySlot = ToInventorySlotIndex(_spell);
        if (IsInFountain(_owner)) {
            if (_owner.GetBuffsWithName("ItemCrystalFlaskCharge").Count == 3 && _owner.HasBuff("ItemCrystalFlask")) {
                SealSpellSlot(_owner, SpellSlotType.InventorySlots, inventorySlot, SpellbookType.SPELLBOOK_CHAMPION, true);
            } else {
                SealSpellSlot(_owner, SpellSlotType.InventorySlots, inventorySlot, SpellbookType.SPELLBOOK_CHAMPION, false);
            }
            for (var i = 0; i < 3; i++) {
                AddBuff("ItemCrystalFlaskCharge", 25000f, 1, _spell, _owner, _owner);
            }
        } else if (_owner.GetBuffWithName("ItemCrystalFlaskCharge") != null) 
        {
            SealSpellSlot(_owner, SpellSlotType.InventorySlots, inventorySlot, SpellbookType.SPELLBOOK_CHAMPION, false);
        }
    }
}
