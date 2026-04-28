using System.Collections;
using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using GameServerLib.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.StatsNS;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace ItemPassives;

public class ItemID_3301 : IItemScript {
    private const    int       ItemId = 3301;
    private const    int       FavorGoldPerProc = 2;
    private readonly ArrayList _minions = new();
    private          ObjAIBase _owner;
    private          Champion  _ch;
    private          int       _favorGoldLooted;

    public StatsModifier StatsModifier { get; } = new();

    public void OnActivate(ObjAIBase owner) {
        _owner = owner;
        _ch     = owner as Champion;
        UpdateFavorTooltip();
    }

    public void OnUpdate(float diff) {
        var units = GetUnitsInRange(_owner, _owner.Position, 1400, false, SpellDataFlags.AffectEnemies | SpellDataFlags.AffectMinions);
        foreach (var unit in units) {
            if (_minions.Contains(unit as Minion)) continue;
            _minions.Add(unit as Minion);
            ApiEventManager.OnDeath.AddListener(_owner, unit as Minion, MinionExecute, true);
        }
    }

    private void MinionExecute(DeathData data) {
        _minions.Remove(data.Unit as Minion);
        if (data.Killer == _owner) return;
        _ch.AddGold(_ch, FavorGoldPerProc);
        _favorGoldLooted += FavorGoldPerProc;
        UpdateFavorTooltip();
        StatsModifier.HealthPoints.FlatBonus = 5.0F;
        _owner.AddStatModifier(StatsModifier);
    }

    private void UpdateFavorTooltip() {
        if (_ch == null || _owner == null) return;

        foreach (var item in _owner.Inventory.GetAllItems()) {
            if (item?.ItemData.ItemId != ItemId) continue;
            var slot = _owner.Inventory.GetItemSlot(item);

            SetSpellToolTipVar(_owner, 1, _favorGoldLooted, SpellbookType.SPELLBOOK_CHAMPION, slot,
                               SpellSlotType.InventorySlots);
            return;
        }
    }
}
