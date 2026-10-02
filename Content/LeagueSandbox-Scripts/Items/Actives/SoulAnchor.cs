using System.Numerics;
using GameServerCore.Scripting.CSharp;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.Scripting.CSharp;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace ItemSpells
{
    public class OdinTrinketRevive : ISpellScript
    {
        public SpellScriptMetadata ScriptMetadata { get; } = new SpellScriptMetadata
        {
            CooldownIsAffectedByCDR = false
        };

        private ObjAIBase _owner;
        private Spell _spell;
        private bool IsReady()
        {
            if (_owner is not Champion)
            {
                return false;
            }

            if (!_owner.IsDead)
            {
                return false;
            }

            var buff = _owner.GetBuffWithName("SoulAnchorCharges");
            if (buff == null || buff.Variables.GetInt("charges") <= 0)
            {
                return false;
            }

            var trinketItem = _owner.Inventory.GetItem(6);
            if (trinketItem?.ItemData.SpellName != "OdinTrinketRevive")
            {
                return false;
            }

            return true;
        }

        public void OnActivate(ObjAIBase owner, Spell spell)
        {
            _owner = owner;
            _spell = spell;
            for (int i = 0; i < spell.SpellData.Cooldown.Length; i++)
            {
                spell.SpellData.Cooldown[i] = 0;
            }

            spell.SpellData.CanOnlyCastWhileDead = true;
            if (!owner.HasBuff("SoulAnchorCharges"))
            {
                AddBuff("SoulAnchorCharges", 0, 1, null, owner, owner, true);
            }

            int charges = owner.GetBuffWithName("SoulAnchorCharges").Variables.GetInt("charges");
            SetItemCharges(owner, 6, charges);
            ApiEventManager.OnCanCast.AddListener(this, owner, CanCastSpell);
        }

        private bool CanCastSpell(AttackableUnit unit, Spell castSpell)
        {
            if (castSpell != _spell)
            {
                return true;
            }

            return IsReady();
        }

        public void OnSpellPreCast(ObjAIBase owner, Spell spell, AttackableUnit target, Vector2 start, Vector2 end)
        {
            if (!IsReady())
            {
                return;
            }

            var state = owner.GetBuffWithName("SoulAnchorCharges").Variables;
            int remainingCharges = state.GetInt("charges") - 1;
            state.Set("charges", remainingCharges);
            SetItemCharges(owner, 6, remainingCharges);
            var champion = (Champion)owner;
            champion.Respawn();
            AddBuff("OdinReviveSpeed", 12, 1, spell, owner, owner);
        }

        public void OnDeactivate(ObjAIBase owner, Spell spell)
        {
            ApiEventManager.RemoveAllListenersForOwner(this);
        }
    }
}
