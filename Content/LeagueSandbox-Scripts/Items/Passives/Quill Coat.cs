using System.Collections.Generic;
using System.Linq;
using System.Numerics;
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
using Buffs;


namespace ItemPassives;

public class ItemID_3205 : IItemScript {
    private const float BonusHealthRegenPer5 = 40f;
    private const float BonusManaRegenPer5   = 30f;
    private const float RegenWindowMs        = 5000f;
    private const float BonusHealthRegen     = BonusHealthRegenPer5 / 5f;
    private const float BonusManaRegen       = BonusManaRegenPer5   / 5f;
    private const int   ItemId               = 3205;

    private ObjAIBase      _owner;
    private PeriodicTicker _periodicTicker;
    private bool           _enabled = false;
    public  StatsModifier  StatsModifier { get; } = new();

    public void OnActivate(ObjAIBase owner) {
        _owner = owner;
        ApiEventManager.OnTakeDamage.AddListener(this, owner, OnTakeDamage);
        ApiEventManager.OnUpdateStats.AddListener(this, owner, OnUpdateStats);
    }

    public void OnUpdate(float diff) {
        if (!_enabled) return;
        var ticks = _periodicTicker.ConsumeTicks(diff, RegenWindowMs, false, 1);
        if (ticks != 1) return;
        _owner.Stats.HealthRegeneration.FlatBonus -= BonusHealthRegen;
        _owner.Stats.ManaRegeneration.FlatBonus   -= BonusManaRegen;
        _enabled                                  =  false;
    }

    private void OnTakeDamage(DamageData data) {
        if (!IsValidTarget(_owner, data.Attacker, SpellDataFlags.AffectNeutral)) return;
        var bufVariables = new BuffVariables();
        bufVariables.Set("damageAmount", data.Attacker.Stats.HealthPoints.Total * 0.05f);
        AddBuff("ItemMonsterBurn", 3f, 1, _owner.AutoAttackSpell, data.Attacker, _owner, buffVariables: bufVariables);
        if (!_enabled) {
            _owner.Stats.HealthRegeneration.FlatBonus += BonusHealthRegen;
            _owner.Stats.ManaRegeneration.FlatBonus   += BonusManaRegen;
        }

        _periodicTicker.Reset();
        _enabled = true;
    }

    private void OnUpdateStats(AttackableUnit unit, float diff) {
        var duration  = _owner.Stats.Level < 9 ? 60f : 120f;
        int wardCount = 0;
        if (_owner.HasBuff("YellowTrinketTracker")) {
            var buff = _owner.GetBuffWithName("YellowTrinketTracker").BuffScript as YellowTrinketTracker;
            wardCount = buff.GetWardCount();
        } else {
            var buff = AddBuff("YellowTrinketTracker", 25000f, 1, _owner.AutoAttackSpell, _owner, _owner, true)
                           .BuffScript as YellowTrinketTracker;
            wardCount = buff.GetWardCount();
        }

        foreach (var slot in from item in _owner.Inventory.GetAllItems()
                             where item?.ItemData.ItemId == ItemId
                             select _owner.Inventory.GetItemSlot(item)) {
            SetSpellToolTipVar(_owner, 2, duration, SpellbookType.SPELLBOOK_CHAMPION, slot,
                               SpellSlotType.InventorySlots);
            SetSpellToolTipVar(_owner, 0, wardCount, SpellbookType.SPELLBOOK_CHAMPION, slot,
                               SpellSlotType.InventorySlots);
            SetSpellToolTipVar(_owner, 1, 3, SpellbookType.SPELLBOOK_CHAMPION, slot, SpellSlotType.InventorySlots);
            return;
        }
    }
}