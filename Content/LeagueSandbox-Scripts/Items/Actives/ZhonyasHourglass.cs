using System.Numerics;
using GameServerCore.Scripting.CSharp;
using ItemHelpers;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.Scripting.CSharp;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace ItemSpells
{
    public class ZhonyasHourglass : ISpellScript
    {
        public SpellScriptMetadata ScriptMetadata { get; } = new SpellScriptMetadata
        {
            CooldownIsAffectedByCDR = false
        };

        private int _itemId;
        public void OnActivate(ObjAIBase owner, Spell spell)
        {
            _itemId = owner.Inventory.GetItem((byte)(spell.CastInfo.SpellSlot - 6))?.ItemData.ItemId ?? 0;
            ItemEffectHelpers.RestoreSharedActiveCooldown(owner, spell, _itemId);
            ApiEventManager.OnCanCast.AddListener(
                this,
                owner,
                (unit, cast) => cast != spell || ItemEffectHelpers.CanUseSharedActive(owner, spell, _itemId)
            );
        }

        public void OnSpellPreCast(ObjAIBase owner, Spell spell, AttackableUnit target, Vector2 start, Vector2 end)
        {
            if (!ItemEffectHelpers.CanUseSharedActive(owner, spell, _itemId))
            {
                return;
            }

            var buffName = _itemId == 3090 ? "WoogletsWitchcap" : "ZhonyasRingShield";
            AddBuff(buffName, 2.5f, 1, spell, owner, owner);
            ItemEffectHelpers.ShareActiveCooldown(owner, spell, _itemId, 90);
        }

        public void OnDeactivate(ObjAIBase owner, Spell spell)
        {
            ApiEventManager.RemoveAllListenersForOwner(this);
        }
    }
}
