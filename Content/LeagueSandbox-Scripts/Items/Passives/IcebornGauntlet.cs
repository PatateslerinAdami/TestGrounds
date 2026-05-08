using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using GameServerLib.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.StatsNS;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace ItemPassives;

public class ItemID_3025 : IItemScript
{
    private ObjAIBase _owner;
    private const int ItemId = 3025;

    public StatsModifier StatsModifier { get; } = new();

    public void OnActivate(ObjAIBase owner)
    {
        _owner = owner;
        SpellbladeManager.Register(owner, ItemId);

        for (byte i = 0; i < 4; i++)
        {
            if (_owner.Spells.TryGetValue(i, out Spell spell) && spell != null)
            {
                ApiEventManager.OnSpellCast.AddListener(this, spell, OnSpellCast);
            }
        }
    }

    public void OnDeactivate(ObjAIBase owner)
    {
        SpellbladeManager.Unregister(owner, ItemId);
        ApiEventManager.RemoveAllListenersForOwner(this);
        _owner = null;
    }

    public void OnUpdate(float diff)
    {
    }

    private void OnSpellCast(Spell spell)
    {
        if (_owner == null || spell == null)
        {
            return;
        }

        if (!spell.Script.ScriptMetadata.TriggersSpellCasts)
        {
            return;
        }

        if (!SpellbladeManager.IsActive(_owner, ItemId))
        {
            return;
        }

        if (SpellbladeManager.HasAnySpellbladeProc(_owner))
        {
            return;
        }

        var itemSpell = GetIcebornSpell();

        if (itemSpell == null || itemSpell.CurrentCooldown > 0f)
        {
            return;
        }

        var variables = new BuffVariables();
        variables.Set("damageMultiplier", 1.25f);
        variables.Set("zoneDuration", 2.0f);
        variables.Set("slowPercent", 0.30f);
        variables.Set("itemCooldown", 1.5f);
        variables.Set("sourceItemId", 3025);

        // Visible player Spellblade icon. Mechanics live in IcebornGauntletProc.
        AddBuff(
            "ItemFrozenFist",
            10f,
            1,
            spell,
            _owner,
            _owner
        );

        // Hidden Iceborn mechanic.
        AddBuff(
            "IcebornGauntletProc",
            10f,
            1,
            spell,
            _owner,
            _owner,
            buffVariables: variables
        );
    }

    private Spell GetIcebornSpell()
    {
        for (byte i = 0; i < 7; i++)
        {
            var item = _owner.Inventory.GetItem(i);
            if (item != null && item.ItemData.ItemId == 3025)
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
