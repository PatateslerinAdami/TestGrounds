using System.Linq;
using System.Numerics;
using GameServerCore.Scripting.CSharp;
using ItemHelpers;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.Scripting.CSharp;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace ItemSpells
{
    public class Muramana : ISpellScript
    {
        public SpellScriptMetadata ScriptMetadata { get; } = new SpellScriptMetadata
        {
            CooldownIsAffectedByCDR = false
        };

        public void OnActivate(ObjAIBase owner, Spell spell)
        {
            spell.SetSpellToggle(owner.HasBuff("Muramana"));
        }

        public void OnSpellPreCast(ObjAIBase owner, Spell spell, AttackableUnit target, Vector2 start, Vector2 end)
        {
            if (!ItemEffectHelpers.CanUseActive(owner, spell, 3042))
            {
                return;
            }

            bool active = !owner.HasBuff("Muramana");
            if (active)
            {
                AddBuff("Muramana", 0, 1, spell, owner, owner, true);
            }
            else
            {
                RemoveBuff(owner, "Muramana");
            }

            var muramanaItems = owner.Inventory.GetAllItems().Where(i => i.ItemData.ItemId == 3042);
            foreach (var item in muramanaItems)
            {
                short spellSlot = (short)(6 + owner.Inventory.GetItemSlot(item));
                owner.Spells[spellSlot].SetSpellToggle(active);
            }
        }

        public void OnDeactivate(ObjAIBase owner, Spell spell)
        {
            bool hasOtherMuramana = owner.Inventory.GetAllItems().Any(i => i.ItemData.ItemId == 3042
                && owner.Inventory.GetItemSlot(i) + 6 != spell.CastInfo.SpellSlot);
            if (!hasOtherMuramana)
            {
                RemoveBuff(owner, "Muramana");
            }
        }
    }
}
