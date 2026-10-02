using System;
using System.Numerics;
using LeagueSandbox.GameServer.Inventory;
using GameServerCore.Scripting.CSharp;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.Scripting.CSharp;

namespace ItemHelpers
{
    // Named actives share cooldown across different items carrying the same spell.
    public abstract class ItemActiveSpell : ISpellScript
    {
        public virtual SpellScriptMetadata ScriptMetadata { get; } = new SpellScriptMetadata
        {
            CooldownIsAffectedByCDR = false,
            CastingBreaksStealth = true
        };

        protected virtual float Cooldown => 60;

        private int _itemId;
        public virtual void OnActivate(ObjAIBase owner, Spell spell)
        {
            _itemId = owner.Inventory.GetItem((byte)(spell.CastInfo.SpellSlot - 6))?.ItemData.ItemId ?? 0;
            for (int i = 0; i < spell.SpellData.Cooldown.Length; i++)
            {
                spell.SpellData.Cooldown[i] = Cooldown;
            }

            var remaining = spell.CurrentCooldown;
            foreach (var item in owner.Inventory.GetAllItems())
            {
                if (ItemCooldownGroups.Get(item.ItemData.SpellName) != ItemCooldownGroups.Get(spell.SpellName))
                {
                    continue;
                }

                var other = owner.Spells[(short)(owner.Inventory.GetItemSlot(item) + 6)];
                remaining = Math.Max(remaining, other.CurrentCooldown);
            }

            if (remaining > spell.CurrentCooldown)
            {
                spell.SetCooldown(remaining, true);
            }

            ApiEventManager.OnCanCast.AddListener(this, owner, (unit, cast) => cast != spell || Ready(owner, spell));
        }

        protected bool Ready(ObjAIBase owner, Spell spell)
        {
            if (!ItemEffectHelpers.CanUseActive(owner, spell, _itemId))
            {
                return false;
            }

            foreach (var item in owner.Inventory.GetAllItems())
            {
                if (ItemCooldownGroups.Get(item.ItemData.SpellName) != ItemCooldownGroups.Get(spell.SpellName))
                {
                    continue;
                }

                var other = owner.Spells[(short)(owner.Inventory.GetItemSlot(item) + 6)];
                if (other != spell && other.CurrentCooldown > 0)
                {
                    return false;
                }
            }

            return true;
        }

        public virtual void OnSpellPreCast(
            ObjAIBase owner,
            Spell spell,
            AttackableUnit target,
            Vector2 start,
            Vector2 end
        )
        {
            if (!Ready(owner, spell) || !Use(owner, spell, target, end))
            {
                spell.CancelCast();
                return;
            }

            foreach (var item in owner.Inventory.GetAllItems())
            {
                if (ItemCooldownGroups.Get(item.ItemData.SpellName) == ItemCooldownGroups.Get(spell.SpellName))
                {
                    owner.Spells[(short)(owner.Inventory.GetItemSlot(item) + 6)].SetCooldown(Cooldown, true);
                }
            }
        }

        protected abstract bool Use(ObjAIBase owner, Spell spell, AttackableUnit target, Vector2 position);
        public virtual void OnDeactivate(ObjAIBase owner, Spell spell)
        {
            ApiEventManager.RemoveAllListenersForOwner(this);
        }
    }
}
