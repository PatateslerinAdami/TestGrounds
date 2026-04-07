using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using GameServerLib.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.StatsNS;
using LeagueSandbox.GameServer.Scripting.CSharp;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace ItemPassives
{
    public class ItemID_3078 : IItemScript
    {
        public StatsModifier StatsModifier { get; } = new StatsModifier();
        private ObjAIBase _owner;

        public void OnActivate(ObjAIBase owner)
        {
            _owner = owner;

            for (short i = 0; i <= 3; i++)
            {
                if (_owner.Spells.TryGetValue(i, out Spell spell) && spell != null)
                {
                    ApiEventManager.OnSpellCast.AddListener(this, spell, OnSpellCast);
                }
            }
        }

        public void OnDeactivate(ObjAIBase owner)
        {
            ApiEventManager.RemoveAllListenersForOwner(this);
        }

        private void OnSpellCast(Spell spell)
        {
            Spell TrinitySheenItemSpell = GetTrinitySheenSpell();

            if (TrinitySheenItemSpell != null && TrinitySheenItemSpell.CurrentCooldown <= 0f && !_owner.HasBuff("Sheen"))
            {
                AddBuff("Sheen", 10.0f, 1, spell, _owner, _owner);
            }
        }

        private Spell GetTrinitySheenSpell()
        {
            for (byte i = 0; i < 7; i++)
            {
                var item = _owner.Inventory.GetItem(i);
                if (item != null && item.ItemData.ItemId == 3078)
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
}
