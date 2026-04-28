using System;
using System.Numerics;
using Buffs;
using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using GameServerLib.GameObjects;
using LeagueSandbox.GameServer.GameObjects;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.Inventory;
using LeagueSandbox.GameServer.Scripting.CSharp;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;
using static LeagueSandbox.GameServer.API.ApiMapFunctionManager;

namespace ItemSpells;

public class ItemGhostWard : ISpellScript {
    private const int MaxCharges = 4;

    private Minion         _ward;
    private ObjAIBase      _owner;
    private Spell          _spell;

    public SpellScriptMetadata ScriptMetadata => new() {
        TriggersSpellCasts = false,
        AmmoPerCharge = 1
    };

    public void OnActivate(ObjAIBase owner, Spell spell) {
        _owner = owner;
        _spell = spell;
        EnsureValidCharges();
        UpdateSlotSeal();
    }

    private Item GetBoundItem() {
        return _owner.Inventory.GetItem(_spell.SpellName);
    }

    private int GetInventorySlot(Item item = null) {
        item ??= GetBoundItem();
        if (item == null) return -1;

        return _owner.Inventory.GetItemSlot(item);
    }

    private void UpdateSlotSeal(Item item = null) {
        item ??= GetBoundItem();
        var slot = GetInventorySlot(item);
        if (slot < 0) return;

        var shouldSeal = item == null || item.SpellCharges <= 0;
        SealSpellSlot(_owner, SpellSlotType.InventorySlots, slot, SpellbookType.SPELLBOOK_CHAMPION, shouldSeal);
    }

    private void EnsureValidCharges() {
        var item = GetBoundItem();
        if (item == null) return;

        var clamped = Math.Clamp(item.SpellCharges, 0, MaxCharges);
        if (clamped == 0)
            clamped = MaxCharges;

        _owner.Inventory.SetItemSpellCharges(item, _owner, clamped);
    }

    public void OnSpellPreCast(ObjAIBase owner, Spell spell, AttackableUnit target, Vector2 start, Vector2 end) {
        var item = GetBoundItem();
        if (item == null) return;
        if (item.SpellCharges <= 0) {
            UpdateSlotSeal(item);
            return;
        }
        
        var cursor   = new Vector2(spell.CastInfo.TargetPosition.X, spell.CastInfo.TargetPosition.Z);
        var current  = new Vector2(owner.Position.X,                owner.Position.Y);
        var distance = cursor - current;

        float   duration = 180f;
        
        Vector2 truecoords;
        if (distance.Length() > 500f) {
            distance = Vector2.Normalize(distance);
            var range = distance * 500f;
            truecoords = current + range;
        } else { truecoords = cursor; }

        _ward = AddMinion(owner, "YellowTrinket", "YellowTrinket", truecoords, owner.Team, 0, true, true, true);
        _ward.Stats.ManaPoints.BaseValue = duration;
        _ward.Stats.CurrentMana          = duration;
        AddParticle(owner, _ward, "TrinketOrbLvl1Audio", truecoords);
        if (owner.HasBuff("YellowTrinketTracker")) {
            var buff = owner.GetBuffWithName("YellowTrinketTracker").BuffScript as YellowTrinketTracker;
            buff?.AddWard(_ward);
        } else {
           var buff = AddBuff("YellowTrinketTracker", 25000f, 1, spell, owner, owner, true).BuffScript as YellowTrinketTracker;
           buff?.AddWard(_ward);
        }

        _owner.Inventory.SetItemSpellCharges(item, _owner, Math.Clamp(item.SpellCharges - 1, 0, MaxCharges));
        UpdateSlotSeal(item);
    }

    public void OnUpdate(float diff) {
        var item = GetBoundItem();
        if (item == null) return;

        if (IsInFountain(_owner) && item.SpellCharges < MaxCharges)
            _owner.Inventory.SetItemSpellCharges(item, _owner, MaxCharges);
        else if (item.SpellCharges > MaxCharges)
            _owner.Inventory.SetItemSpellCharges(item, _owner, MaxCharges);

        UpdateSlotSeal(item);
    }
}
