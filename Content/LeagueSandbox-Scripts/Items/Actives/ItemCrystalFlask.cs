using System.Numerics;
using LeagueSandbox.GameServer.GameObjects;
using GameServerCore.Scripting.CSharp;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.Scripting.CSharp;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace ItemSpells
{
    public class ItemCrystalFlask : ISpellScript
    {
        public SpellScriptMetadata ScriptMetadata { get; } = new SpellScriptMetadata
        {
            CooldownIsAffectedByCDR = false
        };

        private ObjAIBase _owner;
        private Spell _spell;
        private float _elapsed;
        private byte Slot => (byte)(_spell.CastInfo.SpellSlot - 6);

        private bool Ready
        {
            get
            {
                return !_owner.IsDead && _owner.Inventory.GetItem(Slot)?.ItemData.ItemId == 2041
                    && _owner.Inventory.GetItem(Slot).SpellCharges > 0;
            }
        }

        public void OnActivate(ObjAIBase owner, Spell spell)
        {
            _owner = owner;
            _spell = spell;
            var item = owner.Inventory.GetItem(Slot);
            if (item != null && !item.GetState().GetBool("flaskInitialized"))
            {
                item.GetState().Set("flaskInitialized", true);
                SetItemCharges(owner, Slot, 3);
            }

            ApiEventManager.OnCanCast.AddListener(this, owner, (unit, cast) => cast != spell || Ready);
        }

        public void OnSpellPreCast(ObjAIBase owner, Spell spell, AttackableUnit target, Vector2 start, Vector2 end)
        {
            if (!Ready)
            {
                return;
            }

            SetItemCharges(owner, Slot, owner.Inventory.GetItem(Slot).SpellCharges - 1);
            var recovery = owner.GetBuffWithName("ItemCrystalFlask");
            if (recovery != null && !recovery.Elapsed())
            {
                recovery.ExtendDuration(12);
            }
            else
            {
                AddBuff("ItemCrystalFlask", 12, 1, spell, owner, owner);
            }
        }

        public void OnUpdate(float diff)
        {
            _elapsed += diff;
            if (_elapsed < 250)
            {
                return;
            }

            _elapsed %= 250;
            if (!_owner.IsDead && IsInFountain(_owner) && _owner.Inventory.GetItem(Slot)?.ItemData.ItemId == 2041
                && _owner.Inventory.GetItem(Slot).SpellCharges < 3)
            {
                SetItemCharges(_owner, Slot, 3);
            }
        }

        public void OnDeactivate(ObjAIBase owner, Spell spell)
        {
            ApiEventManager.RemoveAllListenersForOwner(this);
        }
    }
}
