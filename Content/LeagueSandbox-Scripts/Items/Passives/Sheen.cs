using GameServerCore.Scripting.CSharp;
using System.Numerics;
using GameServerCore.Enums;
using GameServerLib.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.StatsNS;
using LeagueSandbox.GameServer.Logging;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;
using LeagueSandbox.GameServer.GameObjects;


namespace ItemPassives;

public class ItemID_3057 : IItemScript {
    private ObjAIBase     _owner;
    private const int ItemId = 3057;
    
    public         StatsModifier StatsModifier { get; } = new();

    public void OnActivate(ObjAIBase owner) {
        _owner = owner;
        SpellbladeManager.Register(owner, ItemId);
        for (byte i = 0; i < 4; i++) {
            if (_owner.Spells.TryGetValue(i, out Spell spell))
            {
                ApiEventManager.OnSpellCast.AddListener(this, spell, OnSpellCast);
            }
        }
        
    }

    public void OnSpellCast(Spell spell) {
        if (_owner == null || spell == null || !spell.Script.ScriptMetadata.TriggersSpellCasts || _owner.HasBuff("SheenDelay")) return;
        if (!SpellbladeManager.IsActive(_owner, ItemId)) return;
        if (SpellbladeManager.HasAnySpellbladeProc(_owner)) return;

        var itemSpell = GetSheenSpell();
        if (itemSpell == null || itemSpell.CurrentCooldown > 0f) return;

        var variables = new BuffVariables();
        variables.Set("sourceItemId", ItemId);
        variables.Set("damageAmount", _owner.Stats.AttackDamage.BaseValue);
        AddBuff("Sheen", 10f, 1, spell, _owner, _owner, buffVariables: variables);
    }

    public void OnDeactivate(ObjAIBase owner) {
        SpellbladeManager.Unregister(owner, ItemId);
        ApiEventManager.RemoveAllListenersForOwner(this);
        _owner = null;
    }

    private Spell GetSheenSpell()
    {
        for (byte i = 0; i < 7; i++)
        {
            var item = _owner.Inventory.GetItem(i);
            if (item != null && item.ItemData.ItemId == ItemId)
            {
                short spellSlot = (short)(i + (byte)SpellSlotType.InventorySlots);
                if (_owner.Spells.TryGetValue(spellSlot, out Spell s))
                {
                    return s;
                }
            }
        }

        return null;
    }
    
}
