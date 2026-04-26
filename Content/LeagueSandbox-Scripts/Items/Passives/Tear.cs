using System;
using System.Collections;
using System.Linq;
using System.Numerics;
using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using GameServerLib.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.StatsNS;
using LeagueSandbox.GameServer.Scripting.CSharp;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace ItemPassives;

public class ItemID_3070 : IItemScript {
    private const    int            ItemId = 3070;
    private const    string         SpellName = "TearsDummySpell";
    private const    float          TickPeriodMs = 8000f;
    private          ObjAIBase      _owner;
    private          Champion       _ch;
    private          int            _storage = 2;
    private   static int            _stacks = 0;
    private          PeriodicTicker _periodicTicker;
    public           StatsModifier  StatsModifier { get; } = new();

    public void OnActivate(ObjAIBase owner) {
        _owner                             = owner;
        _ch                                = owner as Champion;
        StatsModifier.ManaPoints.FlatBonus = _stacks;
        for (short i = 0; i < 4; i++) {
            ApiEventManager.OnSpellCast.AddListener(this, owner.Spells[i], OnSpellsCast);
        }
        SyncDummyCooldown();
    }

    public void OnUpdate(float diff) {
        if (_stacks == 750) return;
        var ticks = _periodicTicker.ConsumeTicks(diff, TickPeriodMs, false, 1);
        if (ticks == 1) {
            UpdateTearStack(1);
            _storage = 2;
            SyncDummyCooldown();
        }
    }

    private void OnSpellsCast(Spell spell) {
        if (_storage <= 0 || _stacks == 750) return;

        var dummySpell = _owner.GetSpell(SpellName);
        if (dummySpell == null) return;

        UpdateTearStack(4);
        _storage--;
        SpellCast(_owner, dummySpell.CastInfo.SpellSlot, SpellSlotType.TempItemSlot, true, _owner, Vector2.Zero);
        SyncDummyCooldown();
    }

    private void SyncDummyCooldown() {
        var dummySpell = _owner.GetSpell(SpellName);

        if (_storage > 0) {
            dummySpell.SetCooldown(0f, true);
            return;
        }

        var remainingMs = _periodicTicker.GetRemainingMsUntilNextTick(TickPeriodMs);
        dummySpell.SetCooldown(MathF.Max(0f, remainingMs / 1000f), true);
    }
    
    private void UpdateTearStack(int amount) {
        if (_stacks == 750) return;
        _owner.RemoveStatModifier(StatsModifier);
        _stacks += amount;
        StatsModifier.ManaPoints.FlatBonus = _stacks;
        _owner.AddStatModifier(StatsModifier);
        _owner.Stats.CurrentMana += amount;
        if (_ch == null || _owner == null) return;

        foreach (var slot in from item in _owner.Inventory.GetAllItems() where item?.ItemData.ItemId == ItemId select _owner.Inventory.GetItemSlot(item)) {
            SetSpellToolTipVar(_owner, 0, _stacks , SpellbookType.SPELLBOOK_CHAMPION, slot,
                               SpellSlotType.InventorySlots);
            return;
        }
    }
}
